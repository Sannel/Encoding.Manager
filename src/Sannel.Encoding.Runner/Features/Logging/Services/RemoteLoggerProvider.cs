using System.Collections.Concurrent;
using System.Net.Http.Json;
using Sannel.Encoding.Runner.Features.Logging.Dto;
using Sannel.Encoding.Runner.Features.Runner.Services;

namespace Sannel.Encoding.Runner.Features.Logging.Services;

public sealed class RemoteLoggerProvider : ILoggerProvider
{
	private readonly IHttpClientFactory _httpClientFactory;
	private readonly string _source;
	private readonly ConcurrentQueue<LogEntryDto> _buffer = new();
	private readonly Timer _flushTimer;
	private readonly SemaphoreSlim _flushLock = new(1, 1);
	private bool _disposed;

	public RemoteLoggerProvider(IHttpClientFactory httpClientFactory, string source)
	{
		this._httpClientFactory = httpClientFactory;
		this._source = source;
		this._flushTimer = new Timer(_ => _ = this.FlushAsync(), null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
	}

	public ILogger CreateLogger(string categoryName) =>
		new RemoteLogger(this, categoryName);

	internal void Enqueue(LogEntryDto entry) => this._buffer.Enqueue(entry);

	private async Task FlushAsync()
	{
		if (this._buffer.IsEmpty || this._disposed)
		{
			return;
		}

		if (!this._flushLock.Wait(0))
		{
			return;
		}

		try
		{
			var batch = new List<LogEntryDto>();
			while (this._buffer.TryDequeue(out var entry) && batch.Count < 500)
			{
				batch.Add(entry);
			}

			if (batch.Count == 0)
			{
				return;
			}

			var request = new LogIngestRequest
			{
				Source = this._source,
				Entries = batch,
			};

			try
			{
				var client = this._httpClientFactory.CreateClient(nameof(IRunnerApiClient));
				await client.PostAsJsonAsync("api/logs/ingest", request).ConfigureAwait(false);
			}
			catch
			{
				// Re-enqueue on failure (up to a limit to avoid unbounded growth)
				if (this._buffer.Count < 5000)
				{
					foreach (var entry in batch)
					{
						this._buffer.Enqueue(entry);
					}
				}
			}
		}
		finally
		{
			this._flushLock.Release();
		}
	}

	public void Dispose()
	{
		if (this._disposed)
		{
			return;
		}

		this._disposed = true;
		this._flushTimer.Dispose();

		// Final flush
		this.FlushAsync().GetAwaiter().GetResult();

		this._flushLock.Dispose();
	}
}

public sealed class RemoteLogger : ILogger
{
	private readonly RemoteLoggerProvider _provider;
	private readonly string _categoryName;

	public RemoteLogger(RemoteLoggerProvider provider, string categoryName) =>
		(this._provider, this._categoryName) = (provider, categoryName);

	public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

	public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

	public void Log<TState>(
		LogLevel logLevel,
		EventId eventId,
		TState state,
		Exception? exception,
		Func<TState, Exception?, string> formatter)
	{
		if (!this.IsEnabled(logLevel))
		{
			return;
		}

		this._provider.Enqueue(new LogEntryDto
		{
			Timestamp = DateTimeOffset.UtcNow,
			Level = logLevel.ToString(),
			Category = this._categoryName,
			Message = formatter(state, exception),
			Exception = exception?.ToString(),
		});
	}
}

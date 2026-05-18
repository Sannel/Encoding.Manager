using Microsoft.EntityFrameworkCore;
using Sannel.Encoding.Manager.Web.Features.Data;
using Sannel.Encoding.Manager.Web.Features.Logging.Entities;

namespace Sannel.Encoding.Manager.Web.Features.Logging.Services;

public sealed class DatabaseLoggerProvider : ILoggerProvider
{
	private readonly IDbContextFactory<AppDbContext> _contextFactory;
	private readonly string _source;

	public DatabaseLoggerProvider(IDbContextFactory<AppDbContext> contextFactory, string source) =>
		(this._contextFactory, this._source) = (contextFactory, source);

	public ILogger CreateLogger(string categoryName) =>
		new DatabaseLogger(this._contextFactory, categoryName, this._source);

	public void Dispose()
	{
	}
}

public sealed class DatabaseLogger : ILogger
{
	private readonly IDbContextFactory<AppDbContext> _contextFactory;
	private readonly string _categoryName;
	private readonly string _source;

	public DatabaseLogger(IDbContextFactory<AppDbContext> contextFactory, string categoryName, string source) =>
		(this._contextFactory, this._categoryName, this._source) = (contextFactory, categoryName, source);

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

		var entry = new LogEntry
		{
			Timestamp = DateTimeOffset.UtcNow,
			Level = logLevel.ToString(),
			Category = this._categoryName,
			Message = formatter(state, exception),
			Exception = exception?.ToString(),
			Source = this._source,
		};

		try
		{
			using var context = this._contextFactory.CreateDbContext();
			context.LogEntries.Add(entry);
			context.SaveChanges();
		}
		catch
		{
			// Swallow failures — we can't log about logging failures.
		}
	}
}

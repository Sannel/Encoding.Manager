using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using MudBlazor;
using Sannel.Encoding.Manager.Web.Features.Data;
using Sannel.Encoding.Manager.Web.Features.Logging.Components;
using Sannel.Encoding.Manager.Web.Features.Logging.Entities;
using System.Text;

namespace Sannel.Encoding.Manager.Web.Features.Logging.Pages;

public partial class LogsPage : ComponentBase
{
	[Inject]
	private IDbContextFactory<AppDbContext> ContextFactory { get; set; } = default!;

	[Inject]
	private IDialogService DialogService { get; set; } = default!;

	[Inject]
	private ISnackbar Snackbar { get; set; } = default!;

	[Inject]
	private IJSRuntime JS { get; set; } = default!;

	private const int PageSize = 50;

	private List<LogEntry> _logs = [];
	private int _page;
	private int _totalCount;
	private bool _isLoading;

	private string? _levelFilter;
	private string? _sourceFilter;
	private string? _searchTerm;

	private List<string> _knownSources = [];

	protected override async Task OnInitializedAsync()
	{
		await this.LoadPageAsync();
	}

	private async Task SearchAsync()
	{
		this._page = 0;
		await this.LoadPageAsync();
	}

	private async Task LoadPageAsync()
	{
		this._isLoading = true;
		try
		{
			await using var context = await this.ContextFactory.CreateDbContextAsync();
			var query = context.LogEntries.AsNoTracking().AsQueryable();

			if (!string.IsNullOrWhiteSpace(this._levelFilter))
			{
				query = query.Where(e => e.Level == this._levelFilter);
			}

			if (!string.IsNullOrWhiteSpace(this._sourceFilter))
			{
				query = query.Where(e => e.Source == this._sourceFilter);
			}

			if (!string.IsNullOrWhiteSpace(this._searchTerm))
			{
				query = query.Where(e => e.Message.Contains(this._searchTerm)
					|| (e.Exception != null && e.Exception.Contains(this._searchTerm)));
			}

			this._totalCount = await query.CountAsync();

			this._logs = await query
				.OrderByDescending(e => e.Timestamp)
				.Skip(this._page * PageSize)
				.Take(PageSize)
				.ToListAsync();

			var sources = this._logs
				.Where(l => l.Source is not null && l.Source != "Server")
				.Select(l => l.Source!)
				.Distinct()
				.OrderBy(s => s)
				.ToList();

			foreach (var source in sources)
			{
				if (!this._knownSources.Contains(source))
				{
					this._knownSources.Add(source);
				}
			}
		}
		catch (Exception ex)
		{
			this.Snackbar.Add($"Failed to load logs: {ex.Message}", Severity.Error);
		}
		finally
		{
			this._isLoading = false;
		}
	}

	private async Task PreviousPageAsync()
	{
		if (this._page > 0)
		{
			this._page--;
			await this.LoadPageAsync();
		}
	}

	private async Task NextPageAsync()
	{
		this._page++;
		await this.LoadPageAsync();
	}

	private async Task OnSearchKeyDown(KeyboardEventArgs e)
	{
		if (e.Key == "Enter")
		{
			await this.SearchAsync();
		}
	}

	private async Task PurgeOldLogsAsync()
	{
		var result = await this.DialogService.ShowMessageBoxAsync(
			"Purge Old Logs",
			"Delete all log entries older than 30 days?",
			yesText: "Delete",
			cancelText: "Cancel");

		if (result == true)
		{
			await using var context = await this.ContextFactory.CreateDbContextAsync();
			var cutoff = DateTimeOffset.UtcNow.AddDays(-30);
			var deleted = await context.LogEntries
				.Where(e => e.Timestamp < cutoff)
				.ExecuteDeleteAsync();
			this.Snackbar.Add($"Deleted {deleted} old log entries.", Severity.Success);
			await this.LoadPageAsync();
		}
	}

	private async Task PurgeAllLogsAsync()
	{
		var result = await this.DialogService.ShowMessageBoxAsync(
			"Purge All Logs",
			"Delete all log entries, including entries from the last 30 days?",
			yesText: "Delete All",
			cancelText: "Cancel");

		if (result == true)
		{
			await using var context = await this.ContextFactory.CreateDbContextAsync();
			var deleted = await context.LogEntries.ExecuteDeleteAsync();
			this.Snackbar.Add($"Deleted {deleted} log entries.", Severity.Success);
			this._page = 0;
			await this.LoadPageAsync();
		}
	}

	private async Task ShowDetailAsync(LogEntry entry)
	{
		var parameters = new DialogParameters<LogDetailDialog>
		{
			{ x => x.Entry, entry }
		};
		await this.DialogService.ShowAsync<LogDetailDialog>("Log Detail", parameters,
			new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true });
	}

	private async Task CopyForAiAsync(LogEntry entry)
	{
		var sb = new StringBuilder();
		sb.AppendLine("I encountered the following error in my application and need help debugging it.");
		sb.AppendLine();
		sb.AppendLine($"**Level:** {entry.Level}");
		sb.AppendLine($"**Timestamp:** {entry.Timestamp:O}");
		sb.AppendLine($"**Source:** {entry.Source ?? "Server"}");
		sb.AppendLine($"**Category:** {entry.Category}");
		sb.AppendLine();
		sb.AppendLine("**Message:**");
		sb.AppendLine($"```");
		sb.AppendLine(entry.Message);
		sb.AppendLine($"```");

		if (!string.IsNullOrWhiteSpace(entry.Exception))
		{
			sb.AppendLine();
			sb.AppendLine("**Exception / Stack Trace:**");
			sb.AppendLine($"```");
			sb.AppendLine(entry.Exception);
			sb.AppendLine($"```");
		}

		sb.AppendLine();
		sb.AppendLine("What is the likely cause of this error and how can I fix it?");

		try
		{
			await this.JS.InvokeVoidAsync("navigator.clipboard.writeText", sb.ToString());
			this.Snackbar.Add("Log copied to clipboard for AI.", Severity.Success);
		}
		catch
		{
			this.Snackbar.Add("Failed to copy to clipboard.", Severity.Error);
		}
	}

	private static Color GetLevelColor(string level) =>
		level switch
		{
			"Warning" => Color.Warning,
			"Error" => Color.Error,
			"Critical" => Color.Error,
			_ => Color.Default,
		};
}

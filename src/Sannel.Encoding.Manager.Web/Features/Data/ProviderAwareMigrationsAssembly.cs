#pragma warning disable EF1001 // Internal EF Core API — intentional: subclass MigrationsAssembly to filter by provider namespace

using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Internal;

namespace Sannel.Encoding.Manager.Web.Features.Data;

/// <summary>
/// Replaces the default <see cref="IMigrationsAssembly"/> so that only migrations (and the model
/// snapshot) whose namespace matches the active database provider are discovered at runtime.
/// <list type="bullet">
///   <item>Sqlite migrations live under a namespace containing <c>.Sqlite.</c></item>
///   <item>Postgres migrations live under a namespace containing <c>.Postgres.</c></item>
/// </list>
/// Registered in DI via <c>options.ReplaceService&lt;IMigrationsAssembly, ProviderAwareMigrationsAssembly&gt;()</c>.
/// </summary>
public sealed class ProviderAwareMigrationsAssembly : MigrationsAssembly
{
	private readonly ICurrentDbContext _currentContext;
	private string? _providerFragment;
	private ModelSnapshot? _snapshot;

	public ProviderAwareMigrationsAssembly(
		ICurrentDbContext currentContext,
		IDbContextOptions options,
		IMigrationsIdGenerator idGenerator,
		IDiagnosticsLogger<DbLoggerCategory.Migrations> logger)
		: base(currentContext, options, idGenerator, logger)
	{
		this._currentContext = currentContext;
	}

	/// <inheritdoc />
	public override IReadOnlyDictionary<string, TypeInfo> Migrations
	{
		get
		{
			var fragment = this.GetProviderFragment();
			return base.Migrations
				.Where(kv => kv.Value.Namespace?.Contains(fragment, StringComparison.OrdinalIgnoreCase) == true)
				.ToDictionary(kv => kv.Key, kv => kv.Value);
		}
	}

	/// <inheritdoc />
	public override ModelSnapshot? ModelSnapshot
	{
		get
		{
			if (this._snapshot is not null)
			{
				return this._snapshot;
			}

			var fragment = this.GetProviderFragment();
			this._snapshot = typeof(AppDbContext).Assembly.GetTypes()
				.Where(t =>
					t.IsSubclassOf(typeof(ModelSnapshot))
					&& !t.IsAbstract
					&& t.Namespace?.Contains(fragment, StringComparison.OrdinalIgnoreCase) == true)
				.Select(t => (ModelSnapshot)Activator.CreateInstance(t)!)
				.FirstOrDefault();

			return this._snapshot;
		}
	}

	private string GetProviderFragment()
	{
		if (this._providerFragment is not null)
		{
			return this._providerFragment;
		}

		var providerName = this._currentContext.Context.Database.ProviderName ?? string.Empty;
		this._providerFragment = providerName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase)
			? ".Postgres"
			: ".Sqlite";

		return this._providerFragment;
	}
}

#pragma warning restore EF1001

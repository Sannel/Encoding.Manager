using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Sannel.Encoding.Manager.Web.Features.Data;

/// <summary>
/// Used exclusively by <c>dotnet ef migrations add</c> at design time.
/// </summary>
/// <remarks>
/// Set <c>DB_PROVIDER=postgres</c> to generate Postgres migrations; otherwise SQLite is used.
/// <code>
/// dotnet ef migrations add InitialCreate --output-dir Features/Data/Migrations/Sqlite
/// DB_PROVIDER=postgres dotnet ef migrations add InitialCreate --output-dir Features/Data/Migrations/Postgres
/// </code>
/// </remarks>
public sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
	public AppDbContext CreateDbContext(string[] args)
	{
		var provider = (Environment.GetEnvironmentVariable("DB_PROVIDER") ?? "sqlite")
			.ToLowerInvariant();

		var builder = new DbContextOptionsBuilder<AppDbContext>();

		if (provider is "postgres" or "postgresql")
		{
			builder.UseNpgsql(
				Environment.GetEnvironmentVariable("DB_CONNECTION")
				?? "Host=localhost;Database=encoding_manager;Username=postgres;Password=postgres");
		}
		else
		{
			builder.UseSqlite("Data Source=data/encoding.db");
		}

		// Filter migrations to only those for the active provider namespace
		builder.ReplaceService<IMigrationsAssembly, ProviderAwareMigrationsAssembly>();

		return new AppDbContext(builder.Options);
	}
}

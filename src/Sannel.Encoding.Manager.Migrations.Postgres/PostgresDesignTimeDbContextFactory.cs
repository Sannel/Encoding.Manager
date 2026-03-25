using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sannel.Encoding.Manager.Web.Features.Data;

namespace Sannel.Encoding.Manager.Migrations.Postgres;

public sealed class PostgresDesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
	public AppDbContext CreateDbContext(string[] args)
	{
		var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION")
			?? "Host=localhost;Database=encoding_manager;Username=postgres;Password=postgres";

		var options = new DbContextOptionsBuilder<AppDbContext>()
			.UseNpgsql(connectionString,
				b => b.MigrationsAssembly("Sannel.Encoding.Manager.Migrations.Postgres"))
			.Options;

		return new AppDbContext(options);
	}
}

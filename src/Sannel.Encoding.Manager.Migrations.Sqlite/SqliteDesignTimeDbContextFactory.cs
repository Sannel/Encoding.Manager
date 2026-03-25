using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sannel.Encoding.Manager.Web.Features.Data;

namespace Sannel.Encoding.Manager.Migrations.Sqlite;

public sealed class SqliteDesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
	public AppDbContext CreateDbContext(string[] args)
	{
		var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION")
			?? "Data Source=data/encoding.db";

		var options = new DbContextOptionsBuilder<AppDbContext>()
			.UseSqlite(connectionString,
				b => b.MigrationsAssembly("Sannel.Encoding.Manager.Migrations.Sqlite"))
			.Options;

		return new AppDbContext(options);
	}
}

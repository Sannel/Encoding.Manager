using Microsoft.EntityFrameworkCore;
using Sannel.Encoding.Manager.Web.Features.Data;

namespace Sannel.Encoding.Manager.Web.Features.Logging.Services;

public static class DatabaseLoggerExtensions
{
	public static ILoggingBuilder AddDBLogProvider(this ILoggingBuilder builder, string source = "Server")
	{
		builder.Services.AddSingleton<ILoggerProvider>(sp =>
			new DatabaseLoggerProvider(
				sp.GetRequiredService<IDbContextFactory<AppDbContext>>(),
				source));

		builder.AddFilter<DatabaseLoggerProvider>(level => level >= LogLevel.Warning);

		return builder;
	}
}

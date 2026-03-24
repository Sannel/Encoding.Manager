namespace Sannel.Encoding.Manager.Web.Features.Data.Options;

public class DatabaseOptions
{
	/// <summary>
	/// The database provider to use. Valid values are "sqlite" and "postgres".
	/// Defaults to "sqlite".
	/// </summary>
	public string Provider { get; set; } = "sqlite";

	/// <summary>
	/// The connection string for the selected provider.
	/// Defaults to a SQLite database at data/encoding.db (relative to the working directory).
	/// </summary>
	public string ConnectionString { get; set; } = "Data Source=data/encoding.db";
}

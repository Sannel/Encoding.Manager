namespace Sannel.Encoding.Manager.Web.Features.Data.Options;

public class DatabaseOptions
{
	/// <summary>Valid values: "sqlite", "postgres". Default: "sqlite".</summary>
	public string Provider { get; set; } = "sqlite";

	/// <summary>Default: "Data Source=data/encoding.db"</summary>
	public string ConnectionString { get; set; } = "Data Source=data/encoding.db";
}

namespace NBomberConsole.Models;

public sealed class DataSourceSettings
{
    /// <summary>
    /// Data source type: "CSV" or "Database".
    /// Omit or leave empty for no data source (static request mode).
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Path to the CSV file (relative or absolute). Required when Type is "CSV".
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Database connection string. Required when Type is "Database".
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// SQL query to execute to load test data. Required when Type is "Database".
    /// Column names from the result set become placeholder tokens in URL/headers/body.
    /// </summary>
    public string? Query { get; set; }

    /// <summary>
    /// Database provider name. Required when Type is "Database".
    /// Supported values: "SqlServer"
    /// </summary>
    public string ProviderName { get; set; } = "SqlServer";

    /// <summary>
    /// Controls how data records are selected during the test run:
    ///   "Random"   - randomly pick a record per request (default)
    ///   "Circular" - sequentially loop through records, restarting at the top
    ///   "Constant" - each scenario copy gets one fixed record
    /// </summary>
    public string FeedStrategy { get; set; } = "Random";
}

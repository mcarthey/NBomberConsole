namespace NBomberConsole.Models;

public sealed class EndpointSettings
{
    /// <summary>HTTP method: GET, POST, PUT, PATCH, DELETE</summary>
    public string HttpMethod { get; set; } = "GET";

    /// <summary>Full URL of the endpoint to test</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Optional dictionary of HTTP headers to include</summary>
    public Dictionary<string, string>? Headers { get; set; }

    /// <summary>
    /// Optional JSON body for POST/PUT/PATCH requests.
    /// Stored as a raw JSON object in config; serialized and sent as-is.
    /// </summary>
    public object? JsonBody { get; set; }

    /// <summary>Optional friendly name for the step within the scenario</summary>
    public string? StepName { get; set; }

    /// <summary>
    /// Optional expected HTTP status code for validation.
    /// Responses with different codes are marked as failures.
    /// </summary>
    public int? ExpectedStatusCode { get; set; }

    /// <summary>
    /// Optional request timeout in milliseconds.
    /// Timeouts are tagged with status code "TIMEOUT" in reports so they
    /// appear as a distinct category separate from HTTP errors.
    /// Default: 30000 (30 seconds).
    /// </summary>
    public int TimeoutMs { get; set; } = 30000;

    /// <summary>
    /// Optional data source configuration for data-driven testing.
    /// When configured, placeholder tokens like {ColumnName} in the URL, headers,
    /// and JSON body are replaced with values from the data source on each request.
    /// </summary>
    public DataSourceSettings? DataSource { get; set; }
}

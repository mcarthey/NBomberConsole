using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using NBomber.CSharp;
using NBomber.Contracts;
using NBomber.Contracts.Stats;
using NBomber.Data;
using NBomber.Data.CSharp;
using NBomber.Http;
using NBomber.Http.CSharp;
using NBomber.Plugins.Network.Ping;
using Serilog.Events;
using NBomberConsole.Models;

namespace NBomberConsole;

class Program
{
    static void Main(string[] args)
    {
        // Timestamp used for unique report folder/file names so runs never overwrite each other.
        var runTimestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

        // Shared HttpClient with high connection limit for load testing.
        // NBomber best practice: reuse a single instance across all scenarios.
        var httpClient = Http.CreateDefaultClient(maxConnectionsPerServer: 5000);

        // Dynamically discover all scenarios defined in nbomber-config.json.
        // Users add, remove, or rename scenarios entirely through the JSON config
        // -- no code changes needed.
        var scenarios = BuildScenariosFromConfig(httpClient);

        // Extract the target host from the config for PingPlugin.
        // Falls back to a sensible default if config hasn't been customized yet.
        var targetHost = GetTargetHost();

        NBomberRunner
            .RegisterScenarios(scenarios)
            .LoadConfig("nbomber-config.json")
            .LoadInfraConfig("infra-config.json")
            .WithReportFolder($"reports/{runTimestamp}")
            .WithReportFileName($"load-test-report_{runTimestamp}")
            .WithReportFormats(
                ReportFormat.Html,  // Interactive charts for leadership presentations
                ReportFormat.Csv,   // Raw data for Excel/Power BI analysis
                ReportFormat.Md,    // Markdown for wiki/documentation
                ReportFormat.Txt    // Plain text summary
            )
            .WithWorkerPlugins(
                // PingPlugin: measures raw ICMP network latency to the target host,
                // separate from HTTP latency. Helps distinguish network vs application issues.
                new PingPlugin(PingPluginConfig.CreateDefault(targetHost)),

                // HttpMetricsPlugin: monitors HTTP connection pool activity (active/idle
                // connections over time). Surfaces connection exhaustion under heavy load.
                new HttpMetricsPlugin(new[] { NBomber.Http.HttpVersion.Version1 })
            )
            .WithMinimumLogLevel(LogEventLevel.Debug)
            .Run();
    }

    /// <summary>
    /// Reads all scenario definitions from nbomber-config.json and builds a ScenarioProps
    /// for each one. This replaces the previous hardcoded scenario registration so users
    /// can define any number of scenarios purely through configuration.
    /// </summary>
    private static ScenarioProps[] BuildScenariosFromConfig(HttpClient httpClient)
    {
        var configText = File.ReadAllText("nbomber-config.json");
        var configDoc = JsonDocument.Parse(configText);

        var scenarioNames = configDoc.RootElement
            .GetProperty("GlobalSettings")
            .GetProperty("ScenariosSettings")
            .EnumerateArray()
            .Select(s => s.GetProperty("ScenarioName").GetString()!)
            .ToList();

        return scenarioNames.Select(name => BuildScenario(name, httpClient)).ToArray();
    }

    /// <summary>
    /// Builds a config-driven HTTP scenario with optional data feed support.
    /// All request details (method, URL, headers, body) are read from the CustomSettings
    /// block in nbomber-config.json during initialization.
    ///
    /// When a DataSource is configured, placeholder tokens like {ColumnName} in the URL,
    /// headers, and JSON body are replaced with values from the data feed on each request.
    /// Without a DataSource, the scenario sends identical requests (original behavior).
    /// </summary>
    private static ScenarioProps BuildScenario(string scenarioName, HttpClient httpClient)
    {
        // Settings are populated during WithInit (before any load is generated)
        // and then read on every invocation during the test.
        EndpointSettings? settings = null;
        IDataFeed<Dictionary<string, string>>? dataFeed = null;

        return Scenario.Create(scenarioName, async context =>
        {
            var cfg = settings!;

            // If a data feed is configured, get the next record for placeholder substitution.
            var dataRecord = dataFeed?.GetNextItem(context.ScenarioInfo);

            // Apply data substitutions to URL if we have a data record.
            var url = dataRecord != null ? ApplySubstitutions(cfg.Url, dataRecord) : cfg.Url;
            var stepName = cfg.StepName ?? $"{cfg.HttpMethod} {cfg.Url}";

            var step = await Step.Run(stepName, context, async () =>
            {
                // Build the HTTP request from config-driven settings
                var request = Http.CreateRequest(cfg.HttpMethod, url);

                // Apply any custom headers (auth tokens, trace IDs, etc.)
                // with optional placeholder substitution from data feed
                if (cfg.Headers is not null)
                {
                    foreach (var header in cfg.Headers)
                    {
                        var value = dataRecord != null
                            ? ApplySubstitutions(header.Value, dataRecord)
                            : header.Value;
                        request = request.WithHeader(header.Key, value);
                    }
                }

                // Attach JSON body for POST/PUT/PATCH requests
                // with optional placeholder substitution from data feed
                if (cfg.JsonBody is not null)
                {
                    var jsonString = cfg.JsonBody is JsonElement element
                        ? element.GetRawText()
                        : JsonSerializer.Serialize(cfg.JsonBody);

                    if (dataRecord != null)
                    {
                        jsonString = ApplySubstitutions(jsonString, dataRecord);
                    }

                    request = request.WithBody(
                        new StringContent(jsonString, Encoding.UTF8, "application/json")
                    );
                }

                // Execute the request with a configurable timeout.
                // Timeouts are tagged with "TIMEOUT" status code so they appear as a
                // distinct category in reports, separate from HTTP 5xx or connection errors.
                try
                {
                    // WaitAsync enforces the per-request timeout from config.
                    // If the request exceeds TimeoutMs, a TimeoutException is thrown.
                    var response = await Http.Send(httpClient, request)
                        .WaitAsync(TimeSpan.FromMilliseconds(cfg.TimeoutMs));

                    // Optional status code validation: mark unexpected codes as failures
                    if (cfg.ExpectedStatusCode.HasValue)
                    {
                        var actualCode = response.StatusCode;
                        var expectedStr = cfg.ExpectedStatusCode.Value.ToString();

                        if (actualCode != expectedStr
                            && actualCode != ((HttpStatusCode)cfg.ExpectedStatusCode.Value).ToString())
                        {
                            return Response.Fail(
                                statusCode: actualCode,
                                message: $"Expected status {cfg.ExpectedStatusCode}, got {actualCode}",
                                sizeBytes: response.SizeBytes
                            );
                        }
                    }

                    context.Logger.Debug(
                        "Scenario={Scenario} Step={Step} Invocation={Invocation} StatusCode={StatusCode}",
                        scenarioName, stepName, context.InvocationNumber, response.StatusCode);

                    return Response.Ok(
                        statusCode: response.StatusCode,
                        sizeBytes: response.SizeBytes
                    );
                }
                catch (Exception ex) when (ex is TimeoutException or TaskCanceledException or OperationCanceledException)
                {
                    context.Logger.Warning(
                        "TIMEOUT: Scenario={Scenario} Step={Step} Invocation={Invocation} TimeoutMs={TimeoutMs}",
                        scenarioName, stepName, context.InvocationNumber, cfg.TimeoutMs);

                    return Response.Fail(
                        statusCode: "TIMEOUT",
                        message: $"Request timed out after {cfg.TimeoutMs}ms"
                    );
                }
            });

            return step;
        })
        .WithInit(context =>
        {
            // Deserialize the CustomSettings section for this scenario from nbomber-config.json.
            // This is where the config-driven magic happens -- all HTTP details come from JSON.
            settings = context.CustomSettings.Get<EndpointSettings>();

            if (settings is null || string.IsNullOrWhiteSpace(settings.Url))
            {
                throw new InvalidOperationException(
                    $"Scenario '{scenarioName}' has no URL configured in CustomSettings. " +
                    "Check nbomber-config.json.");
            }

            // Load data source if configured. The data feed provides records that are
            // used for placeholder substitution in URL, headers, and body.
            if (settings.DataSource is { } ds && !string.IsNullOrWhiteSpace(ds.Type))
            {
                var data = LoadData(ds, scenarioName);

                dataFeed = CreateDataFeed(ds.FeedStrategy, data);

                context.Logger.Information(
                    "Loaded {Count} data records for scenario '{Scenario}' from {Type} source (strategy: {Strategy})",
                    data.Count, scenarioName, ds.Type, ds.FeedStrategy);
            }

            context.Logger.Information(
                "Initialized scenario '{Scenario}': {Method} {Url}",
                scenarioName, settings.HttpMethod, settings.Url);

            return Task.CompletedTask;
        })
        .WithClean(context =>
        {
            context.Logger.Information("Completed scenario '{Scenario}'", scenarioName);
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// Loads data from the configured source (CSV file or database).
    /// </summary>
    private static List<Dictionary<string, string>> LoadData(
        DataSourceSettings ds, string scenarioName)
    {
        return ds.Type.ToLowerInvariant() switch
        {
            "csv" => LoadCsvData(ds, scenarioName),
            "database" => LoadDatabaseData(ds, scenarioName),
            _ => throw new InvalidOperationException(
                $"Scenario '{scenarioName}' has unsupported DataSource Type '{ds.Type}'. " +
                "Supported types: CSV, Database")
        };
    }

    private static List<Dictionary<string, string>> LoadCsvData(
        DataSourceSettings ds, string scenarioName)
    {
        if (string.IsNullOrWhiteSpace(ds.FilePath))
            throw new InvalidOperationException(
                $"Scenario '{scenarioName}' has DataSource Type 'CSV' but no FilePath specified.");

        return DataLoader.LoadFromCsv(ds.FilePath);
    }

    private static List<Dictionary<string, string>> LoadDatabaseData(
        DataSourceSettings ds, string scenarioName)
    {
        if (string.IsNullOrWhiteSpace(ds.ConnectionString))
            throw new InvalidOperationException(
                $"Scenario '{scenarioName}' has DataSource Type 'Database' but no ConnectionString specified.");
        if (string.IsNullOrWhiteSpace(ds.Query))
            throw new InvalidOperationException(
                $"Scenario '{scenarioName}' has DataSource Type 'Database' but no Query specified.");

        return DataLoader.LoadFromDatabase(ds.ProviderName, ds.ConnectionString, ds.Query);
    }

    /// <summary>
    /// Creates the appropriate data feed based on the configured strategy.
    /// </summary>
    private static IDataFeed<Dictionary<string, string>> CreateDataFeed(
        string strategy, List<Dictionary<string, string>> data)
    {
        return strategy.ToLowerInvariant() switch
        {
            "random" => DataFeed.Random(data),
            "circular" => DataFeed.Circular(data),
            "constant" => DataFeed.Constant(data),
            _ => throw new InvalidOperationException(
                $"Unsupported FeedStrategy '{strategy}'. " +
                "Supported strategies: Random, Circular, Constant")
        };
    }

    /// <summary>
    /// Replaces {ColumnName} placeholder tokens in a template string with values
    /// from a data record. Matching is case-insensitive.
    /// </summary>
    private static string ApplySubstitutions(string template, Dictionary<string, string> data)
    {
        var result = template;
        foreach (var kvp in data)
        {
            result = result.Replace($"{{{kvp.Key}}}", kvp.Value, StringComparison.OrdinalIgnoreCase);
        }
        return result;
    }

    /// <summary>
    /// Reads the first scenario's URL from nbomber-config.json to extract the hostname
    /// for PingPlugin. This keeps the ping target in sync with whatever endpoint teams configure.
    /// </summary>
    private static string GetTargetHost()
    {
        try
        {
            var configText = File.ReadAllText("nbomber-config.json");
            var doc = JsonDocument.Parse(configText);
            var scenarios = doc.RootElement
                .GetProperty("GlobalSettings")
                .GetProperty("ScenariosSettings");

            foreach (var scenario in scenarios.EnumerateArray())
            {
                if (scenario.TryGetProperty("CustomSettings", out var custom)
                    && custom.TryGetProperty("Url", out var url))
                {
                    var uri = new Uri(url.GetString()!);
                    return uri.Host;
                }
            }
        }
        catch
        {
            // Fall through to default
        }

        return "localhost";
    }
}

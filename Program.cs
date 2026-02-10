using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using NBomber.CSharp;
using NBomber.Contracts;
using NBomber.Contracts.Stats;
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

        // Build scenarios from config. Each scenario name must match a
        // "ScenarioName" entry in nbomber-config.json where its CustomSettings
        // define the HTTP request details (method, URL, headers, body).
        var getScenario = BuildScenario("get_scenario", httpClient);
        var postScenario = BuildScenario("post_scenario", httpClient);

        // Extract the target host from the config for PingPlugin.
        // Falls back to a sensible default if config hasn't been customized yet.
        var targetHost = GetTargetHost();

        NBomberRunner
            .RegisterScenarios(getScenario, postScenario)
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
    /// Builds a config-driven HTTP scenario. All request details (method, URL, headers, body)
    /// are read from the CustomSettings block in nbomber-config.json during initialization.
    /// Teams only need to edit the JSON config -- not this code.
    /// </summary>
    private static ScenarioProps BuildScenario(string scenarioName, HttpClient httpClient)
    {
        // Settings are populated during WithInit (before any load is generated)
        // and then read on every invocation during the test.
        EndpointSettings? settings = null;

        return Scenario.Create(scenarioName, async context =>
        {
            var cfg = settings!;
            var stepName = cfg.StepName ?? $"{cfg.HttpMethod} {cfg.Url}";

            var step = await Step.Run(stepName, context, async () =>
            {
                // Build the HTTP request from config-driven settings
                var request = Http.CreateRequest(cfg.HttpMethod, cfg.Url);

                // Apply any custom headers (auth tokens, trace IDs, etc.)
                if (cfg.Headers is not null)
                {
                    foreach (var header in cfg.Headers)
                    {
                        request = request.WithHeader(header.Key, header.Value);
                    }
                }

                // Attach JSON body for POST/PUT/PATCH requests
                if (cfg.JsonBody is not null)
                {
                    var jsonString = cfg.JsonBody is JsonElement element
                        ? element.GetRawText()
                        : JsonSerializer.Serialize(cfg.JsonBody);

                    request = request.WithBody(
                        new StringContent(jsonString, Encoding.UTF8, "application/json")
                    );
                }

                var response = await Http.Send(httpClient, request);

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

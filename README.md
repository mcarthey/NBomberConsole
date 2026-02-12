# NBomber HTTP Load Testing Template

Enterprise-grade HTTP load testing template built on [NBomber 6.x](https://nbomber.com/) for .NET 8.0. Designed so teams can configure and run load tests by editing a single JSON file.

## Quick Start

```bash
dotnet run
```

Reports are generated in `reports/{timestamp}/` with HTML, CSV, Markdown, and TXT formats.

## How It Works

`Program.cs` is a generic HTTP load test engine. It reads all request details from `nbomber-config.json` so teams never need to modify C# code for standard HTTP testing.

Each scenario in the config has a `CustomSettings` block that defines:

| Field | Description | Example |
|-------|-------------|---------|
| `HttpMethod` | HTTP verb | `"GET"`, `"POST"`, `"PUT"` |
| `Url` | Full endpoint URL | `"https://api.example.com/v1/users"` |
| `Headers` | Optional request headers | `{ "Authorization": "Bearer token" }` |
| `JsonBody` | Optional JSON body (POST/PUT) | `{ "name": "test" }` |
| `StepName` | Optional friendly name for reports | `"create_user"` |
| `ExpectedStatusCode` | Optional validation | `200`, `201` |
| `TimeoutMs` | Request timeout in milliseconds (default: 30000) | `5000`, `60000` |
| `DataSource` | Optional data source for data-driven testing (see below) | `{ "Type": "CSV", ... }` |

### Data-Driven Testing

Use `DataSource` in `CustomSettings` to drive requests with dynamic data from CSV files or databases. Placeholder tokens like `{ColumnName}` in the URL, headers, and JSON body are replaced with values from the data source on each request.

#### CSV Data Source

Create a CSV file (e.g., `test-data.csv`) with a header row. Column names become placeholder tokens:

```csv
PostId,Title,UserId
1,First Post,1
2,Second Post,2
3,Third Post,1
```

Configure the scenario to use it:

```json
"CustomSettings": {
  "HttpMethod": "GET",
  "Url": "https://api.example.com/posts/{PostId}",
  "ExpectedStatusCode": 200,
  "DataSource": {
    "Type": "CSV",
    "FilePath": "test-data.csv",
    "FeedStrategy": "Random"
  }
}
```

Each request will substitute `{PostId}` with a value from the CSV based on the feed strategy.

#### Database Data Source

Query a database to load test data. Column names from the result set become placeholder tokens:

```json
"CustomSettings": {
  "HttpMethod": "GET",
  "Url": "https://api.example.com/users/{UserId}",
  "Headers": {
    "X-Tenant": "{TenantId}"
  },
  "ExpectedStatusCode": 200,
  "DataSource": {
    "Type": "Database",
    "ProviderName": "SqlServer",
    "ConnectionString": "Server=localhost;Database=TestDb;Trusted_Connection=true;TrustServerCertificate=true;",
    "Query": "SELECT TOP 100 UserId, TenantId FROM Users WHERE IsActive = 1",
    "FeedStrategy": "Circular"
  }
}
```

#### Feed Strategies

| Strategy | Behavior |
|----------|----------|
| `Random` | Randomly picks a record per request (default) |
| `Circular` | Loops sequentially through records, restarting at the top |
| `Constant` | Each scenario copy (virtual user) gets one fixed record for the entire test |

#### Placeholders

Placeholders use `{ColumnName}` syntax and can appear in:
- **URL** — `"https://api.example.com/posts/{PostId}"`
- **Headers** — `"Authorization": "Bearer {Token}"`
- **JSON body** — `{ "userId": "{UserId}", "title": "{Title}" }`

Matching is case-insensitive. If a placeholder has no matching column, it is left as-is.

#### No Data Source (Static Requests)

If `DataSource` is omitted, the scenario sends identical requests using the literal URL, headers, and body from the config — the original behavior.

## Configuration Guide

### Changing the Target Endpoint

Edit `nbomber-config.json` and update the `CustomSettings` for each scenario:

```json
"CustomSettings": {
  "HttpMethod": "GET",
  "Url": "https://your-api.com/endpoint",
  "Headers": {
    "Authorization": "Bearer your-token",
    "Accept": "application/json"
  },
  "ExpectedStatusCode": 200
}
```

### Adjusting Load Profile

Modify `LoadSimulationsSettings` for each scenario. Simulations are chained in order — combine them to create any traffic pattern.

#### Available Load Simulations

**Open-model simulations** (control request rate — best for HTTP APIs):

| Simulation | JSON Format | Description |
|-----------|-------------|-------------|
| `Inject` | `[rate, interval, duration]` | Constant request rate for a duration |
| `RampingInject` | `[rate, interval, duration]` | Linear ramp to target rate over duration |
| `InjectRandom` | `[minRate, maxRate, interval, duration]` | Random rate within a range — simulates unpredictable traffic |
| `IterationsForInject` | `[rate, interval, maxIterations]` | Constant rate until iteration count is reached |

**Closed-model simulations** (control concurrent users — best for persistent connections):

| Simulation | JSON Format | Description |
|-----------|-------------|-------------|
| `KeepConstant` | `[copies, duration]` | Fixed number of concurrent virtual users |
| `RampingConstant` | `[copies, duration]` | Linear ramp of concurrent virtual users |
| `IterationsForConstant` | `[copies, maxIterations]` | Fixed users until iteration count is reached |

**Control simulation:**

| Simulation | JSON Format | Description |
|-----------|-------------|-------------|
| `Pause` | `"duration"` | Zero traffic — idle period between phases |

#### Basic Example

```json
"LoadSimulationsSettings": [
  { "RampingInject": [ 50, "00:00:01", "00:00:30" ] },
  { "Inject": [ 100, "00:00:01", "00:01:00" ] }
]
```

#### Production Target: 40,000+ Requests/Hour

```json
"LoadSimulationsSettings": [
  { "RampingInject": [ 12, "00:00:01", "00:05:00" ] },
  { "Inject": [ 12, "00:00:01", "00:55:00" ] }
]
```
(12 req/sec * 3,600 sec = 43,200 requests/hour)

#### Stress Test: Emergency Mode Surge Pattern

Simulates a system that runs idle, then experiences sudden emergency-level traffic spikes to identify the breaking point:

```json
"LoadSimulationsSettings": [
  { "Inject": [ 2, "00:00:01", "00:05:00" ] },
  { "RampingInject": [ 200, "00:00:01", "00:00:30" ] },
  { "Inject": [ 200, "00:00:01", "00:02:00" ] },
  { "RampingInject": [ 2, "00:00:01", "00:00:30" ] },
  { "Pause": "00:01:00" },
  { "InjectRandom": [ 5, 100, "00:00:01", "00:03:00" ] },
  { "Pause": "00:00:30" },
  { "RampingInject": [ 500, "00:00:01", "00:02:00" ] },
  { "Inject": [ 500, "00:00:01", "00:05:00" ] }
]
```

This chains the following phases:
1. **Idle** — 2 req/s for 5 minutes (normal baseline)
2. **Spike** — ramp to 200 req/s over 30 seconds
3. **Sustained spike** — hold 200 req/s for 2 minutes
4. **Recovery** — ramp back down to 2 req/s
5. **Silence** — complete pause for 1 minute
6. **Random chaos** — unpredictable 5-100 req/s for 3 minutes
7. **Brief pause** — 30 seconds
8. **Extreme surge** — ramp to 500 req/s over 2 minutes
9. **Sustained extreme** — hold 500 req/s for 5 minutes (find the breaking point)

The HTML report will show exactly where latency degrades and failures begin.

### Scenario Execution Patterns

All registered scenarios run **concurrently by default**. Use `Pause` and `TargetScenarios` to control execution order.

#### Concurrent (default)

Both scenarios start and run at the same time:

```json
"TargetScenarios": [ "get_scenario", "post_scenario" ]
```

#### Staggered

Use `Pause` at the start of a scenario's load simulation to delay it. This is useful for simulating realistic traffic patterns where different API calls don't all spike at the same instant:

```json
{
  "ScenarioName": "get_scenario",
  "LoadSimulationsSettings": [
    { "RampingInject": [ 50, "00:00:01", "00:00:30" ] },
    { "Inject": [ 100, "00:00:01", "00:05:00" ] }
  ]
},
{
  "ScenarioName": "post_scenario",
  "LoadSimulationsSettings": [
    { "Pause": "00:02:00" },
    { "RampingInject": [ 20, "00:00:01", "00:00:30" ] },
    { "Inject": [ 50, "00:00:01", "00:05:00" ] }
  ]
}
```

In this example, `get_scenario` starts immediately while `post_scenario` waits 2 minutes before beginning.

#### Serial

Chain pauses so one scenario finishes before the next begins:

```json
{
  "ScenarioName": "get_scenario",
  "LoadSimulationsSettings": [
    { "RampingInject": [ 50, "00:00:01", "00:01:00" ] },
    { "Inject": [ 100, "00:00:01", "00:05:00" ] }
  ]
},
{
  "ScenarioName": "post_scenario",
  "LoadSimulationsSettings": [
    { "Pause": "00:06:00" },
    { "RampingInject": [ 20, "00:00:01", "00:01:00" ] },
    { "Inject": [ 50, "00:00:01", "00:05:00" ] }
  ]
}
```

Here `post_scenario` pauses for 6 minutes (the total duration of `get_scenario`), effectively running them back-to-back.

#### Run Only Specific Scenarios

Use `TargetScenarios` to selectively enable or disable scenarios without removing their config:

```json
"TargetScenarios": [ "get_scenario" ]
```

The `post_scenario` config stays in the file but won't execute. This is handy when teams want to test one endpoint at a time or toggle scenarios on/off between runs.

### Thresholds

Thresholds define pass/fail criteria that are evaluated during and after the test. Breached thresholds are flagged in reports and can optionally abort the test early.

```json
"ThresholdSettings": [
  { "OkRequest": "RPS >= 30" },
  { "OkRequest": "Percent > 90" },
  { "OkLatency": "p95 < 1000" },
  { "StatusCode": [ "500", "Percent < 5" ] }
]
```

#### Threshold Types

| Type | Description | Equivalent To |
|------|-------------|---------------|
| `OkRequest` | Assertions on successful requests | `stats.Ok.Request` |
| `FailRequest` | Assertions on failed requests | `stats.Fail.Request` |
| `OkLatency` | Assertions on response time for OK requests | `stats.Ok.Latency` |
| `OkDataTransfer` | Assertions on payload size for OK requests | `stats.Ok.DataTransfer` |
| `StatusCode` | Assertions on a specific HTTP status code | `stats.Ok.StatusCode` / `stats.Fail.StatusCode` |

#### Available Metrics

All threshold types share the same metric names. Not all metrics apply to every type — use the ones that make sense for the category.

| Metric | Description | Typical Use |
|--------|-------------|-------------|
| `RPS` | Requests per second | `OkRequest`, `FailRequest` |
| `Percent` | Percentage of total | `OkRequest`, `FailRequest`, `StatusCode` |
| `Min` | Minimum value | `OkLatency` (ms), `OkDataTransfer` (bytes) |
| `Mean` | Average value | `OkLatency` (ms), `OkDataTransfer` (bytes) |
| `Max` | Maximum value | `OkLatency` (ms), `OkDataTransfer` (bytes) |
| `P50` | 50th percentile (median) | `OkLatency` (ms), `OkDataTransfer` (bytes) |
| `P75` | 75th percentile | `OkLatency` (ms), `OkDataTransfer` (bytes) |
| `P95` | 95th percentile | `OkLatency` (ms), `OkDataTransfer` (bytes) |
| `P99` | 99th percentile | `OkLatency` (ms), `OkDataTransfer` (bytes) |

> **Source:** [NBomber Asserts & Thresholds](https://nbomber.com/docs/nbomber/asserts_and_thresholds/)

#### Examples by Type

**Request thresholds** (`OkRequest`, `FailRequest`):
```
{ "OkRequest": "RPS >= 30" }
{ "OkRequest": "Percent > 90" }
{ "FailRequest": "Percent < 10" }
```

**Latency thresholds** (`OkLatency`) — values in milliseconds:
```
{ "OkLatency": "Max < 2000" }
{ "OkLatency": "P75 < 500" }
{ "OkLatency": "P95 < 1000" }
{ "OkLatency": "P99 < 1500" }
```

**Data transfer thresholds** (`OkDataTransfer`) — values in bytes:
```
{ "OkDataTransfer": "Min > 0" }
{ "OkDataTransfer": "P75 < 200" }
{ "OkDataTransfer": "P95 < 5000" }
```

**Status code thresholds** (`StatusCode`) — first element is the HTTP status code, second is the assertion:
```
{ "StatusCode": [ "500", "Percent < 5" ] }
{ "StatusCode": [ "200", "Percent >= 90" ] }
```

#### Comparison Operators

All thresholds support: `>`, `>=`, `<`, `<=`, `=`

#### Threshold Options

Each threshold entry can also include:

| Option | Description | Example |
|--------|-------------|---------|
| `AbortWhenErrorCount` | Stop the test if this threshold fails N times | `"AbortWhenErrorCount": 10` |
| `StartCheckAfter` | Delay threshold checks (skip warmup noise) | `"StartCheckAfter": "00:00:15"` |

> **Note on ramp-up failures:** Thresholds are checked at every `ReportingInterval` (default 10s) **during** the test, not just at the end. If a scenario uses `RampingInject`, RPS will be low during the initial ramp and may fail RPS thresholds in the first few intervals. Use `StartCheckAfter` to skip checks during warmup/ramp periods. In the included config, `post_scenario` intentionally omits `StartCheckAfter` on its RPS threshold so the ramp-up failures are visible in reports — this is useful for understanding threshold timing. To suppress these, add `"StartCheckAfter": "00:00:35"` (warmup + ramp duration).

#### Full Threshold Example

```json
"ThresholdSettings": [
  { "OkRequest": "RPS >= 30" },
  { "OkRequest": "Percent > 90" },
  { "FailRequest": "Percent < 10" },
  { "OkLatency": "max < 2000" },
  { "OkLatency": "p75 < 500" },
  { "OkLatency": "p95 < 1000" },
  { "OkLatency": "p99 < 1500" },
  { "OkDataTransfer": "p75 < 200" },
  {
    "StatusCode": [ "500", "Percent < 5" ]
  },
  {
    "StatusCode": [ "200", "Percent >= 90" ],
    "AbortWhenErrorCount": 10,
    "StartCheckAfter": "00:00:15"
  }
]
```

### Adding a New Scenario

Scenarios are auto-discovered from `nbomber-config.json` — no C# code changes required.

1. Add a new config block under `ScenariosSettings` in `nbomber-config.json`
2. Add the scenario name to the `TargetScenarios` array

> **Tip:** Copy the `stress_test_scenario` block from the config as a starting template — it demonstrates all available options including data sources, thresholds, and load patterns.

### Running a Subset of Scenarios

Control which scenarios execute via the `TargetScenarios` array in config. For example, to run only GET tests:

```json
"TargetScenarios": [ "get_scenario" ]
```

## Multi-Step Scenarios

The config engine handles single-step HTTP scenarios (one request per iteration). For workflows that chain multiple HTTP calls — such as authenticating before each request, or creating a resource then reading it back — you'll need to write a custom scenario in C#.

NBomber provides three mechanisms for sharing state between steps:

| Mechanism | Scope | Use Case |
|-----------|-------|----------|
| `context.Data` | Single iteration (cleared after each) | Pass resource ID from step 1 to step 2 |
| `context.ScenarioInstanceData` | All iterations for one virtual user | Cache an auth token across requests |
| `Step.Run()` | Per step | Each step gets independent metrics in reports |

### Example 1: Auth Token Flow

POST for a token once, cache it in `ScenarioInstanceData`, then use it on every subsequent GET. The token persists across iterations so you aren't re-authenticating on every request.

```csharp
// In Program.cs — add alongside the config-driven scenarios
var authScenario = Scenario.Create("auth_api_scenario", async context =>
{
    var httpClient = Http.CreateDefaultClient();

    // Step 1: Get auth token (only if not cached or expired)
    if (!context.ScenarioInstanceData.ContainsKey("auth_token"))
    {
        var authStep = await Step.Run("get_auth_token", context, async () =>
        {
            var request = Http.CreateRequest("POST", "https://auth.example.com/token")
                .WithBody(new StringContent(
                    """{"client_id": "my-app", "client_secret": "secret"}""",
                    Encoding.UTF8, "application/json"));

            var response = await Http.Send(httpClient, request);
            var body = await response.Payload.Value.Content.ReadAsStringAsync();
            var json = JsonDocument.Parse(body);
            var token = json.RootElement.GetProperty("access_token").GetString()!;

            // Cache the token for subsequent iterations
            context.ScenarioInstanceData["auth_token"] = token;

            return Response.Ok(statusCode: response.StatusCode, sizeBytes: response.SizeBytes);
        });

        if (authStep.IsError) return authStep;
    }

    // Step 2: Call the protected API with the cached token
    var token = (string)context.ScenarioInstanceData["auth_token"];
    var apiStep = await Step.Run("get_protected_data", context, async () =>
    {
        var request = Http.CreateRequest("GET", "https://api.example.com/data")
            .WithHeader("Authorization", $"Bearer {token}");

        var response = await Http.Send(httpClient, request);
        return Response.Ok(statusCode: response.StatusCode, sizeBytes: response.SizeBytes);
    });

    return apiStep;
});

// Register alongside config-driven scenarios
NBomberRunner
    .RegisterScenarios(scenarios.Append(authScenario).ToArray())
    // ... rest of runner config
```

### Example 2: CRUD Chain (Create → Read)

POST to create a resource, extract the ID from the response, then GET to read it back. Uses `context.Data` which resets each iteration, so every cycle creates a new resource.

```csharp
var crudScenario = Scenario.Create("crud_chain_scenario", async context =>
{
    var httpClient = Http.CreateDefaultClient();

    // Step 1: Create a resource
    var createStep = await Step.Run("create_post", context, async () =>
    {
        var request = Http.CreateRequest("POST", "https://jsonplaceholder.typicode.com/posts")
            .WithHeader("Accept", "application/json")
            .WithBody(new StringContent(
                """{"title": "Load Test Post", "body": "Created by NBomber", "userId": 1}""",
                Encoding.UTF8, "application/json"));

        var response = await Http.Send(httpClient, request);
        var body = await response.Payload.Value.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);
        var postId = json.RootElement.GetProperty("id").GetInt32();

        // Store the created ID for the next step (cleared each iteration)
        context.Data["post_id"] = postId;

        return Response.Ok(statusCode: response.StatusCode, sizeBytes: response.SizeBytes);
    });

    if (createStep.IsError) return createStep;

    // Step 2: Read back the created resource
    var postId = (int)context.Data["post_id"];
    var readStep = await Step.Run("read_post", context, async () =>
    {
        var request = Http.CreateRequest("GET", $"https://jsonplaceholder.typicode.com/posts/{postId}")
            .WithHeader("Accept", "application/json");

        var response = await Http.Send(httpClient, request);
        return Response.Ok(statusCode: response.StatusCode, sizeBytes: response.SizeBytes);
    });

    return readStep;
});
```

### Registering Custom Scenarios

Custom multi-step scenarios coexist with config-driven ones. Add them to the `RegisterScenarios` call and configure their load profile in `nbomber-config.json` like any other scenario:

```json
{
  "ScenarioName": "auth_api_scenario",
  "WarmUpDuration": "00:00:05",
  "LoadSimulationsSettings": [
    { "Inject": [ 10, "00:00:01", "00:01:00" ] }
  ]
}
```

Each `Step.Run()` appears as a separate row in the HTML report with its own RPS, latency percentiles, and success/fail counts — so you can see exactly which step is the bottleneck.

> **When to use config-driven vs. code:** If the scenario is a single HTTP request (even with data feeds, headers, and auth tokens passed as static config values), use the config engine. If the scenario requires reading a response body and using values from it in a subsequent request, write a custom scenario in C#.

## Plugins

Two worker plugins are included for additional metrics:

- **PingPlugin** — ICMP ping to the target host during the test. Captures baseline network latency separate from HTTP latency.
- **HttpMetricsPlugin** — Monitors HTTP connection pool activity (active/idle connections). Helps identify connection exhaustion under heavy load.

## Reports

Each run generates a timestamped folder under `reports/` containing:

| Format | Use Case |
|--------|----------|
| HTML | Interactive charts for presentations and leadership review |
| CSV | Raw metric data for Excel or Power BI analysis |
| Markdown | Paste into wikis, PRs, or documentation |
| TXT | Quick plain-text summary |

## Metrics Captured

| Category | Metrics |
|----------|---------|
| Latency | min, mean, max, stddev, p50, p75, p95, p99 |
| Throughput | RPS, total OK/FAIL requests, success % |
| Data Transfer | Total MB, min/mean/max KB per request |
| Status Codes | Distribution with percentages |
| HTTP Connections | Active/idle connections over time (HttpMetricsPlugin) |
| Network Latency | ICMP ping round-trip time (PingPlugin) |
| Thresholds | Pass/fail status for each configured threshold |

## Logs

Structured Serilog logs are written to `logs/` with daily rolling files. Configure logging behavior in `infra-config.json`.

## Project Structure

```
NBomberConsole/
  Program.cs              - Load test engine (rarely needs changes)
  Models/
    EndpointSettings.cs   - Config model for CustomSettings deserialization
  nbomber-config.json     - Scenario config: endpoints, load, thresholds (edit this)
  infra-config.json       - Serilog logging configuration
  reports/                - Generated test reports (gitignored)
  logs/                   - Generated log files (gitignored)
```

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

Modify `LoadSimulationsSettings` for each scenario:

- **RampingInject**: Gradually increase request rate — `[rate, interval, duration]`
- **Inject**: Sustain a constant request rate — `[rate, interval, duration]`

```json
"LoadSimulationsSettings": [
  { "RampingInject": [ 50, "00:00:01", "00:00:30" ] },
  { "Inject": [ 100, "00:00:01", "00:01:00" ] }
]
```

For the 40,000+ requests/hour production target:
```json
{ "RampingInject": [ 12, "00:00:01", "00:05:00" ] },
{ "Inject": [ 12, "00:00:01", "00:55:00" ] }
```
(12 req/sec * 3,600 sec = 43,200 requests/hour)

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

| Type | Description | Applies To |
|------|-------------|------------|
| `OkRequest` | Assertions on successful requests | Request count, RPS, % of total |
| `FailRequest` | Assertions on failed requests | Request count, RPS, % of total |
| `OkLatency` | Assertions on response time for OK requests | Percentiles and min/mean/max (ms) |
| `FailLatency` | Assertions on response time for failed requests | Percentiles and min/mean/max (ms) |
| `OkDataTransfer` | Assertions on payload size for OK requests | Percentiles and min/mean/max (bytes) |
| `FailDataTransfer` | Assertions on payload size for failed requests | Percentiles and min/mean/max (bytes) |
| `StatusCode` | Assertions on a specific HTTP status code | `["code", "metric operator value"]` |

#### Available Metrics per Type

**Request metrics** (`OkRequest`, `FailRequest`):

| Metric | Description | Example |
|--------|-------------|---------|
| `RPS` | Requests per second | `"RPS >= 30"` |
| `Percent` | Percentage of total requests | `"Percent > 90"` |
| `Count` | Total request count | `"Count >= 1000"` |

**Latency metrics** (`OkLatency`, `FailLatency`) — all values in milliseconds:

| Metric | Description | Example |
|--------|-------------|---------|
| `min` | Minimum response time | `"min < 50"` |
| `mean` | Average response time | `"mean < 200"` |
| `max` | Maximum response time | `"max < 3000"` |
| `p50` | 50th percentile (median) | `"p50 < 100"` |
| `p75` | 75th percentile | `"p75 < 300"` |
| `p95` | 95th percentile | `"p95 < 1000"` |
| `p99` | 99th percentile | `"p99 < 2000"` |
| `stddev` | Standard deviation | `"stddev < 150"` |

**Data transfer metrics** (`OkDataTransfer`, `FailDataTransfer`) — all values in bytes:

| Metric | Description | Example |
|--------|-------------|---------|
| `min` | Minimum payload size | `"min > 0"` |
| `mean` | Average payload size | `"mean < 5000"` |
| `max` | Maximum payload size | `"max < 50000"` |
| `p50` | 50th percentile | `"p50 < 1000"` |
| `p75` | 75th percentile | `"p75 < 200"` |
| `p95` | 95th percentile | `"p95 < 5000"` |
| `p99` | 99th percentile | `"p99 < 10000"` |
| `AllBytes` | Total bytes transferred | `"AllBytes >= 1000000"` |

**Status code metrics** (`StatusCode`) — first element is the HTTP status code, second is the assertion:

| Metric | Description | Example |
|--------|-------------|---------|
| `Percent` | Percentage of total requests with this code | `["500", "Percent < 5"]` |
| `Count` | Total number of responses with this code | `["200", "Count >= 100"]` |

#### Comparison Operators

All thresholds support: `>`, `>=`, `<`, `<=`, `=`

#### Threshold Options

Each threshold entry can also include:

| Option | Description | Example |
|--------|-------------|---------|
| `AbortWhenErrorCount` | Stop the test if this threshold fails N times | `"AbortWhenErrorCount": 10` |
| `StartCheckAfter` | Delay threshold checks (skip warmup noise) | `"StartCheckAfter": "00:00:15"` |

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

1. In `Program.cs`, add one line:
   ```csharp
   var myScenario = BuildScenario("my_scenario", httpClient);
   ```
2. Register it in the `RegisterScenarios` call:
   ```csharp
   .RegisterScenarios(getScenario, postScenario, myScenario)
   ```
3. Add the matching config block in `nbomber-config.json` under `ScenariosSettings`
4. Add `"my_scenario"` to the `TargetScenarios` array

### Running a Subset of Scenarios

Control which scenarios execute via the `TargetScenarios` array in config. For example, to run only GET tests:

```json
"TargetScenarios": [ "get_scenario" ]
```

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

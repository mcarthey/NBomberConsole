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

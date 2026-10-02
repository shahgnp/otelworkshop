# Observability code guide: starter and Lab 2 solution

This guide explains the observability-related code, not the full API implementation. Source links point to the relevant starting line.

## 1. How the versions relate

| Version | Application instrumentation | How you observe it |
|---|---|---|
| `starter` | Native .NET `ILogger` logs and `System.Diagnostics.Metrics` instruments | Container stdout and `dotnet-counters` |
| `lab2-solution` | The same application instrumentation, plus OpenTelemetry collection and automatic instrumentation | Container stdout and logs/metrics exported over OTLP to the Collector |

The starter is used in Lab 1. The identical initial Lab 1 solution is the working folder participants edit during Lab 2. The Lab 2 solution contains the completed OpenTelemetry setup.

The important distinction is **recording signals versus collecting and exporting them**. The application already records logs and metric measurements in the starter; Lab 2 adds an SDK that listens to those signals and sends them elsewhere.

## 2. Starter: registering telemetry

Source: [starter application setup](../app/starter/Program.cs#L1).

```csharp
using System.Diagnostics;
using Workshop.Orders;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<OrderTelemetry>();
```

| Line | Explanation |
|---|---|
| `using System.Diagnostics;` | Makes `Stopwatch` available to measure elapsed processing time. This import does not enable OpenTelemetry or tracing. |
| `using Workshop.Orders;` | Makes the application's `OrderTelemetry` class available. |
| `WebApplication.CreateBuilder(args)` | Creates the ASP.NET Core builder with default logging and configuration, including console logging. |
| `AddSingleton<OrderTelemetry>()` | Registers one telemetry object per application process. Requests share its meter and instruments instead of creating them for each request. It does not register an exporter. |

## 3. Starter: structured logging

Source: [order endpoint](../app/starter/Program.cs#L15).

```csharp
logger.LogInformation("Order {OrderId} placed for {Customer}", orderId, request.Customer);
```

- `ILogger<Program>` is injected into the endpoint. `Program` identifies the log category.
- `LogInformation` records a normal application event at Information severity.
- `{OrderId}` and `{Customer}` are named template properties. The logger receives their values as structured state, not just text substituted into a string.
- A simple console formatter renders a readable message. A JSON formatter also exposes the structured state for machine processing.
- A log template preserves property names; interpolating a string before calling the logger would lose that explicit template structure.

Other log calls make the API's behavior visible:

| Source | Log call | What it signals |
|---|---|---|
| [Health endpoint](../app/starter/Program.cs#L9) | `LogInformation("Health check requested")` | A request reached `/`. Docker's periodic health probe generates these events too. |
| [Order validation](../app/starter/Program.cs#L17) | `LogWarning(...)` with `Customer` | An invalid order request, followed by HTTP 400. |
| [Order lookup](../app/starter/Program.cs#L32) | Warning with `OrderId`, or Information with `OrderId` | A missing order produces HTTP 404; other lookups produce HTTP 200. |
| [Slow endpoint](../app/starter/Program.cs#L44) | `LogWarning("Slow endpoint requested")` | The endpoint deliberately waits 1.2 seconds. The log reports the event, not its measured duration. |
| [Error endpoint](../app/starter/Program.cs#L51) | `LogError("Deliberate error endpoint requested")` | A deliberate HTTP 500 response. This is not an exception being thrown. |

Logs explain individual events. Metrics summarize behavior across many events. Order IDs can be useful in logs, but should not become metric labels.

## 4. Starter: measuring order processing

Source: [timing and recording](../app/starter/Program.cs#L24).

```csharp
var stopwatch = Stopwatch.StartNew();
await Task.Delay(Random.Shared.Next(50, 301));
stopwatch.Stop();
telemetry.RecordOrder(request.PaymentMethod, stopwatch.Elapsed.TotalMilliseconds);
```

| Line | Explanation |
|---|---|
| `Stopwatch.StartNew()` | Starts measuring elapsed time. |
| `Task.Delay(...)` | Simulates asynchronous processing between 50 and 300 milliseconds. |
| `stopwatch.Stop()` | Ends the measured interval. |
| `RecordOrder(...)` | Records an accepted order and the elapsed duration, labeled by payment method. |

This measures the simulated processing block, not the whole HTTP request or CPU time. It runs only after validation succeeds. It does not measure `/slow`, failed validation, or order lookups.

## 5. Native custom metrics: shared by both versions

Sources: [starter metrics](../app/starter/OrderTelemetry.cs#L1) and [Lab 2 metrics](../app/lab2-solution/OrderTelemetry.cs#L1). Their executable metric code is the same.

```csharp
using System.Diagnostics.Metrics;

public const string MeterName = "Workshop.Orders";
private readonly Meter meter = new(MeterName);
private readonly Counter<long> ordersPlaced;
private readonly Histogram<double> orderProcessingDuration;
```

| Line | Explanation |
|---|---|
| `using System.Diagnostics.Metrics;` | Uses .NET's native metrics API, not an OpenTelemetry-specific application API. |
| `MeterName` | Defines the instrumentation scope name. Listeners use this name to subscribe. It is different from the service name. |
| `new Meter(MeterName)` | Creates the scope that owns the custom instruments. It does not send telemetry over the network. |
| `Counter<long>` | Holds an instrument for recording integer increments. |
| `Histogram<double>` | Holds an instrument for recording a distribution of floating-point measurements. |

The constructor creates and describes the instruments:

```csharp
ordersPlaced = meter.CreateCounter<long>("orders.placed", unit: "{order}", description: "Orders accepted by the API.");
orderProcessingDuration = meter.CreateHistogram<double>("orders.processing.duration", unit: "ms", description: "Time spent accepting an order.");
```

- `orders.placed` counts accepted orders. `{order}` describes the thing counted; it is not an attribute placeholder.
- `orders.processing.duration` records processing times in milliseconds. A listener such as OpenTelemetry can aggregate these into histogram buckets, counts, and sums rather than retaining every individual measurement.
- `description` documents each instrument for consumers; it does not change the recorded values.

The recording method emits measurements:

```csharp
ordersPlaced.Add(1, new KeyValuePair<string, object?>("payment.method", paymentMethod));
orderProcessingDuration.Record(durationMilliseconds, new KeyValuePair<string, object?>("payment.method", paymentMethod));
```

- `Add(1, ...)` records one more accepted order.
- `Record(durationMilliseconds, ...)` records one processing-time sample.
- `payment.method` groups measurements, for example by `card` versus `cash`.
- Every distinct attribute combination can create another metric series. Keep payment methods to a small controlled set. The current API only checks for nonempty input; it does not enforce an allowlist.
- Do not label these metrics with unique order IDs or customer names: that would create excessive cardinality and potentially expose personal data.

The native instruments do not provide a historical database. A listener must subscribe to collect observations. Restarting the app resets process-local instrumentation and aggregation state; it does not automatically delete previously captured Docker logs or telemetry retained externally.

## 6. Logging configuration: shared by both versions

Sources: [starter logging configuration](../app/starter/appsettings.json) and [Lab 2 logging configuration](../app/lab2-solution/appsettings.json).

```json
"LogLevel": {
  "Default": "Information",
  "Microsoft.AspNetCore": "Warning"
},
"Console": { "FormatterName": "simple" }
```

- `Default: Information` allows Information, Warning, Error, and Critical messages for categories without a more specific rule. Debug and Trace messages are filtered out.
- `Microsoft.AspNetCore: Warning` reduces framework log noise while retaining warnings and errors.
- `FormatterName: simple` selects readable console output. The Compose environment variable `Logging__Console__FormatterName` overrides this setting; `LOG_FORMATTER=json` selects JSON output.
- JSON formatting changes console representation. It does not enable OpenTelemetry export.

## 7. Lab 2: OpenTelemetry imports

Source: [Lab 2 application setup](../app/lab2-solution/Program.cs#L1).

```csharp
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
```

These namespaces expose the extension methods for logging providers, metric collection, and resource metadata. Imports alone do not activate anything; the following registration calls do.

## 8. Lab 2: collecting and exporting logs

Source: [OpenTelemetry logging registration](../app/lab2-solution/Program.cs#L9).

```csharp
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.ParseStateValues = true;
    logging.AddOtlpExporter();
});
```

| Line | Explanation |
|---|---|
| `AddOpenTelemetry(...)` | Adds an OpenTelemetry logging provider that receives the existing `ILogger` events. The default console provider remains enabled. |
| `IncludeFormattedMessage = true` | Includes the rendered, human-readable message in the exported log record. |
| `ParseStateValues = true` | Parses log state so template properties such as `OrderId` and `Customer` are available as structured attributes. |
| `AddOtlpExporter()` | Exports logs using OpenTelemetry Protocol (OTLP). The deployment supplies the destination through environment configuration. |

The endpoint log calls do not need to be rewritten. A successful order now produces both normal application console output and an exported log record.

## 9. Lab 2: collecting and exporting metrics

Source: [OpenTelemetry metric registration](../app/lab2-solution/Program.cs#L16).

```csharp
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("workshop-orders"))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter(OrderTelemetry.MeterName)
        .AddOtlpExporter());
```

| Line | Explanation |
|---|---|
| `AddOpenTelemetry()` | Registers OpenTelemetry services with dependency injection and application lifecycle management. |
| `ConfigureResource(...)` | Describes the application producing telemetry. Resource attributes identify the producer, rather than an individual order. |
| `AddService("workshop-orders")` | Sets `service.name` to identify this service. The SDK also generates service instance identity by default, which helps distinguish replicas. |
| `WithMetrics(...)` | Configures metric collection. This does not create a tracing pipeline. |
| `AddAspNetCoreInstrumentation()` | Enables collection of incoming HTTP server metrics, such as request duration. Unlike the custom order histogram, this covers requests across endpoints. |
| `AddHttpClientInstrumentation()` | Enables collection of outgoing `HttpClient` metrics. These endpoints do not make outgoing HTTP calls, so this may have no relevant data here. The separate healthcheck process is not instrumented by this app's SDK. |
| `AddRuntimeInstrumentation()` | Collects runtime measurements such as garbage collection, allocation, and threading metrics. |
| `AddMeter(OrderTelemetry.MeterName)` | Subscribes to `Workshop.Orders`, connecting the SDK to the custom counter and histogram. Omitting it leaves those custom instruments outside this metrics pipeline. |
| `AddOtlpExporter()` | Adds periodic metric export over OTLP, including the collected runtime, HTTP, and custom metrics. |

`AddSingleton<OrderTelemetry>()` still appears below this setup. It creates the application instruments; `AddMeter(...)` tells OpenTelemetry to listen to them. Both roles are needed.

## 10. Lab 2: packages and deployment configuration

Source: [Lab 2 package references](../app/lab2-solution/Workshop.Orders.csproj#L8).

| Package | Purpose |
|---|---|
| `OpenTelemetry.Extensions.Hosting` | Integrates the SDK with the .NET host, dependency injection, startup, and shutdown. |
| `OpenTelemetry.Exporter.Console` | Supports the console-exporter exercise in Lab 2. The final solution references it but does not call `AddConsoleExporter()`. |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` | Provides `AddOtlpExporter()` for exporting signals to the Collector. |
| `OpenTelemetry.Instrumentation.AspNetCore` | Provides ASP.NET Core instrumentation. |
| `OpenTelemetry.Instrumentation.Http` | Provides outgoing HTTP client instrumentation. |
| `OpenTelemetry.Instrumentation.Runtime` | Provides .NET runtime metric instrumentation. |

The [starter project](../app/starter/Workshop.Orders.csproj) needs no OpenTelemetry packages for its native `ILogger`, `Meter`, counter, and histogram APIs.

The [Compose app environment](../docker-compose.yml#L22) supplies:

| Setting | Meaning |
|---|---|
| `OTEL_SERVICE_NAME=workshop-orders` | Declares the intended service name through deployment configuration; the code explicitly sets the same name with `AddService`. |
| `OTEL_EXPORTER_OTLP_ENDPOINT=http://collector:4317` | Sends telemetry to the Collector by its Docker network service name. Port 4317 is the OTLP gRPC port used by the default .NET exporter configuration. |
| `OTEL_METRIC_EXPORT_INTERVAL=5000` | Sets the periodic metric export interval to 5,000 milliseconds by default. This is not a per-request send interval and does not set the log export interval. |

## 11. End-to-end flow and limits

```text
Starter:
  ILogger events -> console provider -> stdout -> Docker logs
  native Meter measurements -> attached dotnet-counters listener

Lab 2 solution:
  ILogger events -> console provider -> stdout -> Docker logs
                 -> OpenTelemetry log provider -> OTLP -> Collector
  native Meter + HTTP + runtime metrics -> OpenTelemetry -> OTLP -> Collector
  Collector -> batch processor -> debug exporter -> Collector stdout
```

Source: [Collector pipelines](../collector/config.yaml#L26).

- The Collector receives logs and metrics in separate pipelines and batches them before printing detailed output.
- There is no monitoring database or dashboard in this workshop. Exporting to this Collector is not durable historical storage by itself.
- There is no `WithTracing(...)` registration or Collector traces pipeline. Logs can still include trace/span IDs from ASP.NET Core request activities; that does not mean traces are exported.
- The HTTP health probe establishes that `/` responds. It does not verify that the OpenTelemetry destination is working.

## 12. Observe the difference

Run these commands from the workshop root. Stop log following with `Ctrl+C`.

For the starter:

```bash
./scripts/lab1-up.sh
./scripts/load-docker.sh
./scripts/logs-app.sh
```

Run `./scripts/counters.sh` in a separate terminal, then generate load again to inspect live runtime and custom metrics.

For the completed Lab 2 solution:

```bash
./scripts/reset.sh
./scripts/lab2-up.sh
./scripts/load-docker.sh
./scripts/logs-collector.sh
```

Expect custom order metrics, HTTP/runtime metrics, and structured order/error logs in Collector output. No tracing output is expected.
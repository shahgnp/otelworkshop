---
marp: true
theme: default
paginate: true
style: |
  section { font-size: 28px; }
  pre { font-size: 20px; }
---

# Observability in .NET
## Native telemetry and OpenTelemetry basics

**Hands-on workshop | .NET 8**

<!-- Notes: Time 1 min. Key points: establish the audience and that this is an applied introduction. Question: Who has used ILogger or dotnet-counters before? -->

---

## Today: make signals useful

| Time | Topic |
|---|---|
| 0:00 | Fundamentals, native metrics and logs |
| 0:30 | Lab 1: native metrics and logs |
| 0:55 | OpenTelemetry concepts and SDK demo |
| 1:20 | Lab 2: SDK to Collector |
| 1:50 | Recap and next session |

Outcomes: read structured logs, create/observe metrics, export logs and metrics to a Collector.

<!-- Notes: Time 1 min. Key points: state that no backend and no tracing implementation are included today. Question: What would make this workshop useful in your current service? -->

---

## It is 2 a.m.

Orders are timing out. A customer says they were charged. The API is still running.

- What changed?
- How often does it happen?
- Which customers or routes are affected?
- Can we join the clues?

Observability is the ability to understand a system from the signals it produces.

<!-- Notes: Time 3 min. Key points: start from operational questions rather than tools. Question: Which question would you ask first? -->

---

## Three complementary signals

| Signal | Best at answering |
|---|---|
| Logs | What happened in this event? |
| Metrics | How much, how often, how long? |
| Traces | Which path did this request take? **Next session** |

We implement logs and metrics only today.

<!-- Notes: Time 3 min. Key points: signals are complementary, not competitors. Question: Which signal do you already have most of? -->

---

## The anchor pipeline

ASCII diagram used throughout:

```text
[MEASURE] -> [aggregate] -> [export] -> [store / visualize]
```

Native .NET gives us strong measuring tools. The rest of the pipeline decides whether they are useful at 2 a.m.

<!-- Notes: Time 2 min. Key points: introduce one memorable model. Question: Where does your current application pipeline stop? -->

---

## `ILogger`: events with context

```csharp
logger.LogInformation(
    "Order {OrderId} placed for {Customer}",
    orderId, request.Customer);
logger.LogWarning("Order {OrderId} was not found", id);
logger.LogError("Deliberate error endpoint requested");
```

Levels communicate urgency; properties carry context.

<!-- Notes: Time 3 min. Key points: ILogger is already an OpenTelemetry-compatible log API. Question: Which context do you routinely wish were in a log? -->

---

## Templates, not interpolation

```csharp
// Good: named, structured properties
logger.LogInformation("Order {OrderId}", orderId);

// Text only: formatting happens before logging
logger.LogInformation($"Order {orderId}");
```

Prefer templates for stable, searchable properties. Do not put secrets in either form.

<!-- Notes: Time 3 min. Key points: the rendered text may look similar but data shape differs. Question: What could a backend filter in the first version? -->

---

## Native metrics: `System.Diagnostics.Metrics`

```csharp
var meter = new Meter("Workshop.Orders");
var orders = meter.CreateCounter<long>("orders.placed");
var duration = meter.CreateHistogram<double>(
    "orders.processing.duration", unit: "ms");

orders.Add(1, new("payment.method", "card"));
duration.Record(142);
```

Counter = total occurrences. Histogram = distribution of measurements.

<!-- Notes: Time 4 min. Key points: a Meter owns instruments; measurements may have bounded attributes. Question: Why use a histogram for duration? -->

---

## Useful signals already exist

- The runtime: GC, allocations, CPU, thread pool
- ASP.NET Core: active requests and server request duration
- Kestrel/networking: connections and transport activity
- Your Meter: business behavior such as placed orders

You do not need to hand-write every useful metric.

<!-- Notes: Time 3 min. Key points: distinguish built-in instrument sources from custom business signals. Question: What business count matters in your service? -->

---

## Native demo: watch the container

```bash
./scripts/lab1-up.sh
./scripts/counters.sh
./scripts/load-docker.sh
docker compose logs -f app
```

<!-- Notes: Time 4 min. Key points: show stdout logs, attach the tools container, and run load. Question: Which values move when the POSTs begin? -->

---

## Native pipeline: the missing stages

```text
[MEASURE] -> [AGGREGATE locally] -> [export?] -> [store / visualize?]
```

- Containers write logs to stdout; the platform collects the stream
- Process restart removes local observations
- With two instances, local output cannot answer which instance was slow
- Nobody is retaining or joining signals

<!-- Notes: Time 2 min. Key points: highlight the local aggregate stage and missing handoff. Question: What happens if nobody is watching right now? -->

---

## Lab 1: native metrics and logs

1. Run the starter container and load container
2. Read app stdout with `docker compose logs`
3. Switch JSON logs through an environment variable
4. Attach `dotnet-counters` through the tools container
5. Restart and scale to two instances

Handout: `labs/lab1.md` | **25 minutes**

<!-- Notes: Time 1 min. Key points: explain recovery folder and pairs. Question: Who needs help finding the starter project? -->

---

## Lab 1 checkpoint

Discuss:

- Why is `payment.method` safe but `OrderId` unsafe on a metric?
- When would a log tell you more than a metric?
- What did the restart prove?

```text
local observer != exported, retained telemetry
```

<!-- Notes: Time 1 min. Key points: surface the limitations explicitly before introducing a tool. Question: What limitation bothered you most? -->

---

## OpenTelemetry: common plumbing

OpenTelemetry is a vendor-neutral framework and protocol for producing, processing, and exporting telemetry.

- Standard signal data model
- SDKs for many languages
- OTLP transport protocol
- Collector as a deployable pipeline component

It does not require a particular monitoring backend.

<!-- Notes: Time 3 min. Key points: distinguish standardization from storage. Question: Why could a standard handoff reduce application coupling? -->

---

## API, SDK, exporter

```text
measure -> aggregate -> [EXPORT] -> store / visualize
```

- API: application creates logs or measurements
- SDK: subscribes, enriches, aggregates
- Exporter: sends telemetry somewhere

In .NET, `ILogger` and `Meter` are the familiar API surface.

<!-- Notes: Time 3 min. Key points: API is not the SDK; this workshop keeps application code native where possible. Question: Which box owns aggregation? -->

---

## OTLP and the Collector

```text
app -> OTLP/gRPC :4317 -> Collector -> debug console
```

The Collector receives, processes, and exports telemetry. Today its last hop is deliberately a console, not a backend.

<!-- Notes: Time 2 min. Key points: explain 4317 vs 4318 HTTP before the lab. Question: Where could policy-based enrichment live? -->

---

## Resource: identify the producer

```csharp
.ConfigureResource(resource =>
    resource.AddService("workshop-orders"))
```

Resources describe the process emitting every signal:

- `service.name`
- deployment environment
- service version

<!-- Notes: Time 2 min. Key points: resource is shared context, not an event property. Question: What stable identifier should every service have? -->

---

## SDK packages

```xml
<PackageReference Include="OpenTelemetry.Extensions.Hosting"
                  Version="1.19.1" />
<PackageReference Include="OpenTelemetry.Exporter.Console"
                  Version="1.19.1" />
<PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol"
                  Version="1.19.1" />
```

Add instrumentation packages for ASP.NET Core, HTTP client, and runtime.

<!-- Notes: Time 2 min. Key points: all versions pinned; packages add SDK and integrations, not a replacement logger API. Question: Which package represents a destination? -->

---

## SDK wiring

```csharp
builder.Services.AddOpenTelemetry()
  .ConfigureResource(r => r.AddService("workshop-orders"))
  .WithMetrics(m => m.AddAspNetCoreInstrumentation()
    .AddRuntimeInstrumentation()
    .AddMeter("Workshop.Orders")
    .AddOtlpExporter());
```

Logging uses `builder.Logging.AddOpenTelemetry(...)` with state parsing enabled.

<!-- Notes: Time 3 min. Key points: exact Meter name is a subscription; do not introduce tracing. Question: What happens if AddMeter is omitted? -->

---

## Demo: prove then hand off

1. Replace OTLP exporter with console exporter
2. Generate a request; read local telemetry output
3. Start Collector and restore OTLP exporter
4. Set `OTEL_EXPORTER_OTLP_ENDPOINT=http://collector:4317`
5. Read the same signals in Collector output

<!-- Notes: Time 3 min. Key points: use console exporter as a narrow diagnostic, then Collector abstraction. Question: What changed in application business code? -->

---

## Lab 2: SDK to Collector

1. Edit packages and console exporter on the host
2. Rebuild the container image; observe app stdout
3. Send OTLP across the Compose network to Collector
4. Find fields and `service.instance.id` in debug output
5. Enrich signals in the Collector
6. Diagnose a missing Meter and bad `localhost` endpoint

Handout: `labs/lab2.md` | **30 minutes**

<!-- Notes: Time 1 min. Key points: point out the recovery solution and time-box. Question: Is everyone starting in lab1-solution? -->

---

## Collector config: name each stage

```yaml
receivers: { otlp: { protocols: { grpc: {}, http: {} } } }
processors: { batch: {} }
exporters: { debug: { verbosity: detailed } }
service:
  pipelines:
    metrics: { receivers: [otlp], processors: [batch], exporters: [debug] }
    logs:    { receivers: [otlp], processors: [batch], exporters: [debug] }
```

No traces pipeline is configured.

<!-- Notes: Time 2 min. Key points: map config words to pipeline diagram; learners will use full commented file. Question: Which signals have pipelines? -->

---

## Real Collector output: read it

```text
Resource attributes:
     -> service.name: Str(workshop-orders)
Descriptor:
     -> Name: http.server.request.duration
     -> Unit: s
Data point attributes:
     -> http.response.status_code: Int(201)
     -> http.route: Str(/orders)
ExplicitBounds #6: 0.250000
```

This is verified output from the pinned SDK and Collector versions.

<!-- Notes: Time 2 min. Key points: output varies but names/types are observed. Question: Which fields identify the request outcome? -->

---

## Lab 2 troubleshooting

| Symptom | Check |
|---|---|
| No Collector data | `collector:4317`, not `localhost:4317`? |
| Custom metrics absent | Exact `AddMeter("Workshop.Orders")`? |
| No `service.name` | `ConfigureResource(...AddService...)`? |
| Output delayed | `OTEL_METRIC_EXPORT_INTERVAL=5000`? |

The deliberate failure is removing `AddMeter` and observing silent loss.

<!-- Notes: Time 1 min. Key points: normalize silent custom metric loss as an expected diagnostic. Question: Which check would you do first? -->

---

## Before and after the pipeline

```text
Before: measure -> aggregate -> export? -> [STORE / VISUALIZE?]
After:  measure -> SDK aggregate -> OTLP -> [COLLECTOR DEBUG]
```

The Collector is the replaceable boundary: next session, a backend takes the final position.

<!-- Notes: Time 3 min. Key points: reuse pipeline and highlight export/store stages. Question: What stayed native after adopting OTel? -->

---

## Three takeaways

1. .NET already emits useful logs and metrics; add focused business instruments.
2. Use structured properties and bounded metric attributes.
3. OpenTelemetry turns local signals into a standard pipeline without tying code to a backend.

Next session: traces, context propagation, and linking traces to logs. If trace/span IDs appear on log records then, that is how logs link to traces next session.

<!-- Notes: Time 3 min. Key points: close on continuity, not product selection. Question: What will you instrument first in your own service? -->
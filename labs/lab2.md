# Lab 2: SDK to Collector in containers

## Goal and time budget

Build the OpenTelemetry SDK into the container image, prove collection on app stdout, then send logs and metrics across the Compose network to the Collector. **Time: 30 minutes.**

## Prerequisites checklist

- Docker Desktop or Docker Engine is running: `docker info`.
- Compose v2 works: `docker compose version`.
- Ports 8080, 4317, and 4318 are free: run `./scripts/reset.sh`, then `docker compose ps`.
- Images are ready: run `./scripts/prepull.sh` before the session.
- Open this workshop folder in a text editor. No .NET SDK is required.

Start with `app/lab1-solution`. The Dockerfile compiles that host-edited folder when `LAB=lab1-solution` is set.

## Docker cheat sheet

| Task | Command |
|---|---|
| Check containers | `docker compose ps` |
| Follow app stdout | `docker compose logs -f app` |
| Follow Collector output | `docker compose logs -f collector` |
| Rebuild edited source | `docker compose up --build -d app` |
| Run a command in a container | `docker compose exec app <command>` |
| Restart only Collector | `docker compose restart collector` |
| Stop the stack | `docker compose down` |

## Steps

1. Add the OpenTelemetry packages on the host. Replace `app/lab1-solution/Workshop.Orders.csproj` with this complete file, then save it. Package references are pasted rather than installed with `dotnet add` because Docker performs restore during the image build.

   ```xml
   <Project Sdk="Microsoft.NET.Sdk.Web">
     <PropertyGroup>
       <TargetFramework>net8.0</TargetFramework>
       <Nullable>enable</Nullable>
       <ImplicitUsings>enable</ImplicitUsings>
     </PropertyGroup>
     <ItemGroup>
       <PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.19.1" />
       <PackageReference Include="OpenTelemetry.Exporter.Console" Version="1.19.1" />
       <PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.19.1" />
       <PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.19.0" />
       <PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.19.0" />
       <PackageReference Include="OpenTelemetry.Instrumentation.Runtime" Version="1.19.0" />
     </ItemGroup>
   </Project>
   ```

   **Expected result:** the next Docker build restores these pinned packages. Pinning keeps every participant on the same compatible SDK version.

   > **Observe**: `ILogger` and `Meter` remain the application APIs. These package references add SDK collection, instrumentation, and destinations around them.

2. Configure Step A, the console exporter. In `app/lab1-solution/Program.cs`, add these using directives after `using System.Diagnostics;`:

   ```csharp
   using OpenTelemetry.Logs;
   using OpenTelemetry.Metrics;
   using OpenTelemetry.Resources;
   ```

   Immediately after `var builder = WebApplication.CreateBuilder(args);`, add:

   ```csharp
   builder.Logging.AddOpenTelemetry(logging =>
   {
       logging.IncludeFormattedMessage = true;
       logging.ParseStateValues = true;
       logging.AddConsoleExporter();
   });
   builder.Services.AddOpenTelemetry()
       .ConfigureResource(resource => resource.AddService("workshop-orders"))
       .WithMetrics(metrics => metrics.AddAspNetCoreInstrumentation()
           .AddHttpClientInstrumentation().AddRuntimeInstrumentation()
           .AddMeter(OrderTelemetry.MeterName).AddConsoleExporter());
   ```

   Build and run the edited source. This is the one-command edit-run loop:

   ```bash
   LAB=lab1-solution docker compose --profile collector up --build -d app collector
   ```

   **Expected result:** a warm source rebuild takes about 15 seconds. The app becomes healthy at `http://localhost:8080/`.

   > **Observe**: `service.name` is resource data: it describes every signal from this application. `AddMeter` is the explicit subscription to the custom native Meter.

3. Generate load and watch the console exporter in the app container’s own stdout.

   ```bash
   ./scripts/load-docker.sh
   ```
   ```
   ./scripts/logs-app.sh
   ```

   Stop following with `Ctrl+C`.

   **Expected result:** stdout contains OpenTelemetry console exporter output as well as normal application logs.

   > **Observe**: A console exporter proves SDK collection, but its destination is still the same container stdout stream. It is useful for diagnosis, not a shared telemetry pipeline.

4. Switch to Step B, OTLP to the Collector. In `Program.cs`, replace each `AddConsoleExporter()` call with `AddOtlpExporter()`, save, then run the same one-command rebuild from step 2.

   Prove Compose DNS from the SDK-based tools container. The small runtime image has no DNS utility, so this is the equivalent of `docker compose exec app`:

   ```bash
   docker compose --profile tools run --rm --no-deps --entrypoint /bin/bash tools -lc 'getent hosts collector'
   ```

   **Expected result:** `collector` resolves to a Compose-network address. The app’s environment already sets `OTEL_EXPORTER_OTLP_ENDPOINT=http://collector:4317` and `OTEL_METRIC_EXPORT_INTERVAL=5000` in `docker-compose.yml`.

   > **Observe**: Containers use a service name, not `localhost`, to reach another container. The endpoint and interval live outside the image as environment configuration, a 12-Factor configuration practice.

5. Generate load and complete the Collector scavenger hunt.

   ```bash
   ./scripts/load-docker.sh
   ./scripts/logs-collector.sh
   ```

   Stop following with `Ctrl+C`. This is verified Collector output; values vary:

   ```text
   Resource attributes:
        -> service.name: Str(workshop-orders)
        -> service.instance.id: Str(e311a176-0f49-4f8d-87ca-331f5dd33bb8)
   Descriptor:
        -> Name: orders.placed
   Data point attributes:
        -> payment.method: Str(card)
   SeverityText: Error
   Attributes:
        -> OrderId: Str(c7be16ea)
   ```

   Find `service.name`; `orders.placed` and `payment.method`; histogram buckets for `http.server.request.duration`; a log with `OrderId`; and the error severity for `/error`.

   **Expected result:** all five items appear after a successful POST, error, and one 5-second metric interval.

   > **Observe**: `http.server.request.duration` is a histogram in seconds. `service.instance.id` is supplied by the pinned SDK and identifies this running process. Trace/span IDs can also appear on logs because ASP.NET Core creates request activities; this is how logs link to traces next session, even though no traces pipeline exists here.

6. Answer Lab 1’s two-instance question. Scale the app, wait for both health checks to run, then follow Collector output again.

   ```bash
   LAB=lab1-solution docker compose --profile collector up -d --scale app=2
   ./scripts/logs-collector.sh
   ```

   **Expected result:** each app process emits a distinct `service.instance.id`; the health checks alone create records from both replicas.

   > **Observe**: Now the Collector gives the answer that native stdout/counters could not: a common signal stream retains which instance emitted each record. `service.name` groups a service; `service.instance.id` separates its running instances.

7. Step D: enrich every signal in the Collector. In `collector/config.yaml`, uncomment and use this processor:

   ```yaml
  resource/workshop:
    attributes:
      - key: deployment.environment
        value: workshop
        action: upsert
   ```

   Change both pipeline processor lines to:

   ```yaml
      processors: [resource/workshop, batch]
   ```

   Restart only the Collector, then generate load.

   ```bash
   docker compose restart collector
   ./scripts/load-docker.sh
   ```

   **Expected result:** `deployment.environment: Str(workshop)` appears in resource attributes for metrics and logs.

   > **Observe**: A Collector processor enriches every signal without rebuilding the application image. This is useful when the platform owns deployment metadata.

8. Step E: make the custom Meter silently disappear. Temporarily remove `.AddMeter(OrderTelemetry.MeterName)` from the metrics chain in `Program.cs`, then rebuild with the command from step 2 and run load.

   **Expected result:** built-in runtime and ASP.NET Core metrics remain, but `orders.placed` and `orders.processing.duration` disappear from Collector output. Restore exactly:

   ```csharp
   .AddMeter(OrderTelemetry.MeterName)
   ```

   > **Observe**: A custom Meter is not exported merely because the SDK exists. It requires an exact subscription; this common failure is silent.
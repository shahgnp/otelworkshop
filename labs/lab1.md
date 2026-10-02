# Lab 1: Native metrics and logs in containers

## Goal and time budget

Run native .NET logs and metrics inside containers, then see why per-instance observation is not enough. **Time: 25 minutes.**

## Prerequisites checklist

- Docker is running: `docker info`.
- Compose v2 works: `docker compose version`.
- Ports 8080, 4317, and 4318 are free: run `./scripts/reset.sh` then run `docker compose ps`.
- Images are ready: run `./scripts/prepull.sh` before the session.
- Open this workshop folder in a text editor. No .NET SDK is required.

## Docker cheat sheet

| Task | Command |
|---|---|
| Check containers | `docker compose ps` |
| Follow app stdout | `docker compose logs -f app` |
| Rebuild after an edit | `docker compose up --build -d app` |
| Restart one service | `docker compose restart app` |
| Stop the stack | `docker compose down` |

## Steps

1. Start the starter app.

   ```bash
   ./scripts/lab1-up.sh
   ```

   **Expected result:** `docker compose ps` reports `app` as `healthy`; browse to `http://localhost:8080/`.

   > **Observe**: The application is one minimal API container. There is no database, identity provider, or telemetry backend to hide the signals you will inspect.

2. Generate requests with the Compose load container.

   ```bash
   ./scripts/load-docker.sh
   ```

   **Expected result:** repeated `201`, `200`, `404`, `200`, and `500` status lines appear in that order.

   > **Observe**: The load container calls `app` by Compose service name. It includes normal orders, a missing order, slow requests, and errors.

3. Read the application stdout.

   ```bash
   ./scripts/logs-app.sh
   ```

   Stop following with `Ctrl+C`. Find real output like:

   ```text
   app-1  | info: Program[0]
   app-1  |       Order d8b6d5fa placed for Ada
   app-1  | fail: Program[0]
   app-1  |       Deliberate error endpoint requested
   ```

   **Expected result:** information, warning, and error messages appear under an app container prefix.

   > **Observe**: Containers write logs to stdout and the platform collects that stream. This is the 12-Factor idea of logs as event streams: write events, not application log files. Template properties in `Order {OrderId} placed for {Customer}` carry useful structure.

4. Switch to JSON logs without editing code, then generate load again.

   ```bash
   ./scripts/lab1-down.sh
   ```
   ```bash
   LOG_FORMATTER=json ./scripts/lab1-up.sh
    ```
   ```bash
   ./scripts/load-docker.sh
   ```
    ```bash
       ./scripts/logs-app.sh
    ```
   **Expected result:** `docker compose logs app` contains one JSON object per record, including `State` values for `{OriginalFormat}`, `OrderId`, and `Customer`.

   > **Observe**: `OrderId` is a separate state value, not only rendered text. `Logging__Console__FormatterName` comes from environment configuration, so one image can emit simple or JSON logs without a source edit.

5. Monitor native metrics through the helper container. Keep this terminal open, then run step 2 in another terminal.

   ```bash
   ./scripts/counters.sh
   ```

   **Expected result:** a refreshing table appears. Verified output before load:

   ```text
   [System.Runtime]
       Allocation Rate (B / 1 sec)       8,200
       CPU Usage (%)                      0.036
       GC Heap Size (MB)                  2.937
   ```

   > **Observe**: allocation rate is managed allocation pressure; CPU usage is process work; GC heap size is managed memory retained now. `Workshop.Orders` shows `orders.placed` and `orders.processing.duration`. `payment.method=card` is bounded; order IDs and customers are not valid metric attributes.

6. Stop counters with `Ctrl+C`, restart only the app, start counters again, and do not run load yet.

   ```bash
   docker compose restart app
   ```
   ```bash
   ./scripts/counters.sh
   ```

   **Expected result:** old counter and histogram observations are gone.

   > **Observe**: Restarting a container creates a new process and discards local measurement state. Native instrumentation is useful, but nobody retained the observations outside this process.

7. Stop counters and scale to two app instances.

   ```bash
   LOG_FORMATTER=json docker compose up -d --scale app=2
   ```
   ```
   docker compose ps
   ```
   ```
   docker compose logs --tail=30 app
   ```

   **Expected result:** `app-1` and `app-2` run independently, mapped to 8080 and 8081 in the validated setup.

   > **Observe**: Each instance has separate stdout and in-memory counters. Which instance is slow? Which one produced this error? You cannot tell from here.
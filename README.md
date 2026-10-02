# Observability in .NET: native telemetry and OpenTelemetry basics

This is a two-hour, hands-on workshop for .NET developers who are new to observability. It targets **.NET 8 (LTS)**. It intentionally uses no monitoring backend: an OpenTelemetry Collector prints metrics and logs to its console with the `debug` exporter.

## Quick start (Docker)

Prerequisite: Docker Desktop or Docker Engine with Compose v2. Verify it with `docker compose version`. No .NET SDK, curl, or PowerShell is required for this path.

```bash
./scripts/lab1-up.sh
./scripts/load-docker.sh
./scripts/counters.sh
```

Open `http://localhost:8080/`; follow application output with `./scripts/logs-app.sh` . Use JSON logs in Lab 1 without editing configuration: `LOG_FORMATTER=json ./scripts/lab1-up.sh`.

For Lab 2, start the solution app and Collector, generate load, then observe the Collector:

```bash
./scripts/lab2-up.sh
./scripts/load-docker.sh
./scripts/logs-collector.sh
```

Run `./scripts/reset.sh` between a clean Lab 1 and Lab 2 start.

## Contents

```
workshop/
  README.md
  slides/slides.md
  labs/lab1.md
  labs/lab2.md
  app/starter/
  app/lab1-solution/
  app/lab2-solution/
  collector/config.yaml
  collector/run.sh
  scripts/load.sh
  instructor/guide.md
```
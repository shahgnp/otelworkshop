#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
docker run --rm --name otel-workshop-collector \
  -p 4317:4317 -p 4318:4318 \
  -v "$(pwd)/config.yaml:/etc/otelcol/config.yaml:ro" \
  otel/opentelemetry-collector:0.111.0 \
  --config=/etc/otelcol/config.yaml
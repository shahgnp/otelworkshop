#!/usr/bin/env bash
set -euo pipefail

docker compose pull collector load diagnostics-init
for lab in starter lab1-solution lab2-solution; do
  LAB="$lab" docker compose build app
done
docker compose build tools
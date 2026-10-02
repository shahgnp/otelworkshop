#!/usr/bin/env bash
set -euo pipefail
docker compose --profile collector --profile tools --profile load down --volumes --remove-orphans
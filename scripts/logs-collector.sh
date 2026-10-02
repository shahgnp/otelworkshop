#!/usr/bin/env bash
set -euo pipefail
docker compose --profile collector logs --follow collector
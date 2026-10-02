#!/usr/bin/env bash
set -euo pipefail
LAB=starter docker compose --profile tools run --rm tools monitor --process-id 1 System.Runtime Microsoft.AspNetCore.Hosting Workshop.Orders
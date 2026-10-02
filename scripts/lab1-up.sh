#!/usr/bin/env bash
set -euo pipefail
LAB=starter LOG_FORMATTER="${LOG_FORMATTER:-simple}" docker compose up --build -d app
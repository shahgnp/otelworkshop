#!/usr/bin/env bash
set -euo pipefail
LAB=lab2-solution docker compose --profile collector up --build -d app collector
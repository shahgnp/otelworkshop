#!/usr/bin/env bash
set -euo pipefail
base_url="${1:-http://localhost:5000}"

for index in $(seq 1 8); do
  curl -sS -o /dev/null -w "POST /orders -> %{http_code}\n" -X POST "$base_url/orders" -H 'Content-Type: application/json' -d "{\"customer\":\"Ada-$index\",\"paymentMethod\":\"card\"}"
  curl -sS -o /dev/null -w "GET /orders/$index -> %{http_code}\n" "$base_url/orders/$index"
  curl -sS -o /dev/null -w "GET /orders/missing -> %{http_code}\n" "$base_url/orders/missing"
  curl -sS -o /dev/null -w "GET /slow -> %{http_code}\n" "$base_url/slow"
  curl -sS -o /dev/null -w "GET /error -> %{http_code}\n" "$base_url/error"
done
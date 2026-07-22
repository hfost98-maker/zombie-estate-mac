#!/bin/bash
# Wrapper — canonical launcher is ../run-mac.sh at repo root
exec "$(cd "$(dirname "$0")/.." && pwd)/run-mac.sh" "$@"

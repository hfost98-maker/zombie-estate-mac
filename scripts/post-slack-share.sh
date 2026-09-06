#!/bin/bash
# Post the Zombie Estate Mac share message to a Slack channel via Incoming Webhook.
set -euo pipefail

WEBHOOK="${1:-${SLACK_WEBHOOK_URL:-}}"
if [ -z "$WEBHOOK" ]; then
  echo "Usage: $0 'https://hooks.slack.com/services/...'"
  echo "   or: SLACK_WEBHOOK_URL=... $0"
  exit 1
fi

python3 << 'PY' "$WEBHOOK"
import json, sys, urllib.request

webhook = sys.argv[1]
text = """*Zombie Estate — Mac port (v1.0 app)*

We got the original Xbox Live Indie Game running natively on Apple Silicon Mac via Mono + FNA (no VM, no Wine).

*Repo (scripts + launcher):* https://github.com/hfost98-maker/zombie-estate-mac

*Requirements (install once):*
• Mono 6.12+ — https://www.mono-project.com/download/

*Quick start:*
```
git clone https://github.com/hfost98-maker/zombie-estate-mac.git
cd zombie-estate-mac
chmod +x run-mac.sh scripts/*.sh
./scripts/setup-fnlibs.sh
./scripts/extract-recovery-payload.sh
./run-mac.sh
```
(Needs the ~233 MB Windows recovery launcher on Desktop as `Zombie Estate.exe` before extract.)

*Mac app:* `Zombie Estate.app` v1.0 — double-click launcher in the repo folder (wraps run-mac.sh).

*Notes:* Game assets are extracted locally, not stored in git. See repo README for content sourcing."""

payload = json.dumps({"text": text}).encode("utf-8")
req = urllib.request.Request(
    webhook,
    data=payload,
    headers={"Content-Type": "application/json"},
    method="POST",
)
with urllib.request.urlopen(req, timeout=30) as resp:
    body = resp.read().decode("utf-8", "replace")
    print(f"Slack response ({resp.status}): {body or 'ok'}")
PY

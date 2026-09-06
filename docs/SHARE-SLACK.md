# Zombie Estate Mac — Slack share copy

Paste your Slack Incoming Webhook URL, then run:

```bash
./scripts/post-slack-share.sh 'https://hooks.slack.com/services/...'
```

---

## Message preview

**Zombie Estate — Mac port (v1.0 app)**

We got the original Xbox Live Indie Game running natively on Apple Silicon Mac via Mono + FNA (no VM, no Wine).

**Repo (scripts + launcher):** https://github.com/hfost98-maker/zombie-estate-mac

**Requirements (install once):**
- Mono 6.12+ — https://www.mono-project.com/download/

**Quick start:**
```bash
git clone https://github.com/hfost98-maker/zombie-estate-mac.git
cd zombie-estate-mac
chmod +x run-mac.sh scripts/*.sh
./scripts/setup-fnlibs.sh
# Needs the ~233 MB Windows recovery launcher on your Desktop as "Zombie Estate.exe"
./scripts/extract-recovery-payload.sh
./run-mac.sh
# Or double-click "Zombie Estate.app" in the repo folder
```

**Mac app:** `Zombie Estate.app` v1.0 — double-click launcher in the repo (wraps `run-mac.sh`, windowed 1280×720 by default).

**Notes:**
- Game assets (`Content/*.xnb`) are not in git; `extract-recovery-payload.sh` pulls them from the recovery launcher.
- Internet Archive loose dump is incomplete; full XBLIG package is documented in the repo README.
- Co-op: host runs the Mac build; friends can join via Parsec virtual gamepads.

# docker-proxy watchdog — Issue #2772 Option C (interim)

Host-side watchdog that restarts `meepleai-api` when the userland `docker-proxy`
for its published `:8080` port dies (the intermittent `api.meepleai.app` 502 in
[#2772](https://github.com/meepleAi-app/meepleai-monorepo/issues/2772)).

**This is an interim band-aid.** The structural fix is the containerized
cloudflared cutover (Option A — scaffold in PR #2909,
`docs/for-developers/operations/cf-tunnel-containerization.md`). **Remove this
watchdog once that cutover is validated** — on the docker network there is no
`docker-proxy` in the path, so nothing to restart.

## What it does

Every minute it probes both host bindings `127.0.0.1:8080` and `::1:8080`. If
**both** are unreachable across 3 checks **and** the `meepleai-api` container is
running, it `docker restart`s the container (which recreates `docker-proxy`).
Guards:
- **Quiet on the happy path** (no log spam).
- **10-min restart cooldown** (`WATCHDOG_COOLDOWN`) so a genuinely-broken API
  isn't restart-looped — if `:8080` is still down inside the cooldown, the API
  itself (not `docker-proxy`) is the fault and needs a human.
- **Does not restart a stopped container** (the restart policy / a separate
  alert owns that case).

Requires `docker` + `curl` on the host (both already present on the VPS).

## Install (cron — matches `backup.cron`)

On the VPS (`deploy@204.168.135.69`):

```bash
sudo install -m 0755 infra/hetzner/docker-proxy-watchdog.sh /usr/local/bin/docker-proxy-watchdog.sh
# add the cron line (runs as root so it can talk to the docker socket)
sudo crontab -l 2>/dev/null | { cat; cat infra/hetzner/docker-proxy-watchdog.cron; } | sudo crontab -
# verify
sudo /usr/local/bin/docker-proxy-watchdog.sh; echo "exit=$?"   # happy path exits 0, silent
tail -f /var/log/meepleai-docker-proxy-watchdog.log
```

## Alternative: systemd timer

If you prefer systemd over cron, drop the cron line and use:

```ini
# /etc/systemd/system/meepleai-docker-proxy-watchdog.service
[Unit]
Description=meepleai docker-proxy watchdog (#2772 Option C)
After=docker.service
Requires=docker.service

[Service]
Type=oneshot
ExecStart=/usr/local/bin/docker-proxy-watchdog.sh
```

```ini
# /etc/systemd/system/meepleai-docker-proxy-watchdog.timer
[Unit]
Description=Run the meepleai docker-proxy watchdog every minute

[Timer]
OnBootSec=60
OnUnitActiveSec=60
AccuracySec=10s

[Install]
WantedBy=timers.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now meepleai-docker-proxy-watchdog.timer
```

## Uninstall (after the Option A cutover)

```bash
sudo crontab -l | grep -v docker-proxy-watchdog.sh | sudo crontab -   # or disable the timer
sudo rm -f /usr/local/bin/docker-proxy-watchdog.sh /var/run/meepleai-docker-proxy-watchdog.last
# systemd variant:
# sudo systemctl disable --now meepleai-docker-proxy-watchdog.timer
# sudo rm -f /etc/systemd/system/meepleai-docker-proxy-watchdog.{service,timer} && sudo systemctl daemon-reload
```

## Tunables (env)

| Var | Default | Meaning |
|---|---|---|
| `WATCHDOG_CONTAINER` | `meepleai-api` | container to probe/restart |
| `WATCHDOG_PORT` | `8080` | host port to probe |
| `WATCHDOG_COOLDOWN` | `600` | min seconds between restarts |
| `WATCHDOG_STATE` | `/var/run/meepleai-docker-proxy-watchdog.last` | last-restart timestamp file |

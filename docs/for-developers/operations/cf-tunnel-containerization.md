# Containerizing cloudflared (Cloudflare Tunnel) — Issue #2772 Option A

**Status**: scaffold (draft). The repo half is committed; the **cutover is an ops task** that requires the Cloudflare dashboard + a staging deploy. Nothing here changes the running edge until an operator runs the steps below.

**Issue**: [#2772](https://github.com/meepleAi-app/meepleai-monorepo/issues/2772) — intermittent `api.meepleai.app` 502 from host `docker-proxy` fragility (root cause B, split from #2770).

## Problem recap

`cloudflared` currently runs as a **VPS-native process** (not a container). It can only reach the API through the host-published port `127.0.0.1:8080` / `::1:8080` (`infra/compose.staging.yml` api `ports:`). Docker implements each published port with a userland `docker-proxy` process. Under VPS memory/disk pressure that `docker-proxy` gets OOM-killed; both host bindings then refuse connections **while the API container stays healthy on the `meepleai` docker network**. cloudflared logs `dial tcp [::1]:8080: connect: connection refused` → **502**.

## What Option A does

Run `cloudflared` as a **container on the `meepleai` docker network** so it reaches origins by container name (`http://meepleai-api:8080`) — the `docker-proxy` host path is removed from the request flow entirely.

The repo scaffold (this PR) adds:
- A `cloudflared` service in `infra/compose.staging.yml`, gated behind `profiles: [cf-tunnel]` so it is **not** started by the default `make staging`.
- `infra/secrets/cloudflared.secret.example` — the `TUNNEL_TOKEN` template.
- This runbook.

It does **not** (and cannot, from a repo PR):
- Change the Cloudflare dashboard tunnel ingress/origins (dashboard-only for a token/remotely-managed tunnel).
- Stop the VPS-native `cloudflared`.
- Free VPS resources.

## Prerequisites

- SSH to the staging VPS (`deploy@204.168.135.69`).
- Cloudflare Zero Trust dashboard access for the tunnel that fronts `*.meepleai.app`.
- The tunnel token (same one the VPS-native connector uses).
- **Outbound UDP 7844** open from the VPS to the Cloudflare edge (QUIC, the default protocol when a token is supplied; falls back to HTTP2/TCP). Verify: `nc -uvz -w 3 <cf-edge-ip> 7844`. If UDP is blocked the connector still works over HTTP2, just less optimally.

## Cutover (staging)

1. **Provide the token** on the VPS:
   ```bash
   cd /opt/meepleai/repo/infra
   printf 'TUNNEL_TOKEN=%s\n' "<paste-tunnel-token>" > secrets/cloudflared.secret
   chmod 600 secrets/cloudflared.secret
   ```

2. **Start the containerized connector** (opt-in profile; leaves the native one running for now — a token tunnel supports multiple connectors):
   ```bash
   docker compose -f docker-compose.yml -f compose.staging.yml --profile cf-tunnel up -d cloudflared
   docker logs meepleai-cloudflared --tail=50   # expect "Registered tunnel connection" lines
   ```

3. **Repoint origins in the Cloudflare dashboard** (Zero Trust → Networks → Tunnels → *your tunnel* → **Public Hostnames**). For each hostname change the **Service** origin from `http://localhost:<port>` to the container name on the `meepleai` network:

   | Public hostname | Old origin | New origin |
   |---|---|---|
   | `api.meepleai.app` | `http://localhost:8080` | `http://meepleai-api:8080` |
   | `meepleai.app` | `http://localhost:3000` | `http://meepleai-web:3000` |
   | `grafana.meepleai.app` | `http://localhost:3001` | `http://meepleai-grafana:3000` |
   | `prometheus.meepleai.app` | `http://localhost:9090` | `http://meepleai-prometheus:9090` |

   > While **both** connectors run, the dashboard origin is shared. `http://meepleai-*` only resolves inside the container, so once you repoint, the **native** connector can no longer serve these hostnames — do step 4 promptly.

4. **Stop the VPS-native connector** so only the containerized one serves traffic:
   ```bash
   sudo systemctl stop cloudflared
   sudo systemctl disable cloudflared   # prevent restart on reboot
   ```

5. **Verify** (repeat a few times; the old failure was intermittent):
   ```bash
   for i in $(seq 1 10); do curl -s -o /dev/null -w '%{http_code}\n' https://api.meepleai.app/health; sleep 2; done
   # expect 200 (or 503-Degraded), never 502
   ```
   The deploy smoke test `API Direct Health` (`.github/workflows/deploy-staging.yml`) allows `200 503` but **not** `502`, so a green staging deploy confirms the fix.

## Rollback

1. Re-point the dashboard origins back to `http://localhost:<port>`.
2. `sudo systemctl enable --now cloudflared` (restart the native connector).
3. `docker compose -f docker-compose.yml -f compose.staging.yml --profile cf-tunnel down` (or `stop cloudflared`).

The host `ports:` bindings are untouched by this scaffold, so the native path is intact for rollback.

## Optional follow-up (after the cutover is stable)

- Drop the now-unused host publishes `127.0.0.1:8080/::1:8080` (api) and `:3000` (web) from `compose.staging.yml` — once all edge traffic goes via the docker network they are dead surface (and their `docker-proxy` processes are exactly the fragility this issue removes). Keep the monitoring publishes only if you still reach Grafana/Prometheus over an SSH tunnel.
- Pin the `cloudflare/cloudflared` image to a specific version tag (the scaffold uses `:latest`).
- Consider a container `healthcheck` via `cloudflared`'s `--metrics` `/ready` endpoint for autoheal.

## Notes / references

- Option B (VPS resource headroom so `docker-proxy` isn't OOM-killed) is largely addressed by #2764 (disk-gate ordering fix) — complementary, not a substitute.
- Option C (host watchdog that `docker restart meepleai-api` when `:8080` is down) is a VPS-side mitigation, not a repo change.
- cloudflared docker/token references: Cloudflare Tunnel docs (`cloudflare/cloudflared` — `TUNNEL_TOKEN` env, `tunnel --no-autoupdate run`, QUIC/UDP 7844 egress).

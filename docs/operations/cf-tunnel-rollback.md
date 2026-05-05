# Cloudflare Tunnel Rollback Runbook

> **Goal**: Revert from Cloudflare Tunnel back to Traefik+Let's Encrypt edge.
> **Estimated time**: 30 min.
> **When to use**: smoke set fails after migration; CF Tunnel adds unacceptable latency; CF outage.

## Phase 0 — Verify Traefik config still in repo

```bash
ls infra/traefik/ infra/compose.traefik.yml
```
Expected: files exist. If not, restore from `/tmp/traefik-backup-*.tgz` (created in cf-tunnel-migration.md Phase 0).

## Phase 1 — Disable cloudflared

```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69
sudo systemctl stop cloudflared
sudo systemctl disable cloudflared
```

## Phase 2 — Re-open ports

```bash
sudo ufw allow 80/tcp  comment 'HTTP — Traefik'
sudo ufw allow 443/tcp comment 'HTTPS — Traefik'
sudo ufw status verbose
```

## Phase 3 — Restart Traefik

```bash
cd /opt/meepleai/repo/infra
make staging-minimal  # or 'make staging' if you reverted Makefile changes
docker ps --format "{{.Names}}" | grep traefik
```
Expected: `meepleai-traefik` and `meepleai-docker-socket-proxy` running.

## Phase 4 — Switch DNS back

In CF dashboard:
- `meepleai.app` CNAME → A record pointing to 204.168.135.69 (proxied: orange cloud).
- `api.meepleai.app` same.

Wait for DNS propagation:
```bash
until dig +short meepleai.app | grep -q '^204\.168\.135\.69$'; do
  echo "$(date -u +%H:%M:%S) waiting DNS revert..."; sleep 10
done
echo "✅ DNS reverted to direct A record"
```

## Phase 5 — Validate

```bash
curl -sf https://meepleai.app/health | jq .
bash infra/scripts/smoke-set.sh https://meepleai.app
```
Expected: automated smoke PASS.

## Phase 6 — Document the rollback

Add an entry to `docs/operations/dr-walkthrough-log.md`:

| Date | Action | Reason | Notes |
|------|--------|--------|-------|
| _yyyy-mm-dd_ | CF Tunnel rollback | _e.g., latency >300ms_ | _details_ |

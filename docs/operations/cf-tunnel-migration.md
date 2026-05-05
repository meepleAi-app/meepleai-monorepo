# Cloudflare Tunnel Migration Runbook

> **Goal**: Migrate `meepleai.app` from Traefik+Let's Encrypt edge to Cloudflare Tunnel.
> **Audience**: project owner (single tester).
> **Prerequisites**: Cloudflare account with `meepleai.app` zone; SSH access to staging.
> **Estimated time**: 3-4 hours. Reversible via `cf-tunnel-rollback.md` in <30 min.
> **Pre-migration smoke test**: `make -C infra smoke-staging` must pass automated set.

## Phase 0 — Pre-flight

1. Confirm staging is healthy:
   ```bash
   curl -sf https://meepleai.app/health | jq .
   bash infra/scripts/smoke-set.sh https://meepleai.app
   ```
2. Backup current Traefik config:
   ```bash
   ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 \
     'cd /opt/meepleai/repo/infra && tar czf /tmp/traefik-backup-$(date +%Y%m%d).tgz traefik/ compose.traefik.yml'
   ```
3. Take DB backup:
   ```bash
   ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 \
     'cd /opt/meepleai/repo/infra && bash scripts/backup.sh'
   ```
4. Capture downtime baseline (for G2 measurable):
   ```bash
   # Terminal 1: continuous probe (run during migration window)
   while true; do
     printf "%s %s %s\n" "$(date -u +%H:%M:%S)" \
       "$(curl -s -o /dev/null -w '%{http_code}' --max-time 5 https://meepleai.app/health)" \
       "$(curl -s -o /dev/null -w '%{time_total}s' --max-time 5 https://meepleai.app/health)"
     sleep 1
   done | tee /tmp/cf-cutover-probe.log
   ```

## Phase 1 — Cloudflare setup (web UI)

1. Log into Cloudflare dashboard → Zero Trust → Networks → Tunnels.
2. Create a new tunnel named `meepleai-staging`.
3. Copy the `cloudflared` install command (includes the tunnel token).
4. Configure routes (web UI):
   - `meepleai.app` → `http://localhost:3000` (web)
   - `api.meepleai.app` → `http://localhost:8080` (api)
5. **REQUIRED for single-tester alpha (Fix #2 from spec-panel review)**: Configure CF Access policy:
   - Application: `meepleai.app` (and `api.meepleai.app`)
   - Policy: "owner-only" with email match → your email only.
   - Identity provider: Google SSO (preferito; alternativa: One-time PIN via email).
   - **NOTA**: senza CF Access, dopo Phase 5 (UFW chiude 80/443) gli endpoint non-auth (`/health`, `/api/v1/games`, `/metrics`) diventano pubblici via tunnel — single point of compromise per single-tester. Mandatory.

## Phase 2 — Install `cloudflared` on VPS

1. SSH to staging.
2. Install daemon (the tunnel token from Phase 1 is embedded):
   ```bash
   sudo curl -L --output /tmp/cloudflared.deb \
     https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-linux-arm64.deb
   sudo dpkg -i /tmp/cloudflared.deb
   sudo cloudflared service install <TOKEN_FROM_PHASE_1>
   sudo systemctl enable --now cloudflared
   ```
3. Verify daemon is running:
   ```bash
   sudo systemctl status cloudflared
   sudo journalctl -u cloudflared -n 20
   ```
4. Verify tunnel is up in CF dashboard (status: Healthy).

## Phase 3 — Expose API to localhost

The API container currently does NOT bind a host port (only Traefik routes traffic). Add a temporary host bind so cloudflared can reach it.

1. Edit `infra/compose.staging.yml` to add `ports: ["127.0.0.1:8080:8080"]` to the `api` service.
2. Edit `infra/compose.staging.yml` to add `ports: ["127.0.0.1:3000:3000"]` to the `web` service.
3. Restart:
   ```bash
   make -C /opt/meepleai/repo/infra staging-minimal
   ```
4. Verify:
   ```bash
   curl -sf http://localhost:8080/health | jq .
   curl -sf http://localhost:3000 | head -3
   ```

## Phase 4 — Cutover

1. Test traffic flow via CF Tunnel (DNS still on Traefik):
   - In CF dashboard, temporarily map `dr-test.meepleai.app` → tunnel routes.
   - `curl -sf https://dr-test.meepleai.app/health | jq .`
   - If 200 OK with same payload as current, cutover is safe.
   - **Cleanup**: remove `dr-test.meepleai.app` route from CF dashboard after verification.
2. Switch DNS in CF dashboard:
   - Replace A record `meepleai.app` → 204.168.135.69 with CNAME `meepleai.app` → `<tunnel-id>.cfargotunnel.com` (proxied).
   - Same for `api.meepleai.app`.
3. Wait for DNS propagation (target ≤5 min, but verify don't assume):
   ```bash
   # Loop until CF tunnel CNAME resolves
   until dig +short meepleai.app | grep -q cfargotunnel; do
     echo "$(date -u +%H:%M:%S) waiting DNS..."; sleep 10
   done
   echo "✅ DNS propagated to CF tunnel"
   ```
4. Run smoke set:
   ```bash
   bash infra/scripts/smoke-set.sh https://meepleai.app
   ```
   All automated checks must PASS.
5. Stop the downtime probe (Phase 0 step 4) and review `/tmp/cf-cutover-probe.log` for the count of non-200 responses (this is the **measured downtime**).

## Phase 5 — Lock down ports

1. Enable UFW firewall:
   ```bash
   sudo ufw allow 22/tcp comment 'SSH'
   sudo ufw deny 80/tcp  comment 'HTTP — closed (CF Tunnel)'
   sudo ufw deny 443/tcp comment 'HTTPS — closed (CF Tunnel)'
   sudo ufw enable
   sudo ufw status verbose
   ```
2. Verify from external host:
   ```bash
   nmap -p 80,443,22 204.168.135.69
   ```
   Expected: 22 open, 80/443 filtered or closed.
3. **Verify CF Access auth gate is active** (Fix #2 from spec-panel review):
   ```bash
   # Without auth header — should be 401/403 redirect to CF login
   curl -si https://meepleai.app/api/v1/admin/health | head -3
   curl -si https://meepleai.app/metrics            | head -3
   ```
   Expected: HTTP 302/401/403 (CF Access challenge) o HTML login page. **NOT** 200 con payload applicativo. Se 200, la CF Access policy non è attiva su quel route — torna a Phase 1 step 5.

## Phase 6 — Decommission Traefik (after 7-day soak)

After 7 days of CF Tunnel running with smoke set passing:

1. Stop Traefik:
   ```bash
   docker compose -f docker-compose.yml -f compose.staging.yml -f compose.traefik.yml \
     --profile proxy stop traefik docker-socket-proxy
   ```
2. Remove Traefik labels from `compose.staging.yml` (api, web, embedding, reranker, grafana, prometheus blocks).
3. Remove Traefik secrets from `infra/secrets/`:
   ```bash
   rm infra/secrets/traefik.secret  # backup first if anything custom
   ```
4. Update `Makefile`:
   - Remove `--profile proxy` from `staging-minimal` and `staging-with-tutor` targets.
5. Commit:
   ```bash
   git add infra/compose.staging.yml infra/Makefile
   git rm infra/secrets/traefik.secret
   git commit -m "ops(infra): decommission Traefik after CF Tunnel migration"
   ```

## Phase 7 — Validation

1. `bash infra/scripts/smoke-set.sh https://meepleai.app` → automated PASS.
2. Manual UI checklist (`docs/operations/smoke-manual-ui-checklist.md`) → A4 + C5 PASS.
3. `nmap -p 80,443 204.168.135.69` → filtered/closed.
4. `free -m` on VPS → ≥6GB available (Traefik freed ~512MB).
5. Add entry to `docs/operations/dr-walkthrough-log.md` with date + "CF Tunnel migration completed".

## Troubleshooting

| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| `cloudflared` shows "DNS resolution failed" | Routes not configured in CF dashboard | Re-check Phase 1 step 4 |
| `curl https://meepleai.app/health` returns 502 | API container not bound to localhost | Verify Phase 3 step 1 + restart |
| Smoke set A1 fails (login) | CF Access blocking POST | Add CF Access bypass for `/api/v1/auth/*` paths or disable Access for staging |
| `nmap` still shows 80/443 open | UFW not enabled or rule order wrong | `sudo ufw status verbose` and re-check Phase 5 |
| `/metrics` returns 200 instead of 401/403 | CF Access policy not configured for that route | Add wildcard `*meepleai.app/*` policy in CF dashboard |

## Rollback

If anything breaks: `docs/operations/cf-tunnel-rollback.md`.

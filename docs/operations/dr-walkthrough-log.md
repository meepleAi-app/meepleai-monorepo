# DR Walkthrough Log

> Quarterly review log for `docs/operations/disaster-recovery-runbook.md`.
> Triggered by `infra/scripts/dr-walkthrough-reminder.sh` (cron: 1st jan/apr/jul/oct).

## How to log a walkthrough

After receiving a quarterly reminder:

1. Open `docs/operations/disaster-recovery-runbook.md`.
2. Read end-to-end. For each step, verify:
   - Commands still work as written (no deprecated flags, no removed scripts).
   - Service names, paths, env vars match current `infra/docker-compose.yml`.
   - Required secrets are listed in `infra/secrets/*.secret.example`.
3. Note any **drift** (runbook says X, reality is Y).
4. Add a row to the table below (most recent first).
5. If drift was found, open a separate PR to fix the runbook.

## Walkthrough Entries (most recent first)

| Date | Drift Found | Status | Notes |
|------|-------------|--------|-------|
| 2026-05-05 | none | ok | T18 CF Tunnel cutover completed (Phase 2-6 hard-cutover, no soak — no app in use). cloudflared 2026.3.0 installed (POP fra18). Tunnel routes: meepleai.app, api.meepleai.app, dr-test.meepleai.app. CF Access policy `meepleai-prod` covers production hostnames (owner-only). UFW deny 80/443 + Traefik containers stopped → external nmap shows 22 OPEN, 80/443 CLOSED. Auth gate verified: curl /api/v1/admin/* + /metrics return 302 to CF login. Free RAM +500MB post-Traefik-stop. Backup pre-cutover: /backups/meepleai/20260505-150004 (DB 43.2MB + PDF + Redis). Smoke automated against tunnel returns 302 (expected behavior under owner-only policy); for monitoring add CF Access bypass for /health or use service token. Production drift on VPS (6 commits, untracked files) deferred to follow-up reconciliation session. |

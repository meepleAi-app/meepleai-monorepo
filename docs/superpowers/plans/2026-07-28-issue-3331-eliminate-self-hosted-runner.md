# Plan — Issue #3331: eliminate the co-located self-hosted runner (Option E)

**Date**: 2026-07-28 · **Issue**: [#3331](https://github.com/meepleAi-app/meepleai-monorepo/issues/3331) · **Decision**: Option E (gate verdict, see the [spec-panel](../specs/2026-07-28-spec-panel-3331-cicd-runner.md)).

## Goal

Retire the GitHub Actions self-hosted runner co-located on the Hetzner staging VPS. Build/test on GitHub-hosted runners; keep deploying to staging over public SSH from a cloud runner. Restores the CI-vs-runtime bulkhead (a build OOM can no longer take down the app), removes the whole runner pet-care apparatus, at ~$0–5/month.

## Key findings (from the migration research)

1. **The two-way door is a single repo variable, `vars.RUNNER`.** Every self-hosted-capable job uses `runs-on: ${{ vars.RUNNER && fromJSON(vars.RUNNER) || 'ubuntu-latest' }}`. **Deleting the `RUNNER` repo variable re-routes all 15 workflows to `ubuntu-latest` with zero file edits.** Re-adding it reverts instantly.
2. **SSH is orthogonal to the runner location.** Deploy/rollback/ops steps gate on `vars.DEPLOY_METHOD == 'ssh'`, not `vars.RUNNER`, and reach `secrets.STAGING_HOST` over **public SSH** (`appleboy/ssh-action` + `ssh` + `ssh-keyscan`). They run identically from cloud. Secrets `STAGING_HOST`/`STAGING_USER`/`STAGING_SSH_KEY` are already defined.
3. **K6 is NOT a blocker** (the spec over-estimated it). The nightly `test-performance.yml` is fully self-contained — postgres/redis as GHA **service containers** + an ephemeral `dotnet run` API on the runner, K6 hitting `http://localhost:8080` (the runner's own API, not the server). It already runs option (b). The deploy-smoke K6 (`validate` job) already hits the public `https://meepleai.app`. Both are cloud-portable with **zero code changes**; k6 install already handles amd64.
4. **The only genuine co-location step** is `pre-deploy-check` → "Verify VPS disk headroom" (`deploy-staging.yml:303-337`, `if: vars.RUNNER`): a local `df /` that measures the VPS disk. It auto-skips on cloud. Converting it to an SSH pre-check is **the only net-new code work in E**.
5. Backend-e2e, test-e2e (Playwright — cloud is the *standard* place for it), rollback, and the 7 ops one-offs are all cloud-portable with zero changes (they SSH into the server).

## Inventory

### Migrates to cloud (zero code, just drop `vars.RUNNER`)
`deploy-staging.yml` jobs (detect-changes, pre-deploy-check, snapshot-baseline, deploy, validate, e2e-staging), `test-performance.yml`, `test-e2e.yml`, `backend-e2e-tests.yml`, `rollback.yml`, and 7 ops one-offs (admin-reset, check-api-logs, diagnose-admin, fix-db-password, test-login, check-role-case, fix-line-endings).

### One code change (Phase 1)
`deploy-staging.yml:303-337` — convert the local `df /` disk gate to an SSH pre-check (`ssh $HOST 'df -BG /'`), so the guardrail survives the cutover.

### Delete (Phase 3 — co-location pet-care, "Pile 1")
- Workflows: `runner-health-check.yml`, `runner-maintenance.yml`, `monitor-runner-queue.yml` (+ retire the `WATCHDOG_RUNNERS_PAT` secret + `docs/for-developers/operations/watchdog-runners-pat-setup.md`). Bonus: fewer Search-API monitor workflows (ADR-078 budget).
- `infra/runner/`: `maintenance.sh`, `monitor.sh`, `apply-memory-overrides.sh`, `systemd-overrides/10-memory-limits.conf`, `prometheus-runner.yml`, `docker-compose.monitoring.yml`.
- Prometheus alerts: `infra/prometheus/alerts/runner-availability.yml` + its 4 references (`prometheus.yml:29-30`, `prometheus.staging.yml:24-25`, `docker-compose.yml:352-353`, `compose.staging.yml:334-337`). (prod configs never referenced it.)

### Keep (dedicated-VM IaC for the D fallback)
`infra/runner/cloud-init.yml`, `setup-vm.sh`, `setup-runner.sh` — the recipe to re-provision a dedicated runner if E is ever reversed toward D.

## Phased rollout (incremental, reversible)

### Phase 0 — validate portability (no risk, no teardown)
Run `test-performance.yml`, `test-e2e.yml`, `backend-e2e-tests.yml` on cloud (temporarily point `RUNNER` to `["ubuntu-latest"]` or use a test branch) and confirm green. This proves the heavy suites run on hosted before committing. Instantly reversible.

### Phase 1 — SSH-ify the disk gate (the only code PR)
Convert `deploy-staging.yml:303-337` "Verify VPS disk headroom" to SSH into `STAGING_HOST` and run `df -BG /` (reuse the snapshot-baseline SSH pattern at `:748-763`), keeping the `DEPLOY_DISK_GATE_GB` threshold. Now the gate works from cloud. Merge to main-dev → main-staging.

### Phase 2 — cutover
Delete the `RUNNER` repo variable (GitHub → Settings → Secrets and variables → Actions → Variables). All 15 workflows fall to `ubuntu-latest`; guarded `if: vars.RUNNER` steps skip. Trigger a deploy and confirm: `runner.name` shows "GitHub Actions", the deploy SSHes to `STAGING_HOST` and succeeds. Expect one deploy in `PERF_REGRESSION_MODE=warn` (non-blocking; DEPLOYMENT.json.performance re-seeds automatically).

### Phase 3 — cleanup + deregister (the soft point of no return)
1. On the host: `sudo ./svc.sh stop && sudo ./svc.sh uninstall` in `/home/deploy/actions-runner`, then `./config.sh remove --token <REMOVE_TOKEN>` (token via `gh api -X POST repos/meepleAi-app/meepleai-monorepo/actions/runners/remove-token`).
2. Purge host pet-care: the 3G systemd drop-in, `deploy` crontab (maintenance.sh crons), root docker-prune cron, `/home/deploy/actions-runner/`.
3. Delete the Pile-1 workflows + `infra/runner/` Pile-1 files + the Prometheus runner alert file & its 4 references (one PR).
4. Supersede ADR-044 (record E as the accepted direction; the 2026-07-28 Update already flags the drift).

## Rollback (two-way door)
- **Phases 0-2**: re-create the `RUNNER` repo variable → everything routes back to self-hosted. The runner service is still installed, so it resumes immediately.
- **Phase 3** is the soft point of no return: once the runner is deregistered/deprovisioned, rollback means re-provisioning from `cloud-init.yml`/`setup-vm.sh`/`setup-runner.sh` (kept for exactly this) — i.e. it becomes Option D.

## Risks
- **Hosted-minute cost creep** if the heavy prod-promotion suites (`backend-e2e`, `test-e2e`) run often — measure actual monthly minutes (they were 0 runs in the last 30 days). Set a billing/usage alert. Estimated incremental E: ~600-650 min/mo ≈ $3-5/mo (≤ the D VM), ~$0 if free-tier headroom exists.
- **Loss of the local disk pre-flight** — mitigated by Phase 1 (SSH pre-check). The original disk-full incidents were build-on-VPS pressure, already removed (builds are 100% cloud).
- **Perf-baseline one-time shift** on the first cloud deploy — non-blocking (`mode=warn`), auto-re-seeds.
- **Scoped deploy key hardening** (dedicated key + `authorized_keys` restrictions) is a separate, optional follow-up: a forced `command=` is incompatible with the current multi-command + `scp` deploy, so it needs a server-side `staging-deploy.sh` refactor. Not required for E; the key's power is unchanged, only *where* it is used.

## Effort
The real technical work is **one intervention** (SSH-ify the disk gate, ~half a day) + deleting a repo variable + removing 3 workflows and the runner alerts + a host teardown. The presumed blocker (K6) needs no work.

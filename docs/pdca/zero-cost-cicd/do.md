# DO — Zero-Cost CI/CD Implementation Log

**Epic**: #2967
**Started**: 2026-03-02

## Implementation Steps

### Phase 1: Code Preparation (PR branch)

- [x] Created `infra/runner/` directory with automation scripts
- [x] `cloud-init.yml` — Oracle VM auto-provisioning (Docker, .NET 9, Node 20, pnpm, swap)
- [x] `setup-vm.sh` — Manual VM setup script (alternative to cloud-init)
- [x] `setup-runner.sh` — GitHub Actions runner installation + systemd service
- [x] `monitor.sh` — Health monitoring (JSON + human output + exit-code check)
- [x] `maintenance.sh` — Automated maintenance (daily/weekly/monthly + cron installer)
- [x] `docker-compose.monitoring.yml` — Node Exporter + Prometheus + Grafana stack
- [x] `prometheus-runner.yml` — Prometheus scrape config for runner VM
- [x] Migrated all 15 workflow files (58 `runs-on` occurrences) to `${{ vars.RUNNER || 'ubuntu-latest' }}`
- [x] Created `docs/deployment/self-hosted-runner.md` with full setup + troubleshooting guide

### Phase 2: Oracle VM Provisioning (manual)

- [ ] Create Oracle Cloud Always Free account
- [ ] Provision ARM64 VM (4 OCPU, 24GB RAM, 200GB)
- [ ] SSH access verified
- [ ] Run `setup-vm.sh` or verify cloud-init completed

### Phase 3: Runner Activation (manual)

- [ ] Run `setup-runner.sh --token <TOKEN>`
- [ ] Verify runner shows "Idle" in GitHub Settings
- [ ] Set `vars.RUNNER=self-hosted` in GitHub repo settings
- [ ] Trigger test CI run
- [ ] Run `maintenance.sh --install-cron`

### Phase 4: Monitoring Setup (optional)

- [ ] Deploy monitoring stack: `docker compose -f docker-compose.monitoring.yml up -d`
- [ ] Access Grafana at `:3001`, configure Node Exporter dashboard
- [ ] Verify Prometheus scraping at `:9090`

## Decisions Made

| Decision | Rationale |
|----------|-----------|
| `vars.RUNNER` toggle | Zero-downtime switch, no workflow file changes needed |
| cloud-init + manual script | Supports both automated and manual provisioning |
| 4GB swap | Prevents OOM during .NET builds on 24GB RAM |
| Daily Docker prune | Prevents disk bloat from CI image accumulation |
| systemd service | Auto-restart on crash, auto-start on boot |

## Blockers / Issues

*None yet — pending manual VM provisioning.*

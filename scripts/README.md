# Scripts Directory

Automation scripts for MeepleAI development, deployment, testing, and maintenance.

## Directory Structure

### `deployment/` — Production Deployment
- **`deploy-meepleai.ps1`** — Production deployment automation (up, down, restart, logs, status, backup, update)

```powershell
.\scripts\deployment\deploy-meepleai.ps1 up       # Start production
.\scripts\deployment\deploy-meepleai.ps1 status    # Service status
.\scripts\deployment\deploy-meepleai.ps1 backup    # Backup databases
.\scripts\deployment\deploy-meepleai.ps1 update    # Update to latest images
```

### `development/` — Development Workflow
- **`e2e-demo-setup.ps1`** — E2E demo setup (Docker services, BGG import, PDF upload)

```powershell
.\scripts\development\e2e-demo-setup.ps1
```

### `monitoring/` — Resource Monitoring
- **`docker-resource-monitor.ps1`** — Docker container resource monitoring

```powershell
.\scripts\monitoring\docker-resource-monitor.ps1 -Watch              # Real-time
.\scripts\monitoring\docker-resource-monitor.ps1 -Baseline           # Snapshot
.\scripts\monitoring\docker-resource-monitor.ps1 -Export stats.csv   # Export CSV
.\scripts\monitoring\docker-resource-monitor.ps1 -Watch -Threshold 90
```

### `quality/` — Code Quality
- **`validate-doc-links.ps1`** — Validate markdown links in documentation
- **`validate-csharp-namespaces.ps1`** — Validate C# namespace consistency

```powershell
.\scripts\quality\validate-doc-links.ps1
.\scripts\quality\validate-csharp-namespaces.ps1
```

### `security/` — Security Auditing
- **`epic-4068-security-audit.sh`** — Comprehensive security audit (40+ tests: auth, authz, input validation, rate limiting, TLS, dependencies)

```bash
./scripts/security/epic-4068-security-audit.sh [environment]
```

### `testing/` — Integration Testing
- **`test-services.ps1`** — Health check all MeepleAI services
- **`test-oauth-health.ps1`** — Test OAuth provider configuration
- **`test-runbooks.ps1`** — Test operational runbooks (alerting)

```powershell
.\scripts\testing\test-services.ps1
.\scripts\testing\test-oauth-health.ps1
.\scripts\testing\test-runbooks.ps1 all              # All runbooks
.\scripts\testing\test-runbooks.ps1 high-error-rate   # Specific runbook
```

### Root-Level Scripts

| Script | Purpose |
|--------|---------|
| `git-workflow.sh` | Git workflow helpers (feature, merge, staging, release, hotfix) |
| `validate-workflows.js` | GitHub Actions workflow validation (used in CI) |
| `security-check-local.ps1` | Local security scans (pnpm audit, dotnet audit, Semgrep, safety) |
| `setup-branch-protection.sh` | Configure GitHub branch protection rules |
| `seed-dashboard-data.ps1` + `.sql` | Seed test data for Gaming Hub Dashboard |
| `seed-default-agent.ps1` + `.sql` | Seed default POC agent (MeepleAssistant) |

## Platform

Scripts are PowerShell-based (cross-platform with `pwsh`). Two bash scripts remain for specific use cases:
- `git-workflow.sh` — referenced in 12+ docs, platform-agnostic
- `setup-branch-protection.sh` — uses `gh` CLI
- `security/epic-4068-security-audit.sh` — comprehensive security suite

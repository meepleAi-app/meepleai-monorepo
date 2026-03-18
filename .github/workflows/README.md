# GitHub Actions Workflows

## Workflow Organization

Workflows are organized by category with consistent naming prefixes:

```
.github/workflows/
├── # Core CI/CD (no prefix)
├── ci.yml                    # Main CI pipeline
├── deploy-staging.yml        # Staging deployment
├── deploy-production.yml     # Production deployment
│
├── # Testing (test- prefix)
├── test-e2e.yml              # Full E2E suite (6-shard parallel)
├── test-performance.yml      # K6 + Lighthouse performance
├── test-visual.yml           # Playwright + Chromatic visual
│
├── # Security (security- prefix)
├── security-scan.yml         # CodeQL + dependency scan + secrets
├── security-pentest.yml      # OWASP penetration tests
├── security-review.yml       # Quarterly security review reminder
│
├── # Automation (auto- prefix)
├── auto-branch-policy.yml    # Branch protection rules
├── auto-dependabot.yml       # Dependabot auto-merge
├── auto-validate.yml         # Workflow validation
```

## Core CI/CD

### ci.yml - Main CI Pipeline
- **Triggers**: PR to main, main-dev, main-staging, frontend-dev, backend-dev
- **Jobs**: Frontend (lint, typecheck, test, build), Backend (build, test), E2E critical paths
- **Features**: Path-based filtering, parallel execution, Codecov integration, dynamic runner selection (self-hosted for staging/prod PRs, GitHub-hosted for dev PRs)

### deploy-staging.yml - Staging Deployment
- **Triggers**: Push to `main-staging` branch, manual dispatch
- **Jobs**: Pre-deploy tests, Docker build, SSH/K8s deploy, validation
- **Features**: Automatic deployment (no approval), health checks

### deploy-production.yml - Production Deployment (DISABLED)
- **Status**: Disabled (`.yml.disabled`) — no production environment yet
- **Re-enable**: Rename back to `.yml` when production environment is ready
- **Triggers**: Push/tag to `main` branch, manual dispatch
- **Features**: Manual approval required, rollback capability, GitHub Release creation

## Testing Workflows

### test-e2e.yml - Full E2E Suite
- **Triggers**: Push/PR with web/api changes
- **Jobs**: 6-shard parallel Playwright tests, quality gate (≥90% pass rate)
- **Features**: Full browser matrix, Playwright browser caching, coverage reports, PR comments
- **Optimization** (Issue #3082): Increased from 4 to 6 shards (~30% faster), added browser caching (80-90% cache hit rate)

### test-performance.yml - Performance Testing
- **Triggers**: Nightly schedule, PR with api/web changes, manual
- **Jobs**: K6 load tests, Lighthouse CI
- **Features**: Smoke/load/stress test types, Core Web Vitals, failure notifications

### test-visual.yml - Visual Regression
- **Triggers**: Push/PR with web changes
- **Jobs**: Playwright snapshots, Chromatic Storybook review
- **Features**: Visual diff detection, PR comments with review links

## Security Workflows

### security-scan.yml - Security Scanning
- **Triggers**: Push to main, weekly schedule
- **Jobs**: CodeQL SAST (C#, JavaScript), dependency vulnerabilities, Semgrep secrets
- **Features**: SARIF uploads, HIGH/CRITICAL threshold enforcement

### security-pentest.yml - Penetration Testing
- **Triggers**: Weekly schedule, PR with security label, security file changes
- **Jobs**: OWASP 2FA penetration tests (brute force, replay, timing attacks)
- **Features**: 15 security tests, automatic PR comments

### security-review.yml - Security Review Reminder
- **Triggers**: Quarterly schedule (Jan, Apr, Jul, Oct)
- **Jobs**: Create security review GitHub issue
- **Features**: Review checklist, command templates, success criteria

## Automation Workflows

### auto-branch-policy.yml - Branch Protection
- **Triggers**: PR to main, main-staging, main-dev
- **Jobs**: Validate source branch matches policy
- **Policy**: main ← main-staging only; main-staging ← main-dev only; main-dev ← frontend-dev, backend-dev, feature/*, fix/*, etc.

### auto-dependabot.yml - Dependabot Auto-merge
- **Triggers**: Dependabot PRs with `automerge` label
- **Jobs**: CI status check, auto-merge with squash
- **Features**: Waits for CI, comments on PR

### auto-validate.yml - Workflow Validation
- **Triggers**: PR/push with workflow changes
- **Jobs**: Validate pnpm cache patterns
- **Features**: Prevents broken workflows from merging

## Workflow Execution Targets

| Workflow | Target Time | Notes |
|----------|-------------|-------|
| ci.yml (Frontend) | 3-5 min | Parallel lint/typecheck |
| ci.yml (Backend) | 5-7 min | With Testcontainers |
| ci.yml (E2E) | 3-5 min | Critical paths only |
| test-e2e.yml | 10-15 min | Full 6-shard suite |
| test-performance.yml | 15-20 min | K6 + Lighthouse |
| test-visual.yml | 5-10 min | Playwright + Chromatic |
| security-scan.yml | 8-12 min | Full security suite |

## Configuration

### Required Secrets
- `CODECOV_TOKEN` - Code coverage uploads
- `LHCI_GITHUB_APP_TOKEN` - Lighthouse CI (optional, falls back to GITHUB_TOKEN)
- `SLACK_WEBHOOK_URL` - Failure notifications (optional)

### Environment Variables
See individual workflow files for environment-specific configuration.

## Self-Hosted ARM64 Runner

Workflows use a tiered runner selection strategy to balance cost and concurrency:

| Branch target | Runner | Rationale |
|--------------|--------|-----------|
| `main-dev`, `frontend-dev`, `backend-dev` | `ubuntu-latest` | High PR concurrency, no queue |
| `main-staging`, `main` | Self-hosted (ARM64) | Low frequency, saves GH Actions minutes |
| Deploy workflows | Self-hosted (ARM64) | Always staging/prod, free compute |

**CI (`ci.yml`)** uses dynamic selection via the `changes` job output:
```yaml
# In changes job:
runner: ${{ steps.select-runner.outputs.runner }}

# In downstream jobs:
runs-on: ${{ needs.changes.outputs.runner }}
```

**Other workflows** use the static toggle pattern:
```yaml
runs-on: ${{ vars.RUNNER || 'ubuntu-latest' }}
```

**Architecture Decision**: [ADR-044](../../docs/architecture/adr/adr-044-self-hosted-arm64-runner.md)

### Runner Specs

| Resource | Value |
|----------|-------|
| Provider | Oracle Cloud Free Tier |
| CPU | Ampere A1, 4 OCPUs (ARM64) |
| Memory | 24 GB |
| Disk | 200 GB |
| Max concurrent jobs | 2 (recommended) |

### ARM64 Exclusions

| Workflow | Job | Reason |
|----------|-----|--------|
| `security-scan.yml` | `codeql` | CodeQL CLI has no linux/arm64 binary |

### Operational Workflows

| Workflow | Schedule | Purpose |
|----------|----------|---------|
| `runner-health-check.yml` | Every 15 min | Docker, disk, memory monitoring |
| `runner-maintenance.yml` | Weekly Sun 3 AM | Docker prune, temp cleanup, disk alerts |

### Rollback Procedure

**Full rollback** (all workflows revert to GitHub-hosted):
1. Go to GitHub → Settings → Variables (organization or repository level)
2. Delete or clear the `RUNNER` variable
3. All workflows will immediately fall back to `ubuntu-latest`
4. Verify: check workflow logs for `runner.name` containing "GitHub Actions"

**Per-workflow rollback** (single workflow reverts):
1. Edit the specific workflow file
2. Replace `runs-on: ${{ vars.RUNNER || 'ubuntu-latest' }}` with `runs-on: ubuntu-latest`
3. Commit and push — that workflow now always uses GitHub-hosted

**Per-job rollback** (single job within a workflow):
1. Override only the specific job's `runs-on` to `ubuntu-latest`
2. Other jobs in the same workflow continue using the self-hosted runner

### Visual Test Baselines

When switching runner architecture (x86 ↔ ARM64), Playwright visual test baselines must be regenerated:

```bash
cd apps/web
pnpm test:e2e:visual --update-snapshots --project=desktop-chrome
```

Font rendering differs between architectures, causing false-positive visual diffs.

### Performance Notes

| Category | Expected Delta (ARM64 vs x86) | Timeout |
|----------|-------------------------------|---------|
| Deploy workflows | ≤10% slower (network-bound) | Default |
| Frontend CI | ≤15% slower | Default |
| Backend tests | 30-40% slower (.NET JIT) | 60 min |

## Local Testing

```bash
# Install act (GitHub Actions local runner)
# https://github.com/nektos/act

# Test workflow locally
act -W .github/workflows/ci.yml -j frontend

# Dry run
act -W .github/workflows/ci.yml --dryrun
```

## Troubleshooting

### CI Failures
1. Check job logs in GitHub Actions UI
2. Review path filters if jobs unexpectedly skipped
3. Verify secrets are configured

### Runner Issues
1. Check runner status: GitHub → Settings → Actions → Runners
2. Check health: Review latest `runner-health-check.yml` run
3. Check disk: Review latest `runner-maintenance.yml` run or trigger manually
4. Full rollback: Clear `vars.RUNNER` variable (see Rollback Procedure above)

### Performance Issues
1. Check K6/Lighthouse reports in artifacts
2. Review API response times
3. Check for memory/resource constraints

### Security Alerts
1. Review CodeQL findings
2. Check dependency audit reports
3. Address HIGH/CRITICAL vulnerabilities first

---

**Last Updated**: 2026-03-09

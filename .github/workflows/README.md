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
│
├── # Reusable Workflows
├── notify-slack.yml              # Reusable: centralized Slack notifications
```

## Core CI/CD

### ci.yml - Main CI Pipeline
- **Triggers**: PR to main, main-dev, main-staging
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
- **Policy**: main ← main-staging only; main-staging ← main-dev only; main-dev ← feature/*, fix/*, hotfix/*, docs/*, refactor/*, chore/*, etc.

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
| security-scan.yml | 8-12 min | Full security suite |

## Configuration

### Required Secrets
- `CODECOV_TOKEN` - Code coverage uploads
- `LHCI_GITHUB_APP_TOKEN` - Lighthouse CI (optional, falls back to GITHUB_TOKEN)
- `SLACK_WEBHOOK_URL` - Generic Slack notifications (existing, optional)
- `SLACK_GITNOTIFY_WEBHOOK_URL` - GitHub Actions main channel notifications (optional)
- `SLACK_CRITICAL_WEBHOOK_URL` - Critical failure notifications for deploy/security/runner (optional)

### Environment Variables
See individual workflow files for environment-specific configuration.

## Notification Architecture

Slack notifications use a 3-tier system via the centralized `notify-slack.yml` reusable workflow.

### Slack Notification Tiers

| Tier | Workflows | Behavior |
|------|-----------|----------|
| **CRITICAL** | deploy-staging, rollback | Start + End (both channels) |
| **IMPORTANT** | ci (main-staging/main PRs), backend-e2e, security-scan | Failures only |
| **SILENT** | All others (12 workflows) | GitHub Actions UI only |

**Estimated messages/day:** 2-5 (down from ~150)

**Channels:**
- Main channel (`SLACK_GITNOTIFY_WEBHOOK_URL`): Failures from Tier 1+2 workflows, start/end from Tier 1
- Critical channel (`SLACK_CRITICAL_WEBHOOK_URL`): Failures from deploy-staging, rollback, security-scan

### Deploy Preview

PRs targeting `main-staging` receive an automated comment showing:
- Services affected (API/Web/Infra)
- CI status
- Link to staging environment

**Adding notifications to a new workflow:** Follow the pattern in `deploy-staging.yml` (Tier 1) or `security-scan.yml` (Tier 2). Most workflows should stay in Tier 3 (silent).

## Self-Hosted ARM64 Runner

> **⚠️ Reality note (2026-07-28, #3331)** — this section had drifted from the
> deployed setup:
> - `ci.yml` runs **entirely on GitHub-hosted `ubuntu-latest`** — its
>   `select-runner` step hardcodes cloud (`ci.yml:74`). **CI does not use the
>   self-hosted runner.**
> - The self-hosted runner is **co-located on the Hetzner staging VPS
>   (~8GB ARM64)**, sharing the box with the app containers — **not** a dedicated
>   24GB Oracle VM. Today it runs only the deploy-staging orchestration (SSH),
>   rollback, e2e, and ops one-offs.
> - The co-location is being unwound (a build OOM already broke a deploy). See
>   [ADR-044 § Update 2026-07-28](../../docs/for-claude/architecture/adr/adr-044-self-hosted-arm64-runner.md)
>   and the #3331 spec. **Do not add build-heavy steps to the self-hosted runner.**

Workflows that still use the self-hosted runner select it via the static toggle:
```yaml
runs-on: ${{ vars.RUNNER && fromJSON(vars.RUNNER) || 'ubuntu-latest' }}
```
- **`vars.RUNNER` set** → self-hosted (co-located staging VPS, label `self-hosted,linux,ARM64`)
- **`vars.RUNNER` unset** → GitHub-hosted `ubuntu-latest`

**Architecture Decision**: [ADR-044](../../docs/for-claude/architecture/adr/adr-044-self-hosted-arm64-runner.md) — read the **2026-07-28 Update** for the current (drift-corrected) reality.

### Runner Specs (actual, 2026-07-28)

| Resource | Value |
|----------|-------|
| Host | Hetzner staging VPS (~8GB ARM64), **co-located with app containers** |
| Runner dir | `/home/deploy/actions-runner` (user `deploy`) |
| Memory | `MemoryMax=3G` cgroup cap (systemd override, #2019) |
| Jobs | deploy-staging orchestration, rollback, e2e, ops one-offs (**not** `ci.yml`) |

### ARM64 Exclusions

| Workflow | Job | Reason |
|----------|-----|--------|
| `security-scan.yml` | `codeql` | CodeQL CLI has no linux/arm64 binary |

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
> The self-hosted runner is being retired (Option E, #3331): `vars.RUNNER` has been
> cleared and all workflows run on GitHub-hosted. The `runner-health-check` /
> `runner-maintenance` / `monitor-runner-queue` workflows were removed. To
> re-introduce a self-hosted runner (Option D fallback), provision it from
> `infra/runner/cloud-init.yml` + `setup-vm.sh` + `setup-runner.sh` and re-add the
> `RUNNER` repo variable.

1. Check runner status (if any registered): GitHub → Settings → Actions → Runners
2. Full rollback to self-hosted: re-create the `RUNNER` variable (see Rollback Procedure above)

### Performance Issues
1. Check K6/Lighthouse reports in artifacts
2. Review API response times
3. Check for memory/resource constraints

### Security Alerts
1. Review CodeQL findings
2. Check dependency audit reports
3. Address HIGH/CRITICAL vulnerabilities first

---

**Last Updated**: 2026-03-20

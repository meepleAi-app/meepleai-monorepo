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
├── test-e2e.yml              # Full E2E suite (4-shard parallel)
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
- **Triggers**: Push/PR to main, main-dev, frontend-dev
- **Jobs**: Frontend (lint, typecheck, test, build), Backend (build, test), E2E critical paths
- **Features**: Path-based filtering, parallel execution, Codecov integration

### deploy-staging.yml - Staging Deployment
- **Triggers**: Push to `main-staging` branch, manual dispatch
- **Jobs**: Pre-deploy tests, Docker build, SSH/K8s deploy, validation
- **Features**: Automatic deployment (no approval), health checks

### deploy-production.yml - Production Deployment
- **Triggers**: Push/tag to `main` branch, manual dispatch
- **Jobs**: Staging verification, tests, Docker build, approval gate, blue-green deploy
- **Features**: Manual approval required, rollback capability, GitHub Release creation

## Testing Workflows

### test-e2e.yml - Full E2E Suite
- **Triggers**: Push/PR with web/api changes
- **Jobs**: 4-shard parallel Playwright tests, quality gate (≥90% pass rate)
- **Features**: Full browser matrix, coverage reports, PR comments

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
- **Triggers**: PR to main, main-dev
- **Jobs**: Validate source branch matches policy
- **Policy**: main ← main-dev only; main-dev ← frontend-dev, feature/*, fix/*, etc.

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
| test-e2e.yml | 10-15 min | Full 4-shard suite |
| test-performance.yml | 15-20 min | K6 + Lighthouse |
| test-visual.yml | 5-10 min | Playwright + Chromatic |
| security-scan.yml | 8-12 min | Full security suite |

## Configuration

### Required Secrets
- `CODECOV_TOKEN` - Code coverage uploads
- `CHROMATIC_PROJECT_TOKEN` - Chromatic visual testing
- `LHCI_GITHUB_APP_TOKEN` - Lighthouse CI (optional, falls back to GITHUB_TOKEN)
- `SLACK_WEBHOOK_URL` - Failure notifications (optional)

### Environment Variables
See individual workflow files for environment-specific configuration.

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

### Performance Issues
1. Check K6/Lighthouse reports in artifacts
2. Review API response times
3. Check for memory/resource constraints

### Security Alerts
1. Review CodeQL findings
2. Check dependency audit reports
3. Address HIGH/CRITICAL vulnerabilities first

---

**Last Updated**: 2026-01-26

# GitHub Actions Workflows

## Active Workflows

### Core CI/CD

- **ci.yml** - Main CI pipeline (Frontend + Backend + E2E)
  - Auto-runs on push/PR to main/frontend-dev
  - Path-based filtering for optimal performance
  - Parallel job execution

- **security.yml** - Security scanning
  - CodeQL SAST (C# + JavaScript)
  - Dependency vulnerability scanning
  - Secrets detection with Semgrep
  - Weekly scheduled scans

### Supporting Workflows

- **branch-policy.yml** - Branch protection enforcement
- **security-penetration-tests.yml** - Penetration testing
- **validate-workflows.yml** - Workflow syntax validation
- **dependabot-automerge.yml** - Auto-merge Dependabot PRs

## Disabled Workflows (*.disabled)

The following workflows were temporarily disabled during the CI/CD modernization:

- `e2e-*.yml.disabled` - Replaced by streamlined E2E in ci.yml
- `k6-*.yml.disabled` - Performance tests (will be re-enabled after CI stabilization)
- `lighthouse-ci.yml.disabled` - Lighthouse performance (will be integrated later)
- `migration-guard.yml.disabled` - EF Core migration checks (to be re-enabled)
- `storybook-deploy.yml.disabled` - Storybook deployment (to be re-enabled)

## Recent Changes (2025-12-26)

### Modernization Goals
1. ✅ Simplified CI pipeline (14 → 4 active workflows)
2. ✅ Fixed failing schema validation
3. ✅ Reduced CI execution time (~14min → ~8min estimated)
4. ✅ Improved maintainability and debugging

### Migration Notes
- Old workflows backed up as `*.disabled` files
- Can be safely deleted after 30 days of stable CI
- Re-enable specific workflows as needed by renaming back to `.yml`

## Workflow Execution Time Targets

| Workflow | Target | Actual |
|----------|--------|--------|
| Frontend | 3-5 min | TBD |
| Backend | 5-7 min | TBD |
| E2E | 3-5 min | TBD |
| Security (push) | 8-10 min | TBD |
| Security (weekly) | 15-20 min | TBD |

## Troubleshooting

### CI Failures
1. Check job logs in GitHub Actions UI
2. Review path filters in `ci.yml` if jobs are unexpectedly skipped
3. Verify secrets are configured: `CODECOV_TOKEN`, `POSTGRES_SERVICE_PASSWORD`

### Local Testing
```bash
# Install act (GitHub Actions local runner)
# https://github.com/nektos/act

# Test workflow locally
act -W .github/workflows/ci.yml -j frontend

# Dry run
act -W .github/workflows/ci.yml --dryrun
```

## Future Improvements
- [ ] Re-enable performance testing (K6)
- [ ] Re-enable Lighthouse CI
- [ ] Re-enable migration guard
- [ ] Add matrix testing for multiple Node/dotnet versions
- [ ] Implement workflow caching improvements

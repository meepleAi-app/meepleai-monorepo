# Codecov Integration Guide

This document describes the Codecov integration for automated coverage tracking in MeepleAI.

## Overview

Codecov provides:
- Automated coverage reports on every PR
- Coverage gates to prevent regressions
- Per-flag tracking for different code areas
- Historical trend dashboard

## Configuration

The Codecov configuration is in `.codecov.yml` at the repository root.

### Coverage Targets

| Area | Target | Threshold | Description |
|------|--------|-----------|-------------|
| **Project** | auto | 0.5% | Overall project coverage |
| **Patch** | 80% | 0% | New code in PRs |
| **Backend** | 90% | 0.5% | All backend code |
| **Backend Domain** | 95% | 0.5% | Domain entities (highest quality) |
| **Backend Handlers** | 90% | 0.5% | Application layer handlers |
| **Frontend** | 85% | 0.5% | All frontend code |
| **Frontend Stores** | 70% | 0.5% | Zustand state management |
| **Frontend Hooks** | 65% | 0.5% | React hooks |

### Coverage Flags

Flags track coverage for specific code areas:

```yaml
flags:
  backend:           # apps/api/src/Api/BoundedContexts/**
  backend-domain:    # **/Domain/**
  backend-handlers:  # **/Application/Handlers/**
  frontend:          # apps/web/src/**
  frontend-stores:   # apps/web/src/store/**
  frontend-hooks:    # apps/web/src/hooks/**
```

### Ignored Files

The following are excluded from coverage:
- Generated files (`*.g.cs`)
- Migrations (`**/Migrations/**`)
- Build outputs (`.next/`, `out/`, `obj/`, `bin/`)
- Type definitions (`*.d.ts`)
- Storybook files (`*.stories.tsx`)
- Coverage reports (`coverage/`)
- Entry points (`Program.cs`)

## CI/CD Integration

Coverage is uploaded in `.github/workflows/ci.yml`:

### Frontend Upload
```yaml
- name: Upload Coverage
  uses: codecov/codecov-action@v4
  with:
    files: apps/web/coverage/lcov.info
    flags: frontend
    token: ${{ secrets.CODECOV_TOKEN }}
```

### Backend Upload
```yaml
- name: Upload Coverage
  uses: codecov/codecov-action@v4
  with:
    files: |
      apps/api/coverage/unit-coverage.xml
      apps/api/coverage/integration-coverage.xml
    flags: backend
    token: ${{ secrets.CODECOV_TOKEN }}
```

## PR Comments

Codecov automatically adds comments to PRs showing:
- Coverage diff (lines covered/uncovered)
- Flag breakdown
- Files with coverage changes

Comment layout: `header, diff, flags, files, footer`

## Coverage Gates

PRs are blocked if:
1. **Project coverage** drops more than 0.5%
2. **Patch coverage** (new code) is below 80%
3. **Flag-specific** thresholds are violated

## Dashboard

Access the Codecov dashboard at:
- **Main**: https://codecov.io/gh/meepleAi-app/meepleai-monorepo
- **Backend flag**: https://codecov.io/gh/meepleAi-app/meepleai-monorepo?flag=backend
- **Frontend flag**: https://codecov.io/gh/meepleAi-app/meepleai-monorepo?flag=frontend

## Badges

README badges show current coverage:

```markdown
[![codecov](https://codecov.io/gh/meepleAi-app/meepleai-monorepo/graph/badge.svg)](https://codecov.io/gh/meepleAi-app/meepleai-monorepo)
[![Backend Coverage](https://codecov.io/gh/meepleAi-app/meepleai-monorepo/branch/main/graph/badge.svg?flag=backend)](https://codecov.io/gh/meepleAi-app/meepleai-monorepo?flag=backend)
[![Frontend Coverage](https://codecov.io/gh/meepleAi-app/meepleai-monorepo/branch/main/graph/badge.svg?flag=frontend)](https://codecov.io/gh/meepleAi-app/meepleai-monorepo?flag=frontend)
```

## Local Coverage

### Frontend
```bash
cd apps/web
pnpm test:coverage
# View: apps/web/coverage/lcov-report/index.html
```

### Backend
```bash
cd apps/api
dotnet test --collect:"XPlat Code Coverage"
# View: apps/api/TestResults/*/coverage.cobertura.xml
```

## Troubleshooting

### Coverage not uploading
1. Verify `CODECOV_TOKEN` is set in GitHub Secrets
2. Check CI logs for upload errors
3. Ensure coverage files exist at expected paths

### Coverage lower than expected
1. Check ignored files in `.codecov.yml`
2. Verify test suite is running completely
3. Check flag path patterns match your code structure

### PR comment not appearing
1. Verify `require_head: true` in codecov.yml
2. Check that base branch has coverage data
3. Ensure `after_n_builds` matches expected upload count

## Related Documentation

- [Codecov Docs](https://docs.codecov.com/)
- [CI/CD Pipeline](./ci-cd-pipeline.md)
- [Testing Guide](./README.md)

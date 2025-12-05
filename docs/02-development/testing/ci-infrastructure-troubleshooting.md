# CI Infrastructure Troubleshooting Guide

**Issue #1951**: Comprehensive guide to prevent and resolve CI test infrastructure failures

---

## Overview

This document provides systematic approaches to diagnosing and preventing CI test failures related to infrastructure, not code logic. Use this guide when tests pass locally but fail in CI environment.

---

## Common CI Failure Patterns

### 1. ECONNREFUSED Errors

**Symptom**: `TypeError: fetch failed ... cause: AggregateError ... code: 'ECONNREFUSED'`

**Root Causes**:
- API server not started or not reachable
- Service health checks failing or timing out
- Race conditions between service startup and test execution
- Missing API route mocks in E2E tests

**Solutions**:

#### A. For E2E Tests with SSR (Next.js)

**Problem**: Next.js server-side rendering attempts to fetch from API backend (`http://localhost:8080`) but API server is not running.

**Solution**: Add global API route mocks using Playwright's `page.route()`:

```typescript
// In E2E test file (e.g., accessibility.spec.ts)
test.beforeEach(async ({ page }) => {
  const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

  // Mock API endpoints that may be called during SSR
  await page.route(`${API_BASE}/api/v1/games**`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([]),
    });
  });

  await page.route(`${API_BASE}/api/v1/auth/me`, async route => {
    await route.fulfill({
      status: 401,
      contentType: 'application/json',
      body: JSON.stringify({ error: 'Unauthorized' }),
    });
  });
});
```

**Guard**: All E2E tests with Next.js pages MUST mock commonly-fetched API endpoints in `beforeEach()` hook.

#### B. For API Integration Tests

**Problem**: Health check timeout too short or API startup slow in CI environment.

**Solution**: Implement robust health check with:
- Longer timeout (120s minimum)
- Process validation (verify PID exists and is running)
- Detailed logging for debugging
- Health response content verification

```bash
# In .github/workflows/ci.yml
- name: Wait for API Health
  run: |
    MAX_ATTEMPTS=60  # 60 × 2s = 120s
    ATTEMPT=1

    while [ $ATTEMPT -le $MAX_ATTEMPTS ]; do
      echo "Health check attempt $ATTEMPT/$MAX_ATTEMPTS..."

      # Verify process is alive
      if ! ps -p $PID > /dev/null 2>&1; then
        echo "ERROR: API process died"
        cat api.log
        exit 1
      fi

      # Try health endpoint
      if curl -f -s http://localhost:8080/health > /dev/null 2>&1; then
        echo "✓ API ready after $(($ATTEMPT * 2))s"
        exit 0
      fi

      ATTEMPT=$((ATTEMPT + 1))
      sleep 2
    done

    echo "ERROR: Health timeout after 120s"
    tail -50 api.log
    exit 1
```

**Guard**: All CI jobs starting backend services MUST implement robust health checks with ≥120s timeout and process validation.

---

### 2. React Intl / i18n Test Failures

**Symptom**: `[React Intl] Could not find required 'intl' object. <IntlProvider> needs to exist in the component ancestry.`

**Root Cause**: Component uses `react-intl` (via `useIntl()` or `useTranslation()`) but test doesn't wrap component with `<IntlProvider>`.

**Solution**: Create reusable `renderWithIntl()` helper:

```typescript
// In __tests__/fixtures/common-fixtures.ts
import React from 'react';
import { render } from '@testing-library/react';
import { IntlProvider } from 'react-intl';

const testMessages = {
  'auth.oauth.separator': 'Or continue with',
  'auth.oauth.google': 'Continue with Google',
  'auth.oauth.discord': 'Continue with Discord',
  'auth.oauth.github': 'Continue with GitHub',
  // Add other required messages...
};

export const renderWithIntl = (
  ui: Parameters<typeof render>[0],
  options?: Parameters<typeof render>[1],
  messages: Record<string, string> = testMessages
) => {
  const Wrapper = ({ children }: { children: React.ReactNode }) =>
    React.createElement(
      IntlProvider,
      { locale: 'en', messages },
      children
    );

  return render(ui, { ...options, wrapper: Wrapper });
};
```

**Usage in tests**:
```typescript
// Instead of:
render(<OAuthButtons />);

// Use:
renderWithIntl(<OAuthButtons />);
```

**Guard**: All component tests using `useTranslation()` or `react-intl` MUST use `renderWithIntl()` instead of `render()`.

---

### 3. Service Container Health Checks

**Best Practices**:

```yaml
services:
  postgres:
    image: postgres:16-alpine
    options: >-
      --health-cmd pg_isready
      --health-interval 5s
      --health-timeout 3s
      --health-retries 5
    ports:
      - 5432:5432

  qdrant:
    image: qdrant/qdrant:v1.12.4
    # Add explicit health check validation
    ports:
      - 6333:6333

  redis:
    image: redis:7-alpine
    options: >-
      --health-cmd "redis-cli ping"
      --health-interval 5s
      --health-timeout 3s
      --health-retries 5
    ports:
      - 6379:6379
```

**Verify in steps**:
```yaml
- name: Verify Services Ready
  run: |
    for i in {1..20}; do
      if curl -f http://localhost:6333/healthz > /dev/null 2>&1; then
        echo "Qdrant ready after $i attempts"
        break
      fi
      sleep 1
    done
```

**Guard**: All GitHub Actions service containers MUST have health checks configured or explicit readiness verification in steps.

---

## Troubleshooting Workflow

### Step 1: Identify Failure Category
```bash
# Check CI logs for keywords:
grep -i "ECONNREFUSED" ci.log         # → Connection failures
grep -i "timeout" ci.log              # → Timing issues
grep -i "IntlProvider" ci.log         # → i18n setup
grep -i "health" ci.log               # → Service readiness
```

### Step 2: Compare Local vs CI Environment
| Aspect | Local | CI | Fix Strategy |
|--------|-------|-----|--------------|
| API Server | Dev mode | Production build | Match build modes |
| Service startup | Slow OK | Strict timeouts | Increase timeouts |
| Network | Localhost | Container network | Verify service URLs |
| Parallelization | Multiple workers | Single worker | Disable parallel in CI |

### Step 3: Add Defensive Logging
```yaml
- name: Debug Step
  if: failure()
  run: |
    echo "::group::Environment Info"
    docker ps -a
    netstat -tuln | grep LISTEN
    ps aux | grep dotnet
    echo "::endgroup::"

    echo "::group::Service Logs"
    cat api.log || echo "No API logs"
    echo "::endgroup::"
```

### Step 4: Implement Guards

**Code Guards**:
```typescript
// ⛔ GUARD: Always wrap i18n components with IntlProvider in tests
// Issue #1951: Prevents "intl object not found" errors
export const renderWithIntl = (ui, options?, messages?) => { /* ... */ };
```

**CI Guards**:
```yaml
# ⛔ GUARD: Health checks must timeout >= 120s
# Issue #1951: Prevents false failures from slow startup
- name: Wait for API Health
  run: |
    MAX_ATTEMPTS=60  # 120s total
    # ... robust implementation
```

**Documentation Guards**:
- Add this troubleshooting guide to docs
- Reference issue numbers in code comments
- Maintain CI failure runbook

---

## Prevention Checklist

Before merging changes affecting CI:

- [ ] All E2E tests with SSR pages mock API routes in `beforeEach()`
- [ ] All i18n component tests use `renderWithIntl()` wrapper
- [ ] API health checks have ≥120s timeout + process validation
- [ ] Service containers have health check configurations
- [ ] Failure scenarios logged with detailed diagnostics
- [ ] Local tests pass with same configuration as CI
- [ ] CI logs reviewed for warnings (not just errors)

---

## Related Documentation

- [Testing Strategy](./testing-strategy.md)
- [E2E Test Patterns](./test-writing-guide.md)
- [CI/CD Pipeline](../../05-operations/ci-cd-pipeline.md)

---

**Last Updated**: 2025-12-05 (Issue #1951)
**Maintainer**: Engineering Team

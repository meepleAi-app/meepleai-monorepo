# Fix Demo User Login Tests

**Issue ID**: E2E-005
**Data**: 2025-11-20
**Priorità**: 🔴 ALTA
**Categoria**: Test Completeness
**Effort Stimato**: 4-6 ore
**Status**: 🔴 Not Started

---

## 📋 Problem Description

Il file `demo-user-login.spec.ts` è **completamente skipped** con `test.describe.skip()`. Contiene 4 test per login flow con utenti demo (user, editor, admin) ma **nessuno viene eseguito**.

```typescript
test.describe.skip('Demo User Login', () => {
  // ⚠️ SKIPPED: These tests require the full backend API stack
  // The Playwright config only starts the Next.js frontend
});
```

**Motivo**: Test richiedono backend reale (database, auth), ma Playwright config avvia solo frontend.

**Impatto**: Gap di coverage del ~5% per user login flows reali.

---

## 🎯 Impact & Risks

### Impatto
- **Test Coverage**: Login flow reale non testato (solo mocked)
- **Integration Gaps**: Nessun test end-to-end con backend reale
- **Confidence**: Team non sa se demo login funziona realmente

### Rischi
🔴 **Alto**: Demo login potrebbe essere rotto in produzione
🟡 **Medio**: Onboarding difficile (demo users non funzionano)
🟡 **Medio**: CI non valida integration completa

---

## 📊 Current Situation

### Test Skipped

```typescript
// apps/web/e2e/demo-user-login.spec.ts
test.describe.skip('Demo User Login', () => {
  test('user@meepleai.dev can log in successfully', async ({ page }) => { ... });
  test('editor@meepleai.dev can log in and access editor features', async ({ page }) => { ... });
  test('admin@meepleai.dev can log in and access admin panel', async ({ page }) => { ... });
  test('shows error for invalid credentials', async ({ page }) => { ... });
});
```

**Total**: 4 test skipped
**Lines**: ~230 lines di test code non utilizzato

---

## ✅ Solution Options

### Option 1: Use Mocking (RECOMMENDED - Quick Win)

**Approccio**: Convertire test a usare mock auth invece di backend reale

```typescript
// ✅ BEFORE: Skipped, requires real backend
test.describe.skip('Demo User Login', () => { ... });

// ✅ AFTER: Re-enabled with mocking
test.describe('Demo User Login (Mocked)', () => {
  test('user@meepleai.dev can log in successfully', async ({ page }) => {
    // Use centralized mock setup
    const authController = await setupMockAuth(page, {
      role: 'User',
      email: 'user@meepleai.dev',
      authState: 'unauthenticated' // Start unauthenticated
    });

    await page.goto('/');
    await page.getByTestId('hero-get-started').click();

    // Fill login form
    await page.getByLabel('Email').fill('user@meepleai.dev');
    await page.getByLabel('Password').fill('Demo123!');
    await page.getByRole('button', { name: /login/i }).click();

    // Mock will auto-authenticate on login
    await expect(page).toHaveURL('/chat');
    await expect(page.getByRole('heading', { name: /chat/i })).toBeVisible();
  });
});
```

**Pro**:
- ✅ Quick (2-4 ore)
- ✅ No CI changes needed
- ✅ Stable (no backend dependency)

**Con**:
- ❌ Non testa backend reale
- ❌ Non testa database integration

### Option 2: Separate Integration Test Suite (IDEAL - Long Term)

**Approccio**: Creare suite separata che avvia backend + frontend

**File**: `scripts/run-integration-tests.ts`

```typescript
import { spawn } from 'child_process';
import { test as playwrightTest } from '@playwright/test';

async function startBackend() {
  return spawn('dotnet', ['run'], {
    cwd: 'apps/api/src/Api',
    env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Test' }
  });
}

async function startFrontend() {
  return spawn('pnpm', ['dev'], {
    cwd: 'apps/web'
  });
}

async function runIntegrationTests() {
  const backend = await startBackend();
  await waitForBackend('http://localhost:5080/health');

  const frontend = await startFrontend();
  await waitForFrontend('http://localhost:3000');

  // Run integration tests
  await playwrightTest.run(['demo-user-login.spec.ts']);

  backend.kill();
  frontend.kill();
}
```

**Pro**:
- ✅ Testa backend reale
- ✅ Testa database integration
- ✅ Full E2E confidence

**Con**:
- ❌ Più complesso (6-8 ore)
- ❌ CI deve avviare DB + backend + frontend
- ❌ Più lento (~5min vs ~30s)

### Option 3: Hybrid Approach (RECOMMENDED)

**Approccio**:
- **E2E Suite (existing)**: Use mocking per velocità
- **Integration Suite (new)**: Subset di test con backend reale (run weekly/pre-release)

```json
// package.json
{
  "scripts": {
    "test:e2e": "playwright test",  // Mocked, fast
    "test:e2e:integration": "tsx scripts/run-integration-tests.ts",  // Real backend, slow
    "test:e2e:full": "pnpm test:e2e && pnpm test:e2e:integration"
  }
}
```

---

## 📝 Implementation Recommendation: Option 1 (Quick Win)

### Step 1: Convert to Mocked Tests (Day 1)

```typescript
// Remove .skip, use centralized mock
test.describe('Demo User Login', () => {
  test.beforeEach(async ({ page }) => {
    // Setup will be done per test
  });

  test('user@meepleai.dev can log in successfully', async ({ page }) => {
    await setupMockAuth(page, { role: 'User', email: 'user@meepleai.dev', authState: 'unauthenticated' });
    // ... rest of test
  });

  // Repeat for editor, admin, invalid credentials
});
```

### Step 2: Remove force: true (Already covered in E2E-002)

```typescript
// ❌ BEFORE
await page.getByTestId('hero-get-started').click({ force: true });

// ✅ AFTER
await waitForClickable(page.getByTestId('hero-get-started'));
await page.getByTestId('hero-get-started').click();
```

### Step 3: Validate & Re-enable (Day 2)

```bash
# Run tests locally 5 times
for i in {1..5}; do pnpm test:e2e demo-user-login.spec.ts; done

# Run in CI
git commit -m "fix(e2e): Re-enable demo login tests with mocking"
```

---

## 📝 Implementation Checklist

### Quick Win (Option 1) - 1-2 Days
- [ ] Remove `test.describe.skip`
- [ ] Replace real auth with `setupMockAuth`
- [ ] Remove all `force: true` clicks
- [ ] Replace `waitForTimeout` with proper waits
- [ ] Run tests 5x locally
- [ ] Run tests in CI
- [ ] Update documentation

### Long Term (Option 3) - 1 Week
- [ ] Implement Option 1 first
- [ ] Create `scripts/run-integration-tests.ts`
- [ ] Configure CI for integration tests
- [ ] Add integration tests to weekly schedule
- [ ] Document both test suites

---

## ✅ Acceptance Criteria

### Must Have
- [ ] All 4 tests re-enabled and passing
- [ ] Tests use centralized `setupMockAuth`
- [ ] Zero `force: true` in file
- [ ] Tests pass 10/10 times locally
- [ ] Tests pass in CI

### Should Have
- [ ] Documentation updated
- [ ] Integration test strategy documented
- [ ] README includes both test types

---

## 📁 Files

### Modify
- `apps/web/e2e/demo-user-login.spec.ts` (remove skip, use mocks)

### Reference
- `apps/web/e2e/fixtures/auth.ts` (use setupMockAuth)
- `apps/web/e2e/README.md` (update docs)

---

**Created**: 2025-11-20
**Owner**: QA Team

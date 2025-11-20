# Implement Error Handling Tests

**Issue ID**: E2E-001
**Data**: 2025-11-20
**Priorità**: 🔴 CRITICA
**Categoria**: Test Completeness
**Effort Stimato**: 12-16 ore
**Status**: 🔴 Not Started

---

## 📋 Problem Description

Il file `apps/web/e2e/error-handling.spec.ts` esiste ma è completamente **stub/incomplete**. Contiene la struttura dei test ma **nessuna assertion** funzionante.

```typescript
/**
 * ⚠️ STATUS: These tests are STUBBED and incomplete (Phase 5 deliverable)
 * They define the test structure and scenarios but lack complete assertions.
 */
```

Questo rappresenta un **gap critico** nella test coverage per scenari di errore che sono fondamentali per la robustezza dell'applicazione.

---

## 🎯 Impact & Risks

### Impatto
- **Test Coverage**: Gap del ~15-20% nella coverage E2E
- **Quality Assurance**: Nessuna verifica automatica di error handling
- **User Experience**: Bug in error flows potrebbero raggiungere produzione
- **Regression**: Cambiamenti potrebbero rompere error handling senza detection

### Rischi
🔴 **Alto**: Bug in production su error flows (401, 404, 500, network errors)
🔴 **Alto**: UX degradata per scenari di errore non testati
🟡 **Medio**: Difficoltà debug senza test di error scenarios
🟡 **Medio**: Tempo sprecato in manual testing di error flows

---

## 📊 Current Situation

### File Attuale: `apps/web/e2e/error-handling.spec.ts`

```typescript
test.describe('Error Handling E2E Tests', () => {
  test.describe('Network errors', () => {
    test('should display error toast on network failure', async ({ page, context }) => {
      await context.route('**/api/**', (route) => {
        route.abort('failed');
      });

      await page.goto('/');
      await page.waitForLoadState('networkidle');

      // ❌ NO ASSERTIONS - Test è incompleto
      // Check for error indication (adjust selectors based on your actual implementation)
    });
  });
});
```

**Stato**:
- ✅ Struttura test definita (11 test scenarios)
- ❌ **0 test completamente implementati**
- ❌ Nessuna assertion funzionante
- ❌ Nessun selettore UI definito
- ❌ Nessuna verifica di error recovery flows

---

## ✅ Implementation Recommendations

### 1. Network Errors (Priorità Alta)

```typescript
test.describe('Network Errors', () => {
  test('should display error toast on complete network failure', async ({ page, context }) => {
    // Setup: Block ALL API requests
    await context.route('**/api/**', (route) => route.abort('failed'));

    // Navigate to page that makes API call
    await page.goto('/chat');
    await page.waitForLoadState('networkidle');

    // Trigger action that makes API call
    await page.getByRole('button', { name: /ask|invia/i }).click();

    // ✅ ASSERT: Error toast appears
    const errorToast = page.getByRole('alert').filter({ hasText: /network|connessione/i });
    await expect(errorToast).toBeVisible({ timeout: 5000 });

    // ✅ ASSERT: Error message is user-friendly
    await expect(errorToast).toContainText(/retry|riprova/i);
  });

  test('should auto-retry failed requests (3 attempts)', async ({ page, context }) => {
    let requestCount = 0;

    await context.route('**/api/v1/games', (route) => {
      requestCount++;
      if (requestCount < 3) {
        // Fail first 2 attempts
        route.fulfill({ status: 500, body: JSON.stringify({ error: 'Server Error' }) });
      } else {
        // Succeed on 3rd attempt
        route.fulfill({
          status: 200,
          body: JSON.stringify([{ id: '1', name: 'Chess' }])
        });
      }
    });

    await page.goto('/games');
    await page.waitForLoadState('networkidle');

    // ✅ ASSERT: Request was retried exactly 3 times
    expect(requestCount).toBe(3);

    // ✅ ASSERT: UI shows success after retries
    await expect(page.getByText('Chess')).toBeVisible();
  });

  test('should show offline mode indicator when network is down', async ({ page, context }) => {
    // Simulate offline
    await context.setOffline(true);

    await page.goto('/');

    // ✅ ASSERT: Offline indicator visible
    const offlineIndicator = page.getByRole('status', { name: /offline|disconnesso/i });
    await expect(offlineIndicator).toBeVisible({ timeout: 5000 });

    // Restore network
    await context.setOffline(false);
    await page.waitForTimeout(1000);

    // ✅ ASSERT: Offline indicator disappears
    await expect(offlineIndicator).not.toBeVisible();
  });
});
```

### 2. HTTP Status Errors (Priorità Alta)

```typescript
test.describe('HTTP Status Errors', () => {
  test('should redirect to login on 401 Unauthorized', async ({ page, context }) => {
    await context.route('**/api/v1/chat', (route) => {
      route.fulfill({
        status: 401,
        body: JSON.stringify({ error: 'Unauthorized' })
      });
    });

    await page.goto('/chat');

    // ✅ ASSERT: Redirected to /login
    await expect(page).toHaveURL(/\/login/, { timeout: 5000 });

    // ✅ ASSERT: Login modal or page visible
    await expect(page.getByRole('heading', { name: /login|accedi/i })).toBeVisible();
  });

  test('should show 404 error page for not found resources', async ({ page, context }) => {
    await context.route('**/api/v1/games/nonexistent', (route) => {
      route.fulfill({
        status: 404,
        body: JSON.stringify({ error: 'Game not found' })
      });
    });

    await page.goto('/games/nonexistent');

    // ✅ ASSERT: 404 page or error message visible
    const notFoundMessage = page.getByText(/not found|non trovato|404/i);
    await expect(notFoundMessage).toBeVisible();

    // ✅ ASSERT: User can navigate back
    const backButton = page.getByRole('link', { name: /back|indietro|home/i });
    await expect(backButton).toBeVisible();
  });

  test('should display generic error for 500 Internal Server Error', async ({ page, context }) => {
    await context.route('**/api/v1/chat', (route) => {
      route.fulfill({
        status: 500,
        body: JSON.stringify({ error: 'Internal Server Error' })
      });
    });

    await page.goto('/chat');
    await page.getByRole('textbox', { name: /message|messaggio/i }).fill('Test question');
    await page.getByRole('button', { name: /send|invia/i }).click();

    // ✅ ASSERT: Error toast appears
    const errorToast = page.getByRole('alert');
    await expect(errorToast).toBeVisible({ timeout: 5000 });

    // ✅ ASSERT: Error message is user-friendly (NOT technical stack trace)
    await expect(errorToast).toContainText(/something went wrong|errore/i);
    await expect(errorToast).not.toContainText(/stack trace|exception/i);
  });

  test('should handle 429 Rate Limit with proper message', async ({ page, context }) => {
    await context.route('**/api/v1/chat', (route) => {
      route.fulfill({
        status: 429,
        headers: { 'Retry-After': '60' },
        body: JSON.stringify({ error: 'Rate limit exceeded' })
      });
    });

    await page.goto('/chat');
    await page.getByRole('textbox').fill('Question');
    await page.getByRole('button', { name: /send/i }).click();

    // ✅ ASSERT: Rate limit message visible
    const rateLimitMessage = page.getByRole('alert').filter({
      hasText: /rate limit|too many requests|troppi/i
    });
    await expect(rateLimitMessage).toBeVisible();

    // ✅ ASSERT: Retry time mentioned
    await expect(rateLimitMessage).toContainText(/60|minute|minuto/i);
  });
});
```

### 3. Client-Side Errors (Priorità Media)

```typescript
test.describe('Client-Side Errors', () => {
  test('should catch and display JavaScript errors', async ({ page }) => {
    // Listen for console errors
    const errors: string[] = [];
    page.on('pageerror', (error) => {
      errors.push(error.message);
    });

    // Inject code that throws error
    await page.goto('/');
    await page.evaluate(() => {
      throw new Error('Test error');
    });

    // ✅ ASSERT: Error was caught
    expect(errors.length).toBeGreaterThan(0);
    expect(errors[0]).toContain('Test error');

    // ✅ ASSERT: Error boundary shows fallback UI (if implemented)
    // await expect(page.getByText(/something went wrong/i)).toBeVisible();
  });

  test('should validate form inputs and show errors', async ({ page }) => {
    await page.goto('/login');

    // Submit empty form
    await page.getByRole('button', { name: /login|accedi/i }).click();

    // ✅ ASSERT: Validation errors visible
    await expect(page.getByText(/email.*required|email.*obbligatorio/i)).toBeVisible();
    await expect(page.getByText(/password.*required|password.*obbligatorio/i)).toBeVisible();

    // Fill invalid email
    await page.getByLabel(/email/i).fill('invalid-email');
    await page.getByRole('button', { name: /login/i }).click();

    // ✅ ASSERT: Email format error
    await expect(page.getByText(/valid email|email valida/i)).toBeVisible();
  });
});
```

### 4. Error Recovery Flows (Priorità Alta)

```typescript
test.describe('Error Recovery', () => {
  test('should allow user to retry after error', async ({ page, context }) => {
    let requestCount = 0;

    await context.route('**/api/v1/chat', (route) => {
      requestCount++;
      if (requestCount === 1) {
        // First attempt fails
        route.fulfill({ status: 500 });
      } else {
        // Retry succeeds
        route.fulfill({
          status: 200,
          body: JSON.stringify({ answer: 'Success!' })
        });
      }
    });

    await page.goto('/chat');
    await page.getByRole('textbox').fill('Question');
    await page.getByRole('button', { name: /send/i }).click();

    // Wait for error toast
    const errorToast = page.getByRole('alert');
    await expect(errorToast).toBeVisible();

    // ✅ ASSERT: Retry button visible
    const retryButton = errorToast.getByRole('button', { name: /retry|riprova/i });
    await expect(retryButton).toBeVisible();

    // Click retry
    await retryButton.click();

    // ✅ ASSERT: Second attempt succeeds
    await expect(page.getByText('Success!')).toBeVisible({ timeout: 5000 });
  });

  test('should clear error state after successful action', async ({ page }) => {
    await page.goto('/login');

    // Trigger error
    await page.getByLabel(/email/i).fill('wrong@email.com');
    await page.getByLabel(/password/i).fill('wrongpass');
    await page.getByRole('button', { name: /login/i }).click();

    // Error should appear
    await expect(page.getByRole('alert')).toBeVisible();

    // Fix and retry
    await page.getByLabel(/email/i).fill('user@meepleai.dev');
    await page.getByLabel(/password/i).fill('Demo123!');
    await page.getByRole('button', { name: /login/i }).click();

    // ✅ ASSERT: Error cleared after success
    await expect(page.getByRole('alert')).not.toBeVisible({ timeout: 5000 });
    await expect(page).toHaveURL(/\/(chat|dashboard|games)/);
  });
});
```

---

## 📝 Implementation Checklist

### Phase 1: Core Error Types (Week 1)
- [ ] Implement network error tests (3 tests)
- [ ] Implement 401 Unauthorized test
- [ ] Implement 404 Not Found test
- [ ] Implement 500 Internal Error test
- [ ] Implement 429 Rate Limit test

### Phase 2: Client & Validation (Week 1-2)
- [ ] Implement JavaScript error catching test
- [ ] Implement form validation error tests (2-3 tests)
- [ ] Implement boundary error tests

### Phase 3: Recovery Flows (Week 2)
- [ ] Implement retry mechanism tests
- [ ] Implement error state cleanup tests
- [ ] Implement offline/online transition tests

### Phase 4: Edge Cases (Week 2)
- [ ] Implement timeout error tests
- [ ] Implement CORS error tests
- [ ] Implement partial response error tests
- [ ] Implement malformed JSON error tests

---

## ✅ Acceptance Criteria

### Must Have
- [ ] Tutti i 15+ test scenarios implementati e passanti
- [ ] Ogni test ha almeno 2-3 assertions significative
- [ ] Error toast/notification selectors definiti
- [ ] Test coverage error flows > 80%
- [ ] Nessun test skipped o stubbed
- [ ] Documentazione inline completa

### Should Have
- [ ] Test per ogni HTTP status code rilevante (401, 404, 500, 429)
- [ ] Test per network failures (offline, timeout, abort)
- [ ] Test per client-side errors (JS exceptions, validation)
- [ ] Test per error recovery flows
- [ ] Test stabili (>95% success rate in CI)

### Nice to Have
- [ ] Visual regression test per error states
- [ ] Screenshot capture su test failure
- [ ] Error telemetry verification
- [ ] Accessibility testing per error UI

---

## 📁 Files to Modify

### Primary
- `apps/web/e2e/error-handling.spec.ts` (main implementation)

### Supporting
- `apps/web/e2e/fixtures/errors.ts` (new: error mocking helpers)
- `apps/web/e2e/README.md` (update: error handling section)

### UI Components (verify exist)
- `apps/web/src/components/ui/toast.tsx` (error toasts)
- `apps/web/src/components/ui/alert.tsx` (error alerts)
- `apps/web/src/components/ErrorBoundary.tsx` (error boundary)

---

## 🧪 Testing Strategy

### Unit Tests (Component Level)
```typescript
// Verify error UI components exist and work
describe('ErrorToast', () => {
  it('should display error message', () => { ... });
  it('should show retry button when retryable', () => { ... });
});
```

### E2E Tests (Full Flow)
```typescript
// Verify end-to-end error handling
test('Network failure → Error toast → Retry → Success', async () => { ... });
```

### Manual Testing
- [ ] Test su browser diversi (Chrome, Firefox, Safari)
- [ ] Test su mobile viewport
- [ ] Test con DevTools Network throttling
- [ ] Test con DevTools Offline mode

---

## 🔗 Related Issues

- [Add Negative Test Scenarios](./add-negative-test-scenarios.md) - Complementare
- [Improve Streaming Test Stability](./improve-streaming-test-stability.md) - Error handling per streaming
- [Add RBAC Authorization Tests](./add-rbac-authorization-tests.md) - 403 Forbidden errors

---

## 📚 References

- [Playwright Error Handling](https://playwright.dev/docs/test-assertions#error-assertions)
- [E2E Error Testing Best Practices](https://testingjavascript.com/lessons/test-error-states)
- [Toast Component Documentation](../../04-frontend/components/toast.md)

---

**Created**: 2025-11-20
**Last Updated**: 2025-11-20
**Owner**: QA Team
**Reviewers**: Frontend Team, Product

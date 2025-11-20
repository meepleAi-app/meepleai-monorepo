# Remove Force True Clicks

**Issue ID**: E2E-002
**Data**: 2025-11-20
**Priorità**: 🔴 ALTA
**Categoria**: Test Stability
**Effort Stimato**: 8-12 ore
**Status**: 🔴 Not Started

---

## 📋 Problem Description

La suite E2E contiene **32 occorrenze** di `click({ force: true })` che bypassano i controlli di visibilità e clickability di Playwright.

```typescript
// ❌ ANTI-PATTERN: Force click bypassa controlli
await page.getByRole('button').click({ force: true });
```

Questo è un **anti-pattern** che:
- Nasconde bug reali (elementi overlay, z-index issues, animation timing)
- Rende test meno affidabili (clicka elementi non realmente clickable dall'utente)
- Maschera problemi UI che dovrebbero essere risolti
- Crea falsi positivi (test passa ma feature è broken in produzione)

---

## 🎯 Impact & Risks

### Impatto
- **Test Reliability**: Test passano ma non verificano comportamento reale utente
- **Bug Detection**: Bug UI (overlay, z-index, timing) non vengono catturati
- **UX Quality**: Problemi di interazione non vengono rilevati
- **False Confidence**: Team pensa feature funzioni ma può essere rotta

### Rischi
🔴 **Critico**: Bug UI raggiungono produzione (es. bottoni non clickable)
🔴 **Alto**: Regression non rilevate (UI change rompe interazione ma test passa)
🟡 **Medio**: Debugging difficile (test passa localmente ma fail in CI)
🟡 **Medio**: Manutenzione costosa (force: true nasconde root cause)

---

## 📊 Current Situation

### Occorrenze per File

```bash
$ grep -r "force: true" apps/web/e2e/*.spec.ts | wc -l
32
```

**Breakdown**:
- `demo-user-login.spec.ts`: 11 occorrenze
- `chat-streaming.spec.ts`: 8 occorrenze
- `auth-2fa-complete.spec.ts`: 4 occorrenze
- `pdf-upload-journey.spec.ts`: 3 occorrenze
- Altri file: 6 occorrenze

### Esempi di Uso Attuale

```typescript
// File: demo-user-login.spec.ts
await page.getByTestId('hero-get-started').click({ force: true });
await page.locator('form button[type="submit"]').click({ force: true });

// File: chat-streaming.spec.ts
await page.click('button[type="submit"]', { force: true });
await stopButton.click({ force: true });

// File: auth-2fa-complete.spec.ts
await page.getByRole('button', { name: /enable 2fa/i }).click({ force: true });
```

**Motivi comuni** (da commenti nel codice):
1. "nextjs-portal overlay" - Dialog/Modal con overlay z-index issue
2. "Animation timing" - Element not ready durante transition
3. "Race condition" - Element appare/scompare durante test

---

## ✅ Implementation Recommendations

### Strategia di Rimozione

**Approccio**: Non rimuovere semplicemente `force: true`, ma **risolvere il problema sottostante**.

### Pattern 1: Wait for Element to be Actionable

```typescript
// ❌ BEFORE: Force click
await page.getByRole('button', { name: 'Submit' }).click({ force: true });

// ✅ AFTER: Wait for element to be clickable
const submitButton = page.getByRole('button', { name: 'Submit' });
await submitButton.waitFor({ state: 'visible' });
await submitButton.waitFor({ state: 'attached' }); // Ensure in DOM
await expect(submitButton).toBeEnabled(); // Ensure not disabled
await submitButton.click(); // Now safe to click without force
```

### Pattern 2: Wait for Overlay to Disappear

```typescript
// ❌ BEFORE: Force click through overlay
await page.locator('form button').click({ force: true });

// ✅ AFTER: Wait for overlay to disappear
// Option A: Wait for specific overlay element
await page.locator('[data-nextjs-portal]').waitFor({ state: 'detached' });
await page.locator('form button').click();

// Option B: Wait for button to not be covered
await page.waitForFunction(() => {
  const button = document.querySelector('form button');
  if (!button) return false;
  const rect = button.getBoundingClientRect();
  const elementAtPoint = document.elementFromPoint(
    rect.left + rect.width / 2,
    rect.top + rect.height / 2
  );
  return elementAtPoint === button || button.contains(elementAtPoint);
});
await page.locator('form button').click();
```

### Pattern 3: Fix Animation Timing

```typescript
// ❌ BEFORE: Force click during animation
await page.getByTestId('hero-button').click({ force: true });

// ✅ AFTER: Disable animations globally in config
// playwright.config.ts
export default defineConfig({
  use: {
    // Disable CSS animations
    launchOptions: {
      args: ['--disable-blink-features=AutomationControlled']
    }
  }
});

// Or in test setup
test.beforeEach(async ({ page }) => {
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.addStyleTag({
    content: `
      *, *::before, *::after {
        animation-duration: 0s !important;
        animation-delay: 0s !important;
        transition-duration: 0s !important;
        transition-delay: 0s !important;
      }
    `
  });
});

// Then click normally
await page.getByTestId('hero-button').click();
```

### Pattern 4: Use Custom Wait Helper

```typescript
// Create: apps/web/e2e/helpers/wait-for-clickable.ts
export async function waitForClickable(locator: Locator, timeout = 10000) {
  await locator.waitFor({ state: 'visible', timeout });

  // Wait for no overlays covering element
  await locator.page().waitForFunction(
    (selector) => {
      const element = document.querySelector(selector);
      if (!element) return false;

      const rect = element.getBoundingClientRect();
      const x = rect.left + rect.width / 2;
      const y = rect.top + rect.height / 2;
      const topElement = document.elementFromPoint(x, y);

      return topElement === element || element.contains(topElement);
    },
    await locator.evaluateHandle(el => {
      // Generate unique selector
      return el.getAttribute('data-testid') || el.id || el.className;
    }),
    { timeout }
  );

  // Ensure not disabled
  await expect(locator).toBeEnabled({ timeout: 1000 });
}

// Usage
import { waitForClickable } from './helpers/wait-for-clickable';

const button = page.getByRole('button', { name: 'Submit' });
await waitForClickable(button);
await button.click(); // No force needed
```

### Pattern 5: Fix Root Cause in UI Code

Alcuni casi richiedono fix al codice UI, non al test:

```typescript
// Se dialog/modal causa overlay issues

// ❌ UI CODE ISSUE: Dialog senza proper z-index management
<Dialog>
  <form>
    <button type="submit">Submit</button> {/* Covered by overlay */}
  </form>
</Dialog>

// ✅ FIX UI CODE: Proper portal rendering + z-index
// apps/web/src/components/ui/dialog.tsx
<DialogPrimitive.Portal>
  <DialogPrimitive.Overlay className="z-50" /> {/* Explicit z-index */}
  <DialogPrimitive.Content className="z-50"> {/* Same z-index level */}
    <form>
      <button type="submit">Submit</button> {/* Now clickable */}
    </form>
  </DialogPrimitive.Content>
</DialogPrimitive.Portal>
```

---

## 📝 Implementation Checklist

### Phase 1: Analyze (Week 1, Day 1-2)
- [ ] Audit all 32 occurrences
- [ ] Categorize by root cause:
  - [ ] Overlay issues (NextJS portal, Dialog, Modal)
  - [ ] Animation timing
  - [ ] Race conditions
  - [ ] Other
- [ ] Identify UI code fixes needed vs test fixes

### Phase 2: Fix UI Issues (Week 1, Day 3-4)
- [ ] Fix Dialog/Modal z-index issues
- [ ] Fix animation timing issues in components
- [ ] Add proper `data-testid` where missing
- [ ] Review with frontend team

### Phase 3: Update Tests (Week 2, Day 1-3)
- [ ] Implement `waitForClickable` helper
- [ ] Remove `force: true` from demo-user-login.spec.ts (11)
- [ ] Remove `force: true` from chat-streaming.spec.ts (8)
- [ ] Remove `force: true` from auth-2fa-complete.spec.ts (4)
- [ ] Remove `force: true` from pdf-upload-journey.spec.ts (3)
- [ ] Remove `force: true` from remaining files (6)

### Phase 4: Validate (Week 2, Day 4-5)
- [ ] Run all E2E tests locally (3+ runs)
- [ ] Run all E2E tests in CI (5+ runs)
- [ ] Verify stability (>95% pass rate)
- [ ] Document any remaining `force: true` with justification

---

## ✅ Acceptance Criteria

### Must Have
- [ ] `force: true` ridotto da 32 → <5 occorrenze
- [ ] Tutti i test passano senza `force: true` (>95% success rate)
- [ ] Helper `waitForClickable` implementato e documentato
- [ ] Nessun nuovo `force: true` aggiunto (enforced in code review)

### Should Have
- [ ] Zero `force: true` (goal ottimale)
- [ ] UI code fixes documentati
- [ ] ESLint rule per bloccare `force: true`
- [ ] Documentazione pattern in README

### Nice to Have
- [ ] Custom Playwright matcher `toBeClickable()`
- [ ] Visual regression test per elementi con overlay issues
- [ ] CI fail se `force: true` trovato

---

## 📁 Files to Modify

### Test Files (32 occorrences)
- `apps/web/e2e/demo-user-login.spec.ts` (11) 🔴 Priorità
- `apps/web/e2e/chat-streaming.spec.ts` (8) 🔴 Priorità
- `apps/web/e2e/auth-2fa-complete.spec.ts` (4)
- `apps/web/e2e/pdf-upload-journey.spec.ts` (3)
- Altri 6 file (1-2 occorrences ciascuno)

### New Helper
- `apps/web/e2e/helpers/wait-for-clickable.ts` (new)

### UI Components (potential fixes)
- `apps/web/src/components/ui/dialog.tsx`
- `apps/web/src/app/login/page.tsx`
- `apps/web/src/app/chat/page.tsx`

### Config
- `apps/web/playwright.config.ts` (disable animations globally)
- `apps/web/.eslintrc.js` (add rule to warn on force: true)

---

## 🧪 Testing Strategy

### Before Refactor
```bash
# Baseline: Current pass rate with force: true
pnpm test:e2e --repeat-each=5
# Expected: ~90-95% pass rate
```

### After Refactor
```bash
# Validate: Pass rate without force: true
pnpm test:e2e --repeat-each=5
# Target: >95% pass rate
```

### Regression Prevention
```bash
# Add ESLint rule
// .eslintrc.js
{
  "rules": {
    "no-restricted-syntax": [
      "warn",
      {
        "selector": "CallExpression[callee.property.name='click'] > ObjectExpression:has(Property[key.name='force'][value.value=true])",
        "message": "Avoid using { force: true } in clicks. Wait for element to be clickable instead."
      }
    ]
  }
}
```

---

## 🔗 Related Issues

- [Reduce Hardcoded Timeouts](./reduce-hardcoded-timeouts.md) - Complementare (timing issues)
- [Improve Streaming Test Stability](./improve-streaming-test-stability.md) - Usa force: true
- [Complete POM Migration](./complete-pom-migration.md) - Include waitForClickable in BasePage

---

## 📚 References

- [Playwright Actionability](https://playwright.dev/docs/actionability)
- [Why You Shouldn't Use force: true](https://www.checklyhq.com/blog/why-you-shouldnt-use-force-option-in-playwright/)
- [Playwright Best Practices - Waiting](https://playwright.dev/docs/best-practices#use-web-first-assertions)

---

## 📊 Success Metrics

| Metric | Before | Target | Status |
|--------|--------|--------|--------|
| **Force True Count** | 32 | <5 (ideally 0) | 🔴 Pending |
| **Test Stability** | ~90% | >95% | 🔴 Pending |
| **False Positives** | Unknown | 0 | 🔴 Pending |
| **Bug Detection** | Low | High | 🔴 Pending |

---

**Created**: 2025-11-20
**Last Updated**: 2025-11-20
**Owner**: QA Team
**Reviewers**: Frontend Team

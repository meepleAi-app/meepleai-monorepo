# E2E Selector Best Practices Guide

**Issue #2542**: E2E Test Suite Infrastructure Improvements
**Created**: 2026-01-16
**Author**: MeepleAI Development Team

---

## Overview

This guide establishes the **priority-based selector strategy** for Playwright E2E tests to ensure stable, maintainable, and accessible test automation.

## Selector Priority Hierarchy

### 1. `data-testid` Attributes (Highest Priority) 🛡️

**Use for**: Stable, implementation-agnostic element identification

**Pros**:
- ✅ Immune to UI text changes (i18n, copywriting refactors)
- ✅ Immune to styling/layout changes
- ✅ Explicit test contract between components and tests
- ✅ Fast selector performance

**Cons**:
- ⚠️ Requires component modifications (not free)
- ⚠️ Can bloat HTML if overused

**When to Use**:
- Critical user actions (buttons, forms, navigation)
- Components with dynamic text content
- Multi-language UI elements
- Frequently refactored components

**Naming Convention**:
```typescript
// Pattern: {component}-{element}-{descriptor}
data-testid="stat-card"                    // Container
data-testid="stat-card-label"              // Child element
data-testid="stat-card-value"              // Data element
data-testid="admin-nav-link-analytics"     // Specific instance
data-testid="export-button-trigger"        // Action trigger
data-testid="login-email"                  // Form input
```

**Example**:
```tsx
// Component
export function StatCard({ label, value }: Props) {
  return (
    <Card data-testid="stat-card">
      <div data-testid="stat-card-label">{label}</div>
      <div data-testid="stat-card-value">{value}</div>
    </Card>
  );
}

// Test
await expect(page.getByTestId('stat-card')).toBeVisible();
await expect(page.getByTestId('stat-card-value')).toHaveText('42');
```

---

### 2. Semantic Role Selectors (Medium Priority) ♿

**Use for**: Accessible, semantic element identification

**Pros**:
- ✅ Tests accessibility compliance (WCAG 2.1 AA)
- ✅ Semantic HTML structure validation
- ✅ No component modifications required
- ✅ Self-documenting test intent

**Cons**:
- ⚠️ Can be ambiguous if multiple similar roles exist
- ⚠️ Requires correct ARIA implementation

**When to Use**:
- Buttons, links, headings with stable semantic roles
- Form controls with proper labels
- Navigation elements
- When accessibility testing is also a goal

**Example**:
```typescript
// Buttons
await page.getByRole('button', { name: /submit|invia/i }).click();

// Links
await page.getByRole('link', { name: /home|dashboard/i }).click();

// Headings
await expect(page.getByRole('heading', { name: /analytics/i, level: 2 })).toBeVisible();

// Form inputs
await page.getByLabel(/email|e-mail/i).fill('test@example.com');

// Comboboxes/selects
await page.getByRole('combobox', { name: /time period/i }).selectOption('7');
```

---

### 3. Text Selectors (Lowest Priority) ⚠️

**Use for**: Last resort when data-testid and role selectors are not viable

**Pros**:
- ✅ No component modifications required
- ✅ Quick to write

**Cons**:
- ❌ Fragile to copywriting changes
- ❌ Breaks on i18n changes
- ❌ Performance overhead (full DOM text search)
- ❌ Ambiguous when multiple elements share text

**When to Use**:
- Unique text content unlikely to change
- Temporary tests or prototypes
- Non-critical elements

**Example**:
```typescript
// Use regex for multi-language support
await expect(page.getByText(/analytics dashboard|cruscotto/i)).toBeVisible();

// Prefer exact text with getTextMatcher helper
const matcher = getTextMatcher('admin.analytics.dashboard');
await expect(page.getByText(matcher)).toBeVisible();
```

---

## MeepleAI Project Standards

### Robust Selector Helpers

Use helpers from `e2e/fixtures/robust-selectors.ts`:

```typescript
import {
  waitForMetricsGrid,
  getByDataTestId,
  getButton,
  submitLogin,
  exportAsCSV,
} from './fixtures/robust-selectors';

// Wait for admin analytics metrics
await waitForMetricsGrid(page);

// Get element with optional text filter
await getByDataTestId(page, 'stat-card-label', /users|utenti/i);

// Button with fallback
await getButton(page, /export|esporta/i, 'export-button-trigger');

// High-level actions
await submitLogin(page, 'admin@test.com', 'password');
await exportAsCSV(page);
```

### Decision Tree

```
Need to select element?
├─ Is it a critical action? (login, submit, delete)
│  └─ YES → Use data-testid (add if missing)
├─ Is it a button/link/form?
│  └─ YES → Use getByRole (semantic)
├─ Is text content stable and unique?
│  └─ YES → Use getByText with regex
└─ Otherwise → Add data-testid to component
```

---

## Migration Strategy

### Phase 1: Add Critical data-testid (Completed)
- ✅ Admin components (StatCard, AdminHeader, AdminSidebar, ExportButton)
- ✅ Auth components (LoginForm, RegisterForm, AuthModal, OAuthButtons)
- ✅ Chat components (MessageInput, Message, MessageList)

### Phase 2: Update High-Traffic Tests (In Progress)
- 🔄 admin-analytics.spec.ts (updated)
- ⏳ admin-users.spec.ts (partial - uses data-testid already)
- ⏳ auth E2E tests (oauth, password-reset)

### Phase 3: Gradual Improvement (Future)
- Update remaining tests opportunistically during feature work
- Add data-testid when components are modified
- Prioritize tests with high flake rates

---

## Anti-Patterns to Avoid

### ❌ Don't: Use CSS Selectors Directly
```typescript
// BAD: Fragile to styling changes
await page.locator('.btn-primary').click();
await page.locator('#submit-button').click();
```

### ❌ Don't: Use XPath
```typescript
// BAD: Fragile and hard to maintain
await page.locator('//div[@class="card"]/button[1]').click();
```

### ❌ Don't: Use nth-child Selectors
```typescript
// BAD: Breaks when DOM structure changes
await page.locator('ul > li:nth-child(3)').click();
```

### ❌ Don't: Hardcode Language-Specific Text
```typescript
// BAD: Breaks when UI language changes
await page.getByText('Login').click(); // Only works in English

// GOOD: Use multi-language regex
await page.getByText(/login|accedi/i).click();
```

---

## Real-World Examples

### Admin Analytics Test (Before vs After)

**Before (Fragile)**:
```typescript
await expect(page.getByText(getTextMatcher('admin.analytics.totalUsers'))).toBeVisible();
await expect(page.getByText(getTextMatcher('admin.analytics.activeSessions'))).toBeVisible();
await expect(page.getByText(getTextMatcher('admin.analytics.apiRequestsToday'))).toBeVisible();
// ... 8 more similar lines
```

**After (Robust)**:
```typescript
// Wait for metrics grid
await waitForMetricsGrid(page);

// Verify minimum metrics count
const statCards = page.getByTestId('stat-card');
expect(await statCards.count()).toBeGreaterThanOrEqual(8);

// Verify specific metrics (if needed)
await expect(page.getByTestId('stat-card-label').filter({ hasText: /users/i })).toBeVisible();
```

**Benefits**:
- 11 lines → 3 lines (73% reduction)
- No dependency on i18n keys
- Validates structure, not just text
- Faster execution (single selector)

---

### Auth Test (Before vs After)

**Before (Fragile)**:
```typescript
await page.getByLabel(getTextMatcher('auth.login.emailLabel')).fill('admin@test.com');
await page.getByLabel(getTextMatcher('auth.login.passwordLabel')).fill('password');
await page.getByRole('button', { name: getTextMatcher('auth.login.loginButton') }).click();
```

**After (Robust)**:
```typescript
await submitLogin(page, 'admin@test.com', 'password');
```

**Benefits**:
- 3 lines → 1 line (67% reduction)
- Reusable across tests
- Immune to label text changes
- Single source of truth for login flow

---

## Performance Considerations

### Selector Speed Ranking

1. **`data-testid`**: O(1) - Direct attribute lookup
2. **`role + name`**: O(n) - Semantic tree traversal
3. **`text`**: O(n) - Full text content search

### Optimization Tips

```typescript
// ✅ GOOD: Store locator reference
const cards = page.getByTestId('stat-card');
await expect(cards.first()).toBeVisible();
const count = await cards.count();

// ❌ BAD: Redundant selector calls
await expect(page.getByTestId('stat-card').first()).toBeVisible();
const count = await page.getByTestId('stat-card').count(); // Selector called again
```

---

## Component Modification Guidelines

### When to Add data-testid

**High Priority**:
- Form submit buttons
- Navigation links
- Critical action buttons (delete, export, save)
- Form inputs (email, password, etc.)
- Modal triggers and containers

**Medium Priority**:
- List items in tables/grids
- Toggle switches
- Dropdown menus
- Tab panels

**Low Priority**:
- Static text content
- Decorative elements
- Icons without actions

### How to Add data-testid

```tsx
// Container element
<div data-testid="component-name">

// Interactive elements
<button data-testid="action-submit">Submit</button>
<input data-testid="input-email" />

// List items with dynamic IDs
<li data-testid={`list-item-${item.id}`}>

// Conditional rendering
{isLoading && <div data-testid="loading-spinner" />}
```

---

## Testing the Tests

### Verify Selector Stability

```bash
# Run tests multiple times to detect flakiness
pnpm test:e2e admin-analytics.spec.ts --project=desktop-chrome --repeat-each=5

# Run with different locales
TEST_LANG=en pnpm test:e2e admin-analytics.spec.ts
TEST_LANG=it pnpm test:e2e admin-analytics.spec.ts

# Run with network throttling
pnpm test:e2e admin-analytics.spec.ts --project=slow-3g
```

### Measure Flakiness

```typescript
// Expect stable selectors to have 0% flake rate
const results = await runTestMultipleTimes(5);
expect(results.flakeRate).toBe(0);
```

---

## Maintenance

### When Selectors Break

**Checklist**:
1. Check if component was renamed/removed
2. Check if data-testid was changed/removed
3. Check if ARIA roles were modified
4. Check if text content changed (i18n updates)
5. Update test to match new component structure

### Preventing Breakage

**Code Review Checklist**:
- [ ] New components have data-testid for interactive elements
- [ ] Component refactors preserve existing data-testid
- [ ] E2E tests updated if component API changes
- [ ] Visual regression tests capture UI changes

---

## Impact Metrics

### Before Robust Selectors (Baseline)
- **Fragile Tests**: ~80% using `getTextMatcher` magic strings
- **Estimated Flake Rate**: 15-25% (i18n dependency)
- **Maintenance Cost**: High (every copywriting change breaks tests)

### After Robust Selectors (Target)
- **Stable Tests**: ~70% using `data-testid` or semantic roles
- **Estimated Flake Rate**: <5% (reduced i18n dependency)
- **Maintenance Cost**: Low (decoupled from UI text)

---

## Resources

- **Playwright Best Practices**: https://playwright.dev/docs/best-practices
- **Testing Library Priority**: https://testing-library.com/docs/queries/about#priority
- **WCAG 2.1 ARIA Roles**: https://www.w3.org/TR/wai-aria-1.1/#role_definitions
- **MeepleAI E2E Fixtures**: `apps/web/e2e/fixtures/robust-selectors.ts`

---

## Quick Reference Card

```typescript
// ✅ Priority 1: data-testid
page.getByTestId('submit-button')

// ✅ Priority 2: Semantic role
page.getByRole('button', { name: /submit|invia/i })
page.getByLabel(/email/i)

// ⚠️ Priority 3: Text content (last resort)
page.getByText(/unique text/i)

// ❌ Never use
page.locator('.css-class')              // Fragile
page.locator('#id')                     // Fragile
page.locator('div > button:nth-child(2)') // Fragile
```

---

**Last Updated**: 2026-01-16
**Next Review**: When test flake rate exceeds 5%

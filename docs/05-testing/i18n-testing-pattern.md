# i18n Testing Pattern - Language-Independent Test Assertions

**Created**: 2026-01-25
**Issue**: #3029
**Status**: Active Pattern

## Problem Statement

Frontend tests with hardcoded language strings break when:
- Component locale changes (Italian → English or vice versa)
- UI text is updated in translation files
- Different developers use different language settings

**Example Failure**:
```tsx
// Component renders: "Infrastructure Monitoring" (English)
// Test expects: "Monitoraggio Infrastruttura" (Italian)
// Result: ❌ Test fails - element not found
```

## Solution: data-testid Pattern

Use `data-testid` attributes for language-independent element targeting.

### Pattern Overview

**Component** (add data-testid to testable elements):
```tsx
export function MyComponent() {
  const i18n = getMyComponentI18n(locale);

  return (
    <div>
      <h1 data-testid="component-title">{i18n.title}</h1>
      <button data-testid="submit-button" onClick={handleSubmit}>
        {i18n.submitLabel}
      </button>
      <input
        data-testid="search-input"
        placeholder={i18n.searchPlaceholder}
      />
    </div>
  );
}
```

**Test** (use getByTestId instead of getByText):
```tsx
import { render, screen } from '@testing-library/react';

it('should render correctly', () => {
  render(<MyComponent />);

  // ✅ Language-independent
  expect(screen.getByTestId('component-title')).toBeInTheDocument();
  expect(screen.getByTestId('submit-button')).toBeInTheDocument();
  expect(screen.getByTestId('search-input')).toBeInTheDocument();

  // ❌ Language-dependent (avoid)
  // expect(screen.getByText('My Component')).toBeInTheDocument();
});
```

## Naming Conventions

### data-testid Naming Rules

1. **Kebab-case**: Use lowercase with hyphens (e.g., `user-profile-card`)
2. **Descriptive**: Clearly identify element purpose (e.g., `submit-button`, not `btn1`)
3. **Component-scoped**: Prefix with component/page name for uniqueness
4. **Semantic**: Describe function, not appearance (e.g., `primary-action`, not `blue-button`)

### Common Patterns

| Element Type | Pattern | Example |
|--------------|---------|---------|
| Page title | `{page}-title` | `infrastructure-title` |
| Buttons | `{action}-button` | `refresh-button`, `submit-button` |
| Inputs | `{purpose}-input` | `search-input`, `email-input` |
| Forms | `{entity}-form` | `login-form`, `user-form` |
| Lists | `{entity}-list` | `service-list`, `user-list` |
| Cards | `{entity}-card` | `game-card`, `user-card` |
| Modals | `{purpose}-modal` | `confirm-modal`, `edit-modal` |
| Errors | `{context}-error` | `infrastructure-error`, `form-error` |
| Containers | `{purpose}-container` | `metrics-container`, `results-container` |

## When to Use data-testid

### ✅ Always Use For:
- **Interactive elements**: Buttons, links, inputs, forms
- **Dynamic content containers**: Lists, grids, tables with varying content
- **State-dependent elements**: Loading states, error messages, success notifications
- **Critical user paths**: Login flow, checkout process, main actions
- **Bilingual/multilingual content**: Any text that changes with locale

### ⚠️ Optional For:
- **Static service names**: "PostgreSQL", "Redis" (don't change with locale)
- **Technical identifiers**: IDs, codes, version numbers
- **Numbers/metrics**: Already locale-agnostic with regex matchers

### ❌ Don't Use For:
- **Accessibility roles**: Use getByRole() for semantic testing
- **Form labels**: Use getByLabelText() for accessibility validation
- **Alt text**: Use getByAltText() for image accessibility

## Migration Guide

### Step 1: Identify Language-Dependent Assertions

Search for patterns in test files:
```bash
# Find hardcoded text assertions
grep -r "getByText(['\"]\w" src/**/*.test.tsx

# Find role assertions with hardcoded names
grep -r "getByRole.*name:.*['\"]\w" src/**/*.test.tsx

# Find placeholder assertions
grep -r "getByPlaceholderText(['\"]\w" src/**/*.test.tsx
```

### Step 2: Add data-testid to Components

```tsx
// Before:
<h1 className="text-3xl">{i18n.page.title}</h1>

// After:
<h1 className="text-3xl" data-testid="page-title">{i18n.page.title}</h1>
```

### Step 3: Update Test Assertions

```tsx
// Before (language-dependent):
expect(screen.getByText('Monitoraggio Infrastruttura')).toBeInTheDocument();
expect(screen.getByRole('button', { name: /aggiorna/i })).toBeInTheDocument();
expect(screen.getByPlaceholderText(/cerca servizio/i)).toBeInTheDocument();

// After (language-independent):
expect(screen.getByTestId('infrastructure-title')).toBeInTheDocument();
expect(screen.getByTestId('refresh-button')).toBeInTheDocument();
expect(screen.getByTestId('search-input')).toBeInTheDocument();
```

### Step 4: Handle Bilingual Content Verification

For error messages or dynamic content that needs content verification:

```tsx
// Verify element exists AND check content in both languages
const errorElement = screen.getByTestId('infrastructure-error');
expect(errorElement).toBeInTheDocument();
expect(errorElement.textContent).toMatch(/errore caricamento|loading error/i);
```

## Proof of Concept: Infrastructure Monitoring

**Files**:
- Component: `apps/web/src/app/admin/infrastructure/infrastructure-client.tsx`
- Test: `apps/web/src/app/admin/infrastructure/__tests__/infrastructure-client.test.tsx`

**Conversions** (10 hardcoded assertions → data-testid):

| Before | After | Element |
|--------|-------|---------|
| `getByText('Monitoraggio Infrastruttura')` | `getByTestId('infrastructure-title')` | Page title |
| `getByRole('button', { name: /aggiorna/i })` | `getByTestId('refresh-button')` | Refresh button |
| `getByPlaceholderText(/cerca servizio/i)` | `getByTestId('search-input')` | Search input |
| `getByRole('button', { name: /csv/i })` | `getByTestId('export-csv-button')` | Export button |
| `getByText(/errore caricamento/i)` | `getByTestId('infrastructure-error')` | Error alert |
| `getByRole('switch', { name: /aggiornamento/i })` | `getByTestId('auto-refresh-switch')` | Toggle |
| `getByRole('list', { name: /stato servizi/i })` | `getByTestId('service-health-matrix')` | Service list |
| `getByText(/15[.,]234/)` | `getByTestId('metric-api-requests')` | Metric value |

## Testing with Multiple Locales

### Run tests with different locales:

```bash
# Italian locale (default)
TEST_LANG=it pnpm test

# English locale
TEST_LANG=en pnpm test

# Both locales (CI/CD)
TEST_LANG=it pnpm test && TEST_LANG=en pnpm test
```

### Expected Behavior:

✅ **With data-testid**: Tests pass regardless of locale
❌ **With hardcoded strings**: Tests fail when locale changes

## Best Practices

### DO:
- ✅ Add data-testid to interactive elements and dynamic content
- ✅ Use descriptive, kebab-case names
- ✅ Keep data-testid in sync with component refactors
- ✅ Use getByRole() for accessibility checks in addition to data-testid
- ✅ Document data-testid in component comments/Storybook

### DON'T:
- ❌ Use data-testid for styling or production logic
- ❌ Duplicate data-testid values in same component tree
- ❌ Use generic names like `button1`, `div2`
- ❌ Replace ALL getByRole/getByLabelText with data-testid (keep accessibility tests)
- ❌ Add data-testid to every single element (only testable ones)

## Accessibility Considerations

**Important**: data-testid should complement, not replace, accessibility testing.

```tsx
// ✅ Good: Use both for different purposes
expect(screen.getByTestId('submit-button')).toBeInTheDocument(); // Element targeting
expect(screen.getByRole('button')).toHaveAccessibleName(); // Accessibility validation

// ❌ Bad: Only data-testid, no accessibility check
expect(screen.getByTestId('submit-button')).toBeInTheDocument(); // Missing a11y validation
```

## Systematic Migration Process

For the remaining 184 test files (~1,500 occurrences):

### Phase 1: Categorize Files
1. Admin components (~50 files)
2. Chat/AI components (~40 files)
3. Games/Library components (~40 files)
4. Shared/UI components (~55 files)

### Phase 2: Batch Processing
- Process 10-15 files per batch
- Add data-testid to components
- Update test assertions
- Run tests after each batch

### Phase 3: Validation
- Verify tests pass with TEST_LANG=it
- Verify tests pass with TEST_LANG=en
- Check test coverage maintained
- No new warnings introduced

## Examples from Proof of Concept

See actual implementation in:
- `apps/web/src/app/admin/infrastructure/infrastructure-client.tsx` (lines 290, 308, 317, 474, 526, 536, 554)
- `apps/web/src/app/admin/infrastructure/__tests__/infrastructure-client.test.tsx` (lines 86, 105, 135, 232, 261, 308, 333)

## Related Documentation

- Test utilities: `apps/web/src/test-utils/test-i18n.ts`
- i18n architecture: `docs/02-development/internationalization.md` (if exists)
- Testing guide: `docs/05-testing/frontend-testing.md`

---

**Pattern Status**: ✅ Validated (infrastructure-client proof of concept)
**Ready for**: Systematic application across remaining 184 files

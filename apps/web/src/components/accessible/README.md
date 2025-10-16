# Accessible Components Library (UI-05)

A collection of fully accessible React components following **WCAG 2.1 AA** standards.

Created as part of **Issue #306 (UI-05 - Audit accessibilità baseline)**.

---

## Components

### 1. AccessibleButton

Fully accessible button component with proper ARIA attributes, keyboard navigation, and loading states.

**Features:**
- ✅ Multiple variants (primary, secondary, danger, ghost)
- ✅ Multiple sizes (sm, md, lg)
- ✅ Icon-only buttons with mandatory aria-label
- ✅ Loading states with aria-live announcements
- ✅ Toggle buttons with aria-pressed
- ✅ Focus indicators (WCAG 2.1 AA)
- ✅ High contrast support

**Usage:**
```tsx
import { AccessibleButton } from '@/components/accessible';

// Primary button
<AccessibleButton variant="primary" onClick={handleClick}>
  Save Changes
</AccessibleButton>

// Icon-only button
<AccessibleButton
  variant="ghost"
  iconOnly
  aria-label="Close dialog"
  onClick={handleClose}
>
  ✕
</AccessibleButton>

// Loading button
<AccessibleButton isLoading loadingText="Saving...">
  Save
</AccessibleButton>
```

---

### 2. AccessibleModal

Fully accessible modal dialog with focus trap, ESC key support, and scroll lock.

**Features:**
- ✅ `role="dialog"` and `aria-modal="true"`
- ✅ `aria-labelledby` and `aria-describedby`
- ✅ Focus trap (prevents Tab outside modal)
- ✅ Focus restoration (returns focus on close)
- ✅ ESC key to close
- ✅ Backdrop click to close (optional)
- ✅ Body scroll lock
- ✅ Smooth animations (framer-motion)

**Usage:**
```tsx
import { AccessibleModal } from '@/components/accessible';

const [isOpen, setIsOpen] = useState(false);

<AccessibleModal
  isOpen={isOpen}
  onClose={() => setIsOpen(false)}
  title="Confirm Delete"
  description="Are you sure you want to delete this item?"
>
  <p>This action cannot be undone.</p>
  <div className="flex gap-2 mt-4">
    <AccessibleButton variant="danger" onClick={handleDelete}>
      Delete
    </AccessibleButton>
    <AccessibleButton variant="secondary" onClick={() => setIsOpen(false)}>
      Cancel
    </AccessibleButton>
  </div>
</AccessibleModal>
```

**Important:** Replace existing modal implementations (e.g., in `index.tsx`) with this component.

---

### 3. AccessibleFormInput

Fully accessible form input with proper label association and error announcements.

**Features:**
- ✅ Proper label association (`htmlFor`/`id`)
- ✅ Error announcements with `aria-live="polite"`
- ✅ Hint/description with `aria-describedby`
- ✅ Required field indication
- ✅ `aria-invalid` for error states
- ✅ Focus indicators (WCAG 2.1 AA)
- ✅ Visually hidden labels (still accessible)

**Usage:**
```tsx
import { AccessibleFormInput } from '@/components/accessible';

// Basic input
<AccessibleFormInput
  label="Email"
  type="email"
  value={email}
  onChange={(e) => setEmail(e.target.value)}
  required
/>

// Input with hint
<AccessibleFormInput
  label="Password"
  type="password"
  value={password}
  onChange={(e) => setPassword(e.target.value)}
  hint="Must be at least 8 characters"
  required
/>

// Input with error
<AccessibleFormInput
  label="Username"
  value={username}
  onChange={(e) => setUsername(e.target.value)}
  error="Username is already taken"
/>
```

**Important:** Replace existing form inputs (e.g., login/register forms) with this component.

---

### 4. AccessibleSkipLink

"Skip to main content" link for keyboard users to bypass repetitive navigation.

**Features:**
- ✅ Visually hidden until focused
- ✅ Appears on Tab (first focusable element)
- ✅ High contrast focus indicator
- ✅ Smooth scroll to target
- ✅ Automatic focus management

**Usage:**
```tsx
import { AccessibleSkipLink } from '@/components/accessible';

// In _app.tsx or layout
<AccessibleSkipLink href="#main-content" />

// In page component
<main id="main-content" tabIndex={-1}>
  <h1>Page Title</h1>
  ...
</main>
```

**Important:** Add this to `_app.tsx` as the **first focusable element** on every page.

---

## CSS Utilities (Added to `globals.css`)

### Screen Reader Only
```css
.sr-only {
  /* Visually hidden but accessible to screen readers */
}
```

### Focus Indicators (WCAG 2.1 AA)
```css
*:focus-visible {
  outline: 2px solid theme('colors.primary.500');
  outline-offset: 2px;
}
```

All focus indicators are **2px solid** with **2px offset**, meeting WCAG 2.1 AA requirements.

---

## Accessibility Testing

### Unit Tests (jest-axe)

Example test for AccessibleButton:

```tsx
import { render } from '@testing-library/react';
import { axe, toHaveNoViolations } from 'jest-axe';
import { AccessibleButton } from './AccessibleButton';

expect.extend(toHaveNoViolations);

describe('AccessibleButton', () => {
  it('should have no accessibility violations', async () => {
    const { container } = render(
      <AccessibleButton onClick={() => {}}>
        Click Me
      </AccessibleButton>
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should require aria-label for icon-only buttons', () => {
    // This should log a warning in dev mode
    render(
      <AccessibleButton iconOnly onClick={() => {}}>
        ✕
      </AccessibleButton>
    );
  });
});
```

### E2E Tests (axe-playwright)

Example test:

```typescript
import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

test('page should have no accessibility violations', async ({ page }) => {
  await page.goto('/');

  const results = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
    .analyze();

  expect(results.violations).toEqual([]);
});
```

---

## Next Steps (Remaining Work)

### Phase 5: Implement Fixes

1. **Replace auth modal in `index.tsx`** with `AccessibleModal`
2. **Fix chat.tsx**:
   - Add `aria-label` to icon-only buttons
   - Use semantic `<ul role="log" aria-live="polite">` for messages
   - Add `aria-current` for active chat
3. **Fix Timeline component**:
   - Add `aria-label` to toggle button
   - Proper `<label>` elements in filters
   - Use `<fieldset>` and `<legend>`
4. **Add Skip Link to `_app.tsx`**
5. **Verify color contrast** (WCAG AA: 4.5:1 for normal text)

### Phase 6: Manual Testing

- [ ] Keyboard navigation testing (Tab, Shift+Tab, Enter, ESC)
- [ ] Screen reader testing (NVDA on Windows, VoiceOver on macOS)
- [ ] Zoom testing (200%)

### Phase 7: Documentation

- [ ] Complete audit report after running `pnpm audit:a11y`
- [ ] Create accessibility checklist for developers
- [ ] Update component JSDoc

---

## Running the Full Audit

To execute the automated axe-core audit:

```bash
# Terminal 1: Start dev server
cd apps/web
pnpm dev

# Terminal 2: Run audit
cd apps/web
pnpm audit:a11y
```

This generates:
- `docs/issue/ui-05-accessibility-audit.md` (detailed report)
- `docs/issue/ui-05-accessibility-audit.json` (CI/CD data)

---

## Resources

### WCAG 2.1 AA Requirements
- [WCAG 2.1 Quick Reference](https://www.w3.org/WAI/WCAG21/quickref/?levels=aa)
- [WebAIM WCAG Checklist](https://webaim.org/standards/wcag/checklist)

### Testing Tools
- **axe DevTools:** Browser extension for manual audits
- **Lighthouse:** Chrome DevTools > Lighthouse > Accessibility
- **WAVE:** WebAIM browser extension
- **jest-axe:** Unit testing (installed ✅)
- **@axe-core/playwright:** E2E testing (installed ✅)
- **@axe-core/react:** Runtime checks in dev (installed ✅)

### Screen Readers
- **Windows:** [NVDA](https://www.nvaccess.org/) (free)
- **macOS:** VoiceOver (built-in, Cmd+F5)

### Contrast Checkers
- **Chrome DevTools:** Inspect > Accessibility pane
- **WebAIM Contrast Checker:** https://webaim.org/resources/contrastchecker/

---

## Contributing

When creating new components:

1. **Always include proper ARIA attributes**
2. **Test with jest-axe** (unit tests)
3. **Test with axe-playwright** (E2E tests)
4. **Test keyboard navigation** manually
5. **Document with JSDoc** and examples
6. **Follow existing patterns** from this library

---

**Created by:** UI-05 (Issue #306)
**WCAG Level:** 2.1 AA
**Status:** Components ready, implementation pending

# Accessibility Guidelines (WCAG 2.1 AA)

**MeepleAI Frontend Accessibility Standards & Testing Guide**

Issue: #2929 - Accessibility Audit & WCAG 2.1 AA Compliance

---

## Table of Contents

1. [WCAG 2.1 AA Overview](#wcag-21-aa-overview)
2. [Core Requirements](#core-requirements)
3. [Component Patterns](#component-patterns)
4. [Testing Commands](#testing-commands)
5. [Developer Checklist](#developer-checklist)
6. [Automated Testing](#automated-testing)
7. [Manual Testing](#manual-testing)
8. [CI/CD Integration](#cicd-integration)
9. [Common Violations & Fixes](#common-violations--fixes)
10. [Resources](#resources)

---

## WCAG 2.1 AA Overview

WCAG (Web Content Accessibility Guidelines) 2.1 Level AA is the internationally recognized standard for web accessibility. MeepleAI targets full AA compliance across all pages.

### Four Principles (POUR)

| Principle | Description | Key Requirements |
|-----------|-------------|------------------|
| **Perceivable** | Content must be presentable to users | Alt text, captions, contrast |
| **Operable** | UI must be operable | Keyboard access, focus management |
| **Understandable** | Content must be understandable | Clear labels, consistent navigation |
| **Robust** | Content must be compatible | Valid HTML, ARIA support |

### Success Criteria Levels

- **Level A**: Minimum accessibility (basic requirements)
- **Level AA**: Standard accessibility (MeepleAI target)
- **Level AAA**: Enhanced accessibility (optional enhancements)

---

## Core Requirements

### 1. Color Contrast (WCAG 1.4.3, 1.4.11)

**Normal Text**: Minimum 4.5:1 contrast ratio
**Large Text** (18pt+ or 14pt bold): Minimum 3:1 contrast ratio
**UI Components**: Minimum 3:1 contrast ratio

```css
/* ✅ GOOD - High contrast */
.text-primary { color: #1a1a2e; }     /* On white: 14.5:1 */
.text-secondary { color: #4a5568; }   /* On white: 7.1:1 */

/* ❌ BAD - Low contrast */
.text-muted { color: #a0aec0; }       /* On white: 2.7:1 */
```

**Tools**:
- Chrome DevTools: Inspect → Accessibility pane
- WebAIM Contrast Checker: https://webaim.org/resources/contrastchecker/

### 2. Keyboard Accessibility (WCAG 2.1.1, 2.1.2)

All interactive elements must be:
- Focusable with Tab/Shift+Tab
- Activatable with Enter/Space
- Navigable with arrow keys (where appropriate)

```tsx
// ✅ GOOD - Keyboard accessible
<button onClick={handleClick}>Submit</button>
<a href="/games">View Games</a>

// ❌ BAD - Not keyboard accessible
<div onClick={handleClick}>Submit</div>
<span onClick={navigate}>View Games</span>
```

### 3. Focus Indicators (WCAG 2.4.7)

All focusable elements must have visible focus indicators:

```css
/* Global focus styles in globals.css */
*:focus-visible {
  outline: 2px solid var(--color-primary-500);
  outline-offset: 2px;
}

/* Never remove focus outlines without providing alternative */
/* ❌ BAD */
*:focus { outline: none; }

/* ✅ GOOD - Custom focus style */
.custom-button:focus-visible {
  outline: none;
  box-shadow: 0 0 0 3px rgba(66, 153, 225, 0.6);
}
```

### 4. Alt Text (WCAG 1.1.1)

All images must have appropriate alt text:

```tsx
// ✅ GOOD - Descriptive alt text
<img src="/chess.jpg" alt="Chess board with white pieces in starting position" />

// ✅ GOOD - Decorative image (empty alt)
<img src="/decorative-line.svg" alt="" role="presentation" />

// ❌ BAD - Missing or meaningless alt
<img src="/chess.jpg" />
<img src="/chess.jpg" alt="image" />
```

### 5. ARIA Labels (WCAG 4.1.2)

Use ARIA attributes when native HTML semantics are insufficient:

```tsx
// ✅ GOOD - Icon button with aria-label
<button aria-label="Close dialog" onClick={onClose}>
  <XIcon />
</button>

// ✅ GOOD - Form with aria-describedby
<input
  id="email"
  aria-describedby="email-hint email-error"
/>
<span id="email-hint">We'll never share your email</span>
<span id="email-error" role="alert">Invalid email format</span>

// ❌ BAD - Icon button without accessible name
<button onClick={onClose}>
  <XIcon />
</button>
```

---

## Component Patterns

MeepleAI provides a library of accessible components at `apps/web/src/components/accessible/`.

### AccessibleButton

```tsx
import { AccessibleButton } from '@/components/accessible';

// Primary button
<AccessibleButton variant="primary" onClick={handleClick}>
  Save Changes
</AccessibleButton>

// Icon-only button (requires aria-label)
<AccessibleButton
  variant="ghost"
  iconOnly
  aria-label="Close dialog"
  onClick={handleClose}
>
  <XIcon />
</AccessibleButton>

// Loading state (announces to screen readers)
<AccessibleButton isLoading loadingText="Saving...">
  Save
</AccessibleButton>

// Toggle button
<AccessibleButton
  isToggle
  isPressed={isActive}
  onClick={() => setIsActive(!isActive)}
>
  Dark Mode
</AccessibleButton>
```

### AccessibleModal

```tsx
import { AccessibleModal } from '@/components/accessible';

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

**Features**:
- Focus trap (Tab cycles within modal)
- ESC key to close
- Focus restoration on close
- Body scroll lock
- `aria-modal="true"` and proper labeling

### AccessibleFormInput

```tsx
import { AccessibleFormInput } from '@/components/accessible';

// Basic input with label
<AccessibleFormInput
  label="Email"
  type="email"
  value={email}
  onChange={(e) => setEmail(e.target.value)}
  required
/>

// Input with hint text
<AccessibleFormInput
  label="Password"
  type="password"
  hint="Must be at least 8 characters"
  required
/>

// Input with error state
<AccessibleFormInput
  label="Username"
  value={username}
  error="Username is already taken"
/>
```

### AccessibleSkipLink

Add to layout as the first focusable element:

```tsx
import { AccessibleSkipLink } from '@/components/accessible';

// In layout.tsx
export default function RootLayout({ children }) {
  return (
    <html>
      <body>
        <AccessibleSkipLink href="#main-content" />
        <Header />
        <main id="main-content" tabIndex={-1}>
          {children}
        </main>
        <Footer />
      </body>
    </html>
  );
}
```

---

## Testing Commands

### Quick Reference

```bash
# Unit accessibility tests (jest-axe)
pnpm test:a11y

# E2E accessibility tests (axe-playwright)
pnpm test:a11y:e2e

# Full automated audit (requires dev server)
pnpm audit:a11y

# All tests with coverage
pnpm test:coverage

# E2E tests (includes accessibility)
pnpm test:e2e
```

### Detailed Commands

| Command | Tool | Scope | Output |
|---------|------|-------|--------|
| `pnpm test:a11y` | Vitest + jest-axe | Component unit tests | Terminal |
| `pnpm test:a11y:e2e` | Playwright + axe | E2E page tests | playwright-report/ |
| `pnpm audit:a11y` | tsx script | Full site audit | docs/issue/*.md |
| `pnpm test:e2e` | Playwright | All E2E including a11y | playwright-report/ |

---

## Developer Checklist

Use this checklist before submitting PRs with UI changes:

### Semantic HTML

- [ ] Use semantic elements (`<button>`, `<a>`, `<nav>`, `<main>`, `<header>`, `<footer>`)
- [ ] Use heading hierarchy correctly (`<h1>` → `<h2>` → `<h3>`, no skipping)
- [ ] Use `<ul>`/`<ol>` for lists, `<table>` for tabular data
- [ ] Avoid `<div>` and `<span>` for interactive elements

### Images & Media

- [ ] All `<img>` have meaningful `alt` text or `alt=""` for decorative
- [ ] Complex images have extended descriptions
- [ ] Videos have captions/transcripts

### Forms

- [ ] All inputs have associated `<label>` elements
- [ ] Error messages are programmatically linked (`aria-describedby`)
- [ ] Required fields are indicated visually AND programmatically
- [ ] Form instructions are clear and accessible

### Keyboard Navigation

- [ ] All interactive elements are focusable with Tab
- [ ] Focus order is logical (matches visual order)
- [ ] Custom widgets have appropriate keyboard support
- [ ] No keyboard traps (except modals with ESC escape)

### Color & Contrast

- [ ] Text meets 4.5:1 contrast ratio (3:1 for large text)
- [ ] Information is not conveyed by color alone
- [ ] Focus indicators are visible
- [ ] Works in high contrast mode

### ARIA

- [ ] ARIA is only used when necessary (prefer native HTML)
- [ ] `aria-label` provided for icon-only buttons
- [ ] Live regions (`aria-live`) for dynamic content
- [ ] Modal dialogs use proper ARIA attributes

### Testing

- [ ] Component passes jest-axe tests
- [ ] Page passes axe-playwright E2E tests
- [ ] Manual keyboard navigation verified
- [ ] Screen reader tested (optional but recommended)

---

## Automated Testing

### Unit Tests (jest-axe)

Create accessibility tests alongside component tests:

```tsx
// Button.a11y.test.tsx
import { render } from '@testing-library/react';
import { axe } from 'jest-axe';
import { Button } from './Button';

describe('Button - Accessibility', () => {
  it('should have no accessibility violations', async () => {
    const { container } = render(
      <Button onClick={() => {}}>Click Me</Button>
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have no violations with all variants', async () => {
    const { container } = render(
      <>
        <Button variant="primary">Primary</Button>
        <Button variant="secondary">Secondary</Button>
        <Button variant="danger">Danger</Button>
        <Button disabled>Disabled</Button>
      </>
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
```

### E2E Tests (axe-playwright)

E2E accessibility tests in `apps/web/e2e/accessibility.spec.ts`:

```typescript
import AxeBuilder from '@axe-core/playwright';
import { test, expect } from '@playwright/test';

const WCAG_TAGS = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'];

test.describe('Accessibility - Page Tests', () => {
  test('Landing Page meets WCAG 2.1 AA', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    const results = await new AxeBuilder({ page })
      .withTags(WCAG_TAGS)
      .analyze();

    expect(results.violations).toEqual([]);
  });

  test('Games Catalog meets WCAG 2.1 AA', async ({ page }) => {
    await page.goto('/board-game-ai/games');
    await page.waitForLoadState('networkidle');

    const results = await new AxeBuilder({ page })
      .withTags(WCAG_TAGS)
      .analyze();

    expect(results.violations).toEqual([]);
  });
});
```

### Excluding Known Issues

Temporarily exclude elements while fixing:

```typescript
const results = await new AxeBuilder({ page })
  .withTags(WCAG_TAGS)
  .exclude('#third-party-widget')  // Can't control
  .exclude('[data-chromatic-ignore]')  // Test-only
  .analyze();
```

---

## Manual Testing

### Keyboard Navigation Checklist

1. **Tab through page**: All interactive elements reachable in logical order
2. **Shift+Tab**: Reverse navigation works
3. **Enter/Space**: Activates buttons, links, controls
4. **Arrow keys**: Navigate within components (menus, tabs, etc.)
5. **Escape**: Closes modals, dropdowns, popups
6. **Focus visible**: Current focus always visible

### Screen Reader Testing

**Windows**: NVDA (free) - https://www.nvaccess.org/
**macOS**: VoiceOver (built-in) - Cmd+F5
**Browser**: ChromeVox extension

**Test these scenarios**:
1. Page title announced on navigation
2. Headings provide document outline (H key in NVDA)
3. Links and buttons announce their purpose
4. Form labels read correctly
5. Error messages announced
6. Dynamic content changes announced (live regions)

### Zoom Testing

1. Zoom to 200% (Ctrl/Cmd + +)
2. Verify no horizontal scrolling required
3. Verify text remains readable
4. Verify all functionality accessible
5. Test at 400% for AAA compliance

---

## CI/CD Integration

Accessibility tests run automatically in CI:

### GitHub Actions (ci.yml)

```yaml
# E2E tests include accessibility
- name: Run E2E Tests
  run: pnpm test:e2e
  env:
    CI: true
```

### test-e2e.yml (Full Suite)

- 4-shard parallel execution
- All pages tested for WCAG 2.1 AA
- Reports uploaded as artifacts
- Quality gate: ≥90% pass rate

### Pre-commit Checks

Consider adding lint checks:

```bash
# In package.json scripts
"lint:a11y": "eslint --ext .tsx --rule 'jsx-a11y/alt-text: error' src/"
```

---

## Common Violations & Fixes

### 1. Missing Form Labels

**Violation**: `form-elements-have-labels`

```tsx
// ❌ BAD
<input type="email" placeholder="Email" />

// ✅ FIX
<label htmlFor="email">Email</label>
<input id="email" type="email" />

// ✅ OR (visually hidden label)
<label htmlFor="email" className="sr-only">Email</label>
<input id="email" type="email" placeholder="Email" />
```

### 2. Low Color Contrast

**Violation**: `color-contrast`

```css
/* ❌ BAD */
.text-gray { color: #9ca3af; } /* 3.5:1 on white */

/* ✅ FIX */
.text-gray { color: #6b7280; } /* 4.6:1 on white */
```

### 3. Empty Button/Link

**Violation**: `button-name`, `link-name`

```tsx
// ❌ BAD
<button onClick={handleClose}><XIcon /></button>

// ✅ FIX
<button onClick={handleClose} aria-label="Close dialog">
  <XIcon aria-hidden="true" />
</button>
```

### 4. Missing Alt Text

**Violation**: `image-alt`

```tsx
// ❌ BAD
<img src="/game.jpg" />

// ✅ FIX (meaningful image)
<img src="/game.jpg" alt="Catan board game box cover" />

// ✅ FIX (decorative image)
<img src="/decorative.svg" alt="" role="presentation" />
```

### 5. Non-Semantic Elements

**Violation**: Various keyboard/focus issues

```tsx
// ❌ BAD
<div onClick={handleClick} className="button">Click me</div>

// ✅ FIX
<button onClick={handleClick}>Click me</button>
```

### 6. Heading Hierarchy

**Violation**: `heading-order`

```tsx
// ❌ BAD (skips h2)
<h1>Page Title</h1>
<h3>Section</h3>

// ✅ FIX
<h1>Page Title</h1>
<h2>Section</h2>
```

### 7. Missing Landmark Regions

**Violation**: `landmark-one-main`, `bypass`

```tsx
// ❌ BAD
<div className="content">...</div>

// ✅ FIX
<main id="main-content">...</main>
<nav aria-label="Main navigation">...</nav>
```

---

## Resources

### WCAG References

- [WCAG 2.1 Quick Reference](https://www.w3.org/WAI/WCAG21/quickref/?levels=aa)
- [WebAIM WCAG Checklist](https://webaim.org/standards/wcag/checklist)
- [MDN Accessibility Guide](https://developer.mozilla.org/en-US/docs/Web/Accessibility)

### Testing Tools

| Tool | Type | Usage |
|------|------|-------|
| axe DevTools | Browser extension | Manual audits |
| WAVE | Browser extension | Visual accessibility checker |
| Lighthouse | Chrome DevTools | Accessibility scoring |
| jest-axe | Unit testing | Component tests |
| @axe-core/playwright | E2E testing | Page tests |
| @axe-core/react | Runtime | Dev mode warnings |

### Screen Readers

| Platform | Screen Reader | Install |
|----------|---------------|---------|
| Windows | NVDA | https://www.nvaccess.org/ (free) |
| Windows | JAWS | Commercial license |
| macOS | VoiceOver | Built-in (Cmd+F5) |
| Linux | Orca | Built-in on GNOME |

### Contrast Checkers

- Chrome DevTools → Inspect → Accessibility pane
- [WebAIM Contrast Checker](https://webaim.org/resources/contrastchecker/)
- [Colour Contrast Analyser](https://www.tpgi.com/color-contrast-checker/)

---

## Project Files

### Accessible Components
- `apps/web/src/components/accessible/AccessibleButton.tsx`
- `apps/web/src/components/accessible/AccessibleModal.tsx`
- `apps/web/src/components/accessible/AccessibleFormInput.tsx`
- `apps/web/src/components/accessible/AccessibleSkipLink.tsx`
- `apps/web/src/components/accessible/README.md`

### Test Files
- `apps/web/e2e/accessibility.spec.ts` - E2E accessibility tests
- `apps/web/src/components/**/__tests__/*.a11y.test.tsx` - Unit a11y tests
- `apps/web/scripts/run-accessibility-audit.ts` - Full audit script

### Documentation
- This guide: `docs/05-testing/accessibility-guidelines.md`
- Component docs: `apps/web/src/components/accessible/README.md`

---

**Issue**: #2929 - Accessibility Audit & WCAG 2.1 AA Compliance
**Last Updated**: 2026-02-01
**Maintainer**: Frontend Team

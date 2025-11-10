# Accessibility Testing Guide

**Issue**: #841
**Epic**: #844 (UI/UX Testing Roadmap 2025)
**Last Updated**: 2025-11-10

---

## Overview

This guide covers automated accessibility testing using axe-core and Playwright to ensure WCAG 2.1 AA/AAA compliance for MeepleAI application.

**Key Principle**: Automated testing catches ~40% of accessibility issues. Manual testing is still required for complete coverage.

---

## Quick Start

### Running Accessibility Tests

```bash
# Run all accessibility tests
cd apps/web
pnpm test:a11y

# Run specific accessibility test
pnpm exec playwright test accessibility.spec.ts

# Run with UI mode (interactive)
pnpm exec playwright test accessibility.spec.ts --ui

# Run specific test by name
pnpm exec playwright test accessibility.spec.ts --grep "Landing page"
```

### Viewing Test Results

```bash
# Open HTML report (after test run)
pnpm exec playwright show-report

# View in CI
# Check GitHub Actions artifacts for failed tests
```

---

## WCAG Compliance Standards

### WCAG 2.1 Levels

| Level | Description | Requirements | Target |
|-------|-------------|--------------|--------|
| **A** | Minimum | Basic accessibility | Baseline |
| **AA** | Recommended | Standard for most websites | ✅ MeepleAI Target |
| **AAA** | Enhanced | Highest level of compliance | Aspirational |

### Key Requirements (WCAG 2.1 AA)

**Color Contrast** (1.4.3):
- Normal text (< 18pt): **4.5:1** minimum contrast ratio
- Large text (≥ 18pt or ≥ 14pt bold): **3:1** minimum

**Keyboard Navigation** (2.1.1):
- All functionality available via keyboard
- No keyboard traps
- Focus indicators visible

**Forms** (3.3.2):
- Labels or instructions provided
- Error identification
- Error suggestions

**ARIA** (4.1.2):
- Proper roles, states, and properties
- Valid ARIA attributes
- Semantic HTML preferred

---

## Test Suite Structure

### Test Categories

Our accessibility test suite (`e2e/accessibility.spec.ts`) covers:

1. **WCAG 2.1 AA Compliance** (4 tests)
   - Landing page
   - Chess page
   - Chat page (unauthenticated)
   - Setup page (unauthenticated)

2. **Keyboard Navigation** (3 tests)
   - Navigate with Tab key
   - Activate buttons with Enter
   - Close modals with ESC

3. **Focus Indicators** (2 tests)
   - Buttons have visible focus
   - Links have visible focus

4. **Screen Reader / Semantic HTML** (3 tests)
   - Proper heading hierarchy
   - Main landmark present
   - Form labels associated

5. **Auth Modal** (1 test)
   - Modal accessibility when open

**Total**: 13 tests

### Helper Function

```typescript
/**
 * Run axe accessibility scan on current page
 * @param page - Playwright page object
 * @param pageName - Human-readable page name for logging
 */
async function checkAccessibility(page: any, pageName: string) {
  const accessibilityScanResults = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
    .analyze();

  if (accessibilityScanResults.violations.length > 0) {
    console.error(`Accessibility violations on ${pageName}:`);
    accessibilityScanResults.violations.forEach((violation) => {
      console.error(`- ${violation.id}: ${violation.description}`);
      console.error(`  Impact: ${violation.impact}`);
      console.error(`  Help: ${violation.helpUrl}`);
      console.error(`  Nodes affected: ${violation.nodes.length}`);
    });
  }

  expect(accessibilityScanResults.violations).toEqual([]);
}
```

---

## Adding New Accessibility Tests

### Pattern 1: Public Page Test

```typescript
test('your-page should not have accessibility violations', async ({ page }) => {
  await page.goto('/your-page');
  await page.waitForLoadState('networkidle');

  const results = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
    .analyze();

  if (results.violations.length > 0) {
    console.log('Violations found:', formatViolations(results.violations));
  }

  expect(results.violations).toEqual([]);
});
```

### Pattern 2: Authenticated Page Test

```typescript
test.describe('Authenticated Pages', () => {
  test.beforeEach(async ({ page }) => {
    // Login before each test
    await page.goto('/login');
    await page.getByLabel('Email').fill('user@meepleai.dev');
    await page.getByLabel('Password').fill('Demo123!');
    await page.getByRole('button', { name: /login/i }).click();
    await page.waitForURL(/\/(chat|dashboard)/);
  });

  test('dashboard should not have violations', async ({ page }) => {
    await page.goto('/dashboard');
    await page.waitForLoadState('networkidle');

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .analyze();

    expect(results.violations).toEqual([]);
  });
});
```

### Pattern 3: Dynamic Content Test

```typescript
test('modal should be accessible', async ({ page }) => {
  await page.goto('/');

  // Open modal
  await page.getByRole('button', { name: 'Open Modal' }).click();
  await page.waitForSelector('[role="dialog"]');

  // Scan modal content
  const results = await new AxeBuilder({ page })
    .include('[role="dialog"]') // Scan only modal
    .analyze();

  expect(results.violations).toEqual([]);
});
```

---

## Common Violations & Fixes

### 1. Color Contrast Violations

**Violation**: `color-contrast`
**Impact**: SERIOUS
**WCAG Rule**: 1.4.3 Contrast (Minimum)

**Example**:
```
Element has insufficient color contrast of 1.86
(foreground: #393d4c, background: #020618)
Expected: 4.5:1
```

**Fixes**:

**Option A: Use lighter text colors**
```tsx
// Before
<p className="text-slate-400">Text</p>

// After
<p className="text-slate-50">Text</p>
```

**Option B: Use darker backgrounds**
```tsx
// Before
<div className="bg-slate-950">

// After
<div className="bg-slate-900">
```

**Option C: Increase opacity for semi-transparent elements**
```tsx
// Before
<code className="bg-white/20 text-slate-200">

// After
<code className="bg-slate-700 text-white">
```

**Verification Tools**:
- [WebAIM Contrast Checker](https://webaim.org/resources/contrastchecker/)
- Chrome DevTools > Inspect > Accessibility pane
- axe DevTools browser extension

### 2. Missing Form Labels

**Violation**: `label`
**Impact**: CRITICAL
**WCAG Rule**: 3.3.2 Labels or Instructions

**Fix**:
```tsx
// Before
<input type="email" placeholder="Email" />

// After
<label htmlFor="email-input">Email</label>
<input id="email-input" type="email" placeholder="Email" />

// Or with aria-label
<input type="email" aria-label="Email address" placeholder="Email" />
```

### 3. Missing Alt Text

**Violation**: `image-alt`
**Impact**: SERIOUS
**WCAG Rule**: 1.1.1 Non-text Content

**Fix**:
```tsx
// Before
<img src="/logo.png" />

// After
<img src="/logo.png" alt="MeepleAI Logo" />

// For decorative images
<img src="/decoration.png" alt="" role="presentation" />
```

### 4. Insufficient Focus Indicators

**Violation**: `focus-visible`
**Impact**: SERIOUS
**WCAG Rule**: 2.4.7 Focus Visible

**Fix**:
```css
/* Add visible focus styles */
.btn-primary:focus-visible {
  outline: 3px solid #60a5fa;
  outline-offset: 2px;
}

/* Or use Tailwind */
.btn-primary {
  @apply focus:outline-none focus:ring-4 focus:ring-primary-300;
}
```

### 5. Invalid ARIA Attributes

**Violation**: `aria-valid-attr-value`
**Impact**: SERIOUS
**WCAG Rule**: 4.1.2 Name, Role, Value

**Fix**:
```tsx
// Before
<button aria-expanded="yes">

// After
<button aria-expanded="true">

// Before
<div aria-labelledby="nonexistent">

// After
<div aria-labelledby="heading-id">
<h2 id="heading-id">Heading</h2>
```

---

## CI Integration

### Existing Workflow

Accessibility tests run automatically in CI:

**Workflow**: `.github/workflows/ci.yml`
**Job**: `accessibility-tests`
**Triggers**: Pull requests, pushes to main
**Command**: `pnpm test:e2e e2e/accessibility.spec.ts`

### CI Behavior

**On Success**: ✅ Workflow passes
**On Failure**: ❌ Workflow fails, PR blocked
**Artifacts**: Playwright HTML report uploaded (7-day retention)

### Viewing CI Results

1. Go to GitHub Actions tab
2. Select failed workflow run
3. Download `playwright-a11y-report-*` artifact
4. Extract and open `index.html` in browser

---

## Manual Testing Checklist

### Keyboard Navigation

- [ ] Tab through all interactive elements
- [ ] Focus indicators are visible
- [ ] Tab order is logical
- [ ] No keyboard traps
- [ ] Enter/Space activate buttons
- [ ] ESC closes modals

### Screen Reader Testing

**Tools**: NVDA (Windows), JAWS (Windows), VoiceOver (macOS)

- [ ] All images have alt text
- [ ] All form inputs have labels
- [ ] Headings are in logical order (H1 → H2 → H3)
- [ ] Links have descriptive text
- [ ] Buttons have accessible names
- [ ] Landmarks are properly defined (main, nav, aside, footer)

### Color & Contrast

- [ ] Text has sufficient contrast (4.5:1 for normal, 3:1 for large)
- [ ] Interactive elements have contrast (3:1 minimum)
- [ ] Color is not the only visual means of conveying information

### Forms

- [ ] All inputs have associated labels
- [ ] Error messages are announced
- [ ] Required fields are indicated
- [ ] Form validation is accessible

---

## Troubleshooting

### Issue: Tests Fail Locally But Pass in CI

**Cause**: Local environment differences (dark mode, extensions, cached state)

**Solution**:
```bash
# Clear Playwright cache
pnpm exec playwright install --with-deps chromium

# Use incognito mode
# (Add to playwright.config.ts)
use: {
  launchOptions: {
    args: ['--incognito']
  }
}
```

### Issue: Too Many Violations Found

**Cause**: Page has significant accessibility issues

**Solution**:
1. Run axe DevTools browser extension manually
2. Prioritize by impact (critical > serious > moderate > minor)
3. Fix critical and serious first
4. Create follow-up issues for moderate/minor
5. Document known violations and timeline for fixes

### Issue: False Positives

**Cause**: axe-core doesn't understand context

**Solution**:
```typescript
// Disable specific rules if false positive
const results = await new AxeBuilder({ page })
  .withTags(['wcag2a', 'wcag2aa'])
  .disableRules(['color-contrast']) // Only if confirmed false positive
  .analyze();
```

**Warning**: Only disable rules after manual verification that they are false positives!

### Issue: Dynamic Content Not Scanned

**Cause**: Content loads after page scan

**Solution**:
```typescript
// Wait for dynamic content to load
await page.waitForSelector('[data-testid="dynamic-content"]');

// Then scan
const results = await new AxeBuilder({ page }).analyze();
```

---

## Best Practices

### 1. Test at Multiple States

```typescript
// Test both empty and filled states
test('form accessibility - empty state', async ({ page }) => { /* ... */ });
test('form accessibility - filled state', async ({ page }) => { /* ... */ });
test('form accessibility - error state', async ({ page }) => { /* ... */ });
```

### 2. Use Semantic Selectors

```typescript
// ✅ Good - Accessible selectors
page.getByRole('button', { name: 'Submit' })
page.getByLabel('Email')
page.getByText('Welcome')

// ❌ Bad - Implementation-dependent
page.locator('.btn-submit')
page.locator('#email-input')
```

### 3. Test Focus Management

```typescript
test('focus moves to modal when opened', async ({ page }) => {
  await page.getByRole('button', { name: 'Open Modal' }).click();

  // Verify focus moved to modal
  const focusedElement = await page.evaluate(() =>
    document.activeElement?.getAttribute('role')
  );
  expect(focusedElement).toBe('dialog');
});
```

### 4. Test Keyboard Shortcuts

```typescript
test('ESC closes modal', async ({ page }) => {
  await page.getByRole('button', { name: 'Open' }).click();
  await page.keyboard.press('Escape');

  // Modal should be closed
  const modalVisible = await page.locator('[role="dialog"]').isVisible();
  expect(modalVisible).toBe(false);
});
```

---

## Accessible Component Patterns

### Accessible Button

```tsx
<button
  onClick={handleClick}
  aria-label="Close dialog" // If no visible text
  className="btn-primary focus:outline-none focus:ring-4 focus:ring-primary-300"
>
  ✕
</button>
```

### Accessible Form

```tsx
<form onSubmit={handleSubmit}>
  <label htmlFor="email" className="block mb-2">
    Email Address
  </label>
  <input
    id="email"
    type="email"
    aria-required="true"
    aria-invalid={hasError}
    aria-describedby={hasError ? "email-error" : undefined}
  />
  {hasError && (
    <p id="email-error" role="alert" className="text-red-500">
      Please enter a valid email
    </p>
  )}
</form>
```

### Accessible Modal

```tsx
<Dialog
  open={isOpen}
  onClose={handleClose}
  aria-labelledby="modal-title"
  aria-describedby="modal-description"
>
  <h2 id="modal-title">Modal Title</h2>
  <p id="modal-description">Modal description</p>

  {/* Focus trap automatically managed by Dialog component */}
</Dialog>
```

### Accessible Navigation

```tsx
<nav aria-label="Main navigation">
  <ul>
    <li>
      <Link href="/" aria-current={isActive('/') ? 'page' : undefined}>
        Home
      </Link>
    </li>
  </ul>
</nav>
```

---

## CSS Classes for Accessibility

### Current Accessible Classes

**Defined in** `apps/web/src/styles/globals.css`:

```css
/* WCAG AA compliant gradient text */
.gradient-text-accessible {
  @apply bg-gradient-to-r from-blue-400 via-purple-400 to-pink-400
         bg-clip-text text-transparent;
}

/* WCAG AA compliant secondary button */
.btn-secondary-accessible {
  @apply border-2 border-blue-400 text-blue-300 font-semibold py-3 px-6
         rounded-lg transition-all duration-200 hover:border-blue-300
         hover:bg-blue-400/10 active:scale-95;
}
```

### Color Palette Guidelines

**For dark backgrounds** (bg-slate-950, bg-slate-900):

| Text Color | Contrast on bg-slate-950 | WCAG AA Status |
|------------|--------------------------|----------------|
| `text-slate-50` | 15.89 | ✅ PASS (4.5:1) |
| `text-slate-100` | 14.91 | ✅ PASS (4.5:1) |
| `text-slate-200` | 12.89 | ✅ PASS (4.5:1) |
| `text-slate-300` | 10.05 | ✅ PASS (4.5:1) |
| `text-slate-400` | 6.12 | ✅ PASS (4.5:1) |
| `text-slate-500` | 3.76 | ❌ FAIL (4.5:1) |

**Recommendation**: Use `text-slate-50` to `text-slate-400` for body text on dark backgrounds.

**For light backgrounds** (bg-white, bg-slate-50):

| Text Color | Contrast | WCAG AA Status |
|------------|----------|----------------|
| `text-slate-900` | 16.2 | ✅ PASS |
| `text-slate-800` | 13.5 | ✅ PASS |
| `text-slate-700` | 10.8 | ✅ PASS |
| `text-slate-600` | 7.2 | ✅ PASS |

---

## Interpreting axe-core Results

### Violation Object Structure

```typescript
{
  id: 'color-contrast',           // Violation type
  impact: 'serious',              // critical | serious | moderate | minor
  description: 'Ensure...',       // Description
  help: 'Elements must...',       // How to fix
  helpUrl: 'https://...',         // Documentation link
  tags: ['wcag2aa', 'wcag143'],  // WCAG criteria
  nodes: [                        // Affected elements
    {
      html: '<p class="...">',    // Element HTML
      target: ['.text-slate-400'], // CSS selector
      failureSummary: 'Fix...',   // What failed
      any: [{                     // Specific violation data
        data: {
          fgColor: '#393d4c',
          bgColor: '#020618',
          contrastRatio: 1.86,
          expectedContrastRatio: '4.5:1'
        }
      }]
    }
  ]
}
```

### Impact Levels

| Impact | Priority | Action Required |
|--------|----------|-----------------|
| **critical** | P0 | Fix immediately, blocks release |
| **serious** | P1 | Fix before release |
| **moderate** | P2 | Fix in next sprint |
| **minor** | P3 | Fix when convenient |

---

## CI Failure Runbook

### When Accessibility Tests Fail in CI

**1. Check the failure**:
```bash
# Go to GitHub Actions
# Click on failed workflow
# View "Run Playwright Accessibility Tests" step
```

**2. Download the report**:
```bash
# Download playwright-a11y-report-* artifact
# Extract ZIP
# Open index.html in browser
```

**3. Analyze violations**:
- Identify which test failed
- Review violation details (element, contrast ratio, WCAG rule)
- Determine impact level

**4. Fix locally**:
```bash
# Reproduce locally
cd apps/web
pnpm test:a11y

# Fix violations in code
# Re-run tests
pnpm test:a11y

# Verify fix
# All tests should pass
```

**5. Commit and push**:
```bash
git add .
git commit -m "fix(a11y): Fix color contrast violation on homepage"
git push
```

### Emergency: Temporarily Disable Check

**Only if**:
- Critical production issue
- Fix requires design team input
- Violation is false positive (confirmed by accessibility expert)

**How**:
```typescript
// In accessibility.spec.ts
test.skip('problematic test', async ({ page }) => {
  // Temporarily skipped - Issue #XXX
});
```

**Requirements**:
- Create GitHub issue documenting why skipped
- Set deadline for fix
- Add TODO comment with issue number
- Get approval from tech lead

---

## Resources

### Tools

**Automated Testing**:
- [axe-core](https://github.com/dequelabs/axe-core) - Our testing library
- [axe DevTools](https://www.deque.com/axe/devtools/) - Browser extension for manual audits
- [Lighthouse](https://developer.chrome.com/docs/lighthouse) - Chrome built-in audits
- [WAVE](https://wave.webaim.org/) - Visual accessibility feedback

**Manual Testing**:
- [NVDA](https://www.nvaccess.org/) - Free screen reader (Windows)
- [VoiceOver](https://www.apple.com/accessibility/voiceover/) - Built-in screen reader (macOS)
- [JAWS](https://www.freedomscientific.com/products/software/jaws/) - Commercial screen reader

**Color Contrast**:
- [WebAIM Contrast Checker](https://webaim.org/resources/contrastchecker/)
- [Coolors Contrast Checker](https://coolors.co/contrast-checker)
- Chrome DevTools Accessibility pane

### Documentation

- [WCAG 2.2 Quick Reference](https://www.w3.org/WAI/WCAG22/quickref/)
- [Playwright Accessibility Testing](https://playwright.dev/docs/accessibility-testing)
- [axe-core Rule Descriptions](https://github.com/dequelabs/axe-core/blob/develop/doc/rule-descriptions.md)
- [WebAIM Articles](https://webaim.org/articles/)

### Internal Documentation

- Research: `claudedocs/research_automated_ui_ux_testing_2025-11-10.md`
- Violations Analysis: `docs/issue/issue-841-accessibility-violations-analysis.md`
- Implementation Status: `docs/issue/issue-841-phase1-implementation-status.md`

---

## FAQ

### Q: Why do we test accessibility?

**A**: Three reasons:
1. **Legal compliance**: ADA, Section 508, European Accessibility Act
2. **Better UX**: Benefits all users (keyboard navigation, clear focus, high contrast)
3. **SEO boost**: Google considers accessibility in rankings

### Q: Can I skip accessibility tests?

**A**: No. Accessibility tests are mandatory and integrated into CI. PRs with violations will be blocked.

### Q: What if I need to ship urgently?

**A**: Create a follow-up issue and get approval from tech lead to skip temporarily. Document the violation and timeline for fix.

### Q: How do I fix a violation I don't understand?

**A**:
1. Click the `helpUrl` in violation output
2. Check axe-core rule documentation
3. Use axe DevTools browser extension for interactive guidance
4. Ask in team chat or consult accessibility expert

### Q: What about mobile accessibility?

**A**: Current tests cover desktop. Mobile-specific testing (touch targets, zoom, orientation) is in roadmap (Issue #843).

---

## Next Steps

### For Developers

1. **Before committing**: Run `pnpm test:a11y` locally
2. **If violations found**: Fix before pushing
3. **If unsure**: Consult this guide or axe-core docs

### For Reviewers

1. **Check CI**: Verify accessibility tests passed
2. **Manual spot check**: Test keyboard navigation on changed pages
3. **Request changes**: If accessibility impacted negatively

### Roadmap

- [ ] Add authenticated page tests (#841 Phase 2)
- [ ] Expand to 15+ critical pages
- [ ] Add mobile accessibility tests (#843)
- [ ] Integrate Lighthouse accessibility scoring (#842)
- [ ] Setup accessibility monitoring dashboard

---

**Maintained by**: Engineering Team
**Questions**: Create issue with `accessibility` label
**Last Review**: 2025-11-10

# Responsive Testing Guide

**Issue #993** - [P1] [BGAI-052] Responsive design testing (320px-1920px)

This guide explains how to test responsive layouts across all breakpoints using assertion-based behavioral testing.

## Overview

We use **assertion-based testing** (NOT visual regression) to verify responsive behavior across:

- **Mobile**: 390×844px (iPhone 13)
- **Tablet**: 1024×1366px (iPad Pro)
- **Desktop**: 1920×1080px

### Why Assertion-Based?

✅ **Robust**: Not affected by minor UI changes
✅ **Fast**: No screenshot rendering overhead
✅ **Maintainable**: No baseline screenshots to update
✅ **Focused**: Tests behavior, not pixels
✅ **CI-Friendly**: Consistent across environments

❌ **NOT Visual Regression**: We do NOT use screenshot comparison due to:
- Screenshot instability (animations, dynamic content)
- High maintenance burden (baseline updates)
- Flaky tests across environments
- Slower CI execution

## Architecture

### Viewport Projects

`apps/web/playwright.config.ts` defines 3 viewport projects:

```typescript
projects: [
  {
    name: 'mobile',
    use: {
      ...devices['iPhone 13'],
      viewport: { width: 390, height: 844 },
    },
  },
  {
    name: 'tablet',
    use: {
      ...devices['iPad Pro'],
      viewport: { width: 1024, height: 1366 },
    },
  },
  {
    name: 'desktop',
    use: {
      ...devices['Desktop Chrome'],
      viewport: { width: 1920, height: 1080 },
    },
  },
]
```

### Helper Utilities

`apps/web/e2e/helpers/responsive-utils.ts` provides reusable functions:

```typescript
// Layout verification
checkMobileLayout(page)   // Verifies mobile-specific behavior
checkTabletLayout(page)   // Verifies tablet-specific behavior
checkDesktopLayout(page)  // Verifies desktop-specific behavior

// Quality checks
checkNoHorizontalOverflow(page)      // No horizontal scrollbars
checkTextReadability(page, minSize)   // Text size >= 14px
checkTouchTargets(page, minSize)      // Touch targets >= 44×44px
```

## Writing Responsive Tests

### Test Structure

```typescript
import { test, expect } from '@playwright/test';
import {
  checkMobileLayout,
  checkTabletLayout,
  checkDesktopLayout,
  checkNoHorizontalOverflow,
} from './helpers/responsive-utils';

test.describe('MyComponent Responsive', () => {
  test('should work on mobile', async ({ page }) => {
    await page.goto('/my-page');
    await page.waitForLoadState('networkidle');

    // Verify mobile layout
    const { viewport } = await checkMobileLayout(page);
    expect(viewport.isMobile).toBe(true);

    // No horizontal overflow
    await checkNoHorizontalOverflow(page);

    // Component-specific assertions
    const mobileNav = page.locator('[data-testid="mobile-nav"]');
    await expect(mobileNav).toBeVisible();
  });

  test('should work on tablet', async ({ page }) => {
    await page.goto('/my-page');
    const { viewport } = await checkTabletLayout(page);
    expect(viewport.isTablet).toBe(true);
    await checkNoHorizontalOverflow(page);
  });

  test('should work on desktop', async ({ page }) => {
    await page.goto('/my-page');
    const { viewport } = await checkDesktopLayout(page);
    expect(viewport.isDesktop).toBe(true);
    await checkNoHorizontalOverflow(page);
  });
});
```

### What to Test

#### Mobile (390px)
- ✅ Navigation: hamburger menu visible, sidebar hidden
- ✅ Layout: single column, stacked content
- ✅ Touch targets: ≥44×44px
- ✅ Text: readable (≥14px)
- ✅ No horizontal overflow

#### Tablet (1024px)
- ✅ Navigation: visible or collapsible
- ✅ Layout: adaptive grid (2 columns vs 3)
- ✅ Content: readable without overflow

#### Desktop (1920px)
- ✅ Sidebar: fully visible
- ✅ Navigation: all elements visible
- ✅ Layout: multi-column optimized
- ✅ Content: no wasted space

### Common Patterns

#### Authenticated Pages

```typescript
test.beforeEach(async ({ page }) => {
  // Login first
  await page.goto('/auth/login');
  await page.waitForSelector('input[type="email"]');
  await page.fill('input[type="email"]', 'admin@meepleai.dev');
  await page.fill('input[type="password"]', 'Demo123!');
  await page.click('button[type="submit"]');
  await page.waitForURL('/');
});

test('should display settings on mobile', async ({ page }) => {
  await page.goto('/settings');
  await checkMobileLayout(page);
  // ... assertions
});
```

#### Conditional Elements

```typescript
test('sidebar should be hidden on mobile', async ({ page }) => {
  await page.goto('/');

  const sidebar = page.locator('[role="complementary"], aside').first();
  const sidebarCount = await sidebar.count();

  if (sidebarCount > 0) {
    const isHidden = await sidebar.isHidden().catch(() => true);
    expect(isHidden).toBe(true);
  }
});
```

## Running Tests

### All Viewports

```bash
cd apps/web
pnpm playwright test responsive-layout
```

### Specific Viewport

```bash
# Mobile only
pnpm playwright test responsive-layout --project=mobile

# Tablet only
pnpm playwright test responsive-layout --project=tablet

# Desktop only
pnpm playwright test responsive-layout --project=desktop
```

### Watch Mode

```bash
pnpm playwright test responsive-layout --ui
```

### Debug Mode

```bash
pnpm playwright test responsive-layout --debug
```

## CI Integration

Tests run automatically in GitHub Actions:

```yaml
# .github/workflows/ci.yml
- name: Run E2E tests
  run: cd apps/web && pnpm playwright test
```

All 3 viewport projects run in parallel for optimal performance.

## Best Practices

### ✅ DO

- Use behavioral assertions (visibility, layout flow)
- Test critical user journeys across viewports
- Verify touch targets on mobile/tablet
- Check for horizontal overflow
- Test navigation accessibility
- Use helper utilities for consistency

### ❌ DON'T

- Use screenshot comparison (unstable)
- Test pixel-perfect layouts
- Hardcode specific pixel values in assertions
- Assume element positions (use visibility/accessibility)
- Skip viewport-specific behavior tests

## Troubleshooting

### Test Timeouts

Increase timeout for slow pages:

```typescript
test('slow page', async ({ page }) => {
  test.setTimeout(60000); // 60s
  await page.goto('/slow-page');
  await page.waitForLoadState('networkidle');
});
```

### Element Not Found

Check viewport-specific selectors:

```typescript
// Mobile-specific element
const mobileNav = page.locator('[data-mobile-nav]');
if (await mobileNav.count() > 0) {
  await expect(mobileNav).toBeVisible();
}
```

### Flaky Tests

Add explicit waits:

```typescript
await page.waitForSelector('[data-testid="content"]');
await page.waitForLoadState('networkidle');
await checkMobileLayout(page);
```

## Examples

See `apps/web/e2e/responsive-layout.spec.ts` for complete examples:

- HomePage responsive tests
- Chat page responsive tests
- Settings page responsive tests
- Navigation component tests
- Cross-viewport consistency tests

## References

- [Playwright Testing Docs](https://playwright.dev/docs/intro)
- [Tailwind Breakpoints](https://tailwindcss.com/docs/responsive-design)
- [WCAG Touch Target Size](https://www.w3.org/WAI/WCAG21/Understanding/target-size.html)
- [Issue #993](https://github.com/meepleai/meepleai-monorepo/issues/993)

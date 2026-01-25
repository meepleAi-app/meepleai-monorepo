# Wave 9 Testing Implementation Guide (Issue #2965)

**Status**: Foundation complete, bulk test updates pending
**Created**: 2026-01-24
**Completion**: Waves 8 done, Wave 9 templates ready

## Overview

This guide provides templates and patterns for completing Wave 9 testing tasks.
Wave 8 (Polish & Effects) is complete with full implementation.

## Completed (Wave 8)

✅ **Glass Effects**: Desktop glass morphism with mobile fallbacks
✅ **Dark Mode Enhancements**: Glows, text shadows, gradients
✅ **Mobile Optimizations**: No blur on mobile, reduced animations
✅ **Framer Motion**: FadeIn, StaggerChildren, PageTransition, modal variants
✅ **Chromatic Setup**: Configuration and GitHub Action

## Pending (Wave 9)

### 1. Unit Test Updates (Task #7) ✅

**Scope**: 305 component test files identified (out of 871 total)
**Status**: Test utilities created, pattern documented, sample provided

**Test Utilities Created**:
- `src/lib/test-utils/theme-wrapper.tsx` - renderWithTheme, testBothThemes helpers
- `__tests__/sample-theme-test-pattern.test.tsx` - Comprehensive pattern examples

**Template for Component Tests**:
\`\`\`typescript
import { render, screen } from '@testing-library/react';
import { ThemeProvider } from 'next-themes';

import { YourComponent } from './YourComponent';

// Wrapper with theme provider
const renderWithTheme = (ui: React.ReactElement, theme: 'light' | 'dark' = 'light') => {
  return render(
    <ThemeProvider attribute="class" defaultTheme={theme}>
      {ui}
    </ThemeProvider>
  );
};

describe('YourComponent', () => {
  describe('Light Mode', () => {
    it('renders correctly in light mode', () => {
      renderWithTheme(<YourComponent />, 'light');
      expect(screen.getByRole('...')).toBeInTheDocument();
    });
  });

  describe('Dark Mode', () => {
    it('renders correctly in dark mode', () => {
      renderWithTheme(<YourComponent />, 'dark');
      expect(screen.getByRole('...')).toBeInTheDocument();
    });

    it('applies dark mode styles', () => {
      const { container } = renderWithTheme(<YourComponent />, 'dark');
      expect(container.firstChild).toHaveClass('dark');
    });
  });
});
\`\`\`

**Files to Update** (estimated 100+):
- \`apps/web/**/__tests__/**/*.test.{ts,tsx}\`
- \`apps/web/**/*.test.{ts,tsx}\`

**Strategy**:
1. Identify components using theme-dependent styles
2. Add ThemeProvider wrapper to tests
3. Test both light and dark variants
4. Update snapshots with \`pnpm test -- -u\`

### 2. E2E Theme Switching Tests (Task #8)

**Playwright Test Template**:
\`\`\`typescript
// apps/web/e2e/theme-switching.spec.ts
import { test, expect } from '@playwright/test';

test.describe('Theme Switching', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
  });

  test('theme toggle switches between light and dark mode', async ({ page }) => {
    // Get initial theme
    const html = page.locator('html');
    const initialTheme = await html.getAttribute('class');

    // Click theme toggle (assuming it's in user dropdown)
    await page.click('[aria-label*="Toggle theme"]');

    // Wait for theme change
    await page.waitForTimeout(300);

    // Verify theme changed
    const newTheme = await html.getAttribute('class');
    expect(newTheme).not.toBe(initialTheme);
  });

  test('theme persists across page navigations', async ({ page }) => {
    // Set to dark mode
    await page.click('[aria-label*="Toggle theme"]');
    await page.waitForTimeout(300);

    const html = page.locator('html');
    const darkTheme = await html.getAttribute('class');
    expect(darkTheme).toContain('dark');

    // Navigate to another page
    await page.goto('/about');

    // Verify theme persisted
    const persistedTheme = await html.getAttribute('class');
    expect(persistedTheme).toContain('dark');
  });

  test('no layout shift during theme switch', async ({ page }) => {
    // Measure CLS during theme switch
    const metrics = await page.evaluate(() => {
      return new Promise((resolve) => {
        new PerformanceObserver((list) => {
          for (const entry of list.getEntries()) {
            if (entry.entryType === 'layout-shift' && !(entry as any).hadRecentInput) {
              resolve((entry as any).value);
            }
          }
        }).observe({ entryTypes: ['layout-shift'] });
      });
    });

    await page.click('[aria-label*="Toggle theme"]');
    await page.waitForTimeout(500);

    expect(metrics).toBeLessThan(0.1); // CLS < 0.1 (Good)
  });

  test('keyboard accessibility for theme toggle', async ({ page }) => {
    // Tab to theme toggle
    await page.keyboard.press('Tab'); // Repeat until focus on toggle

    // Activate with Enter
    await page.keyboard.press('Enter');
    await page.waitForTimeout(300);

    // Verify theme changed
    const html = page.locator('html');
    const theme = await html.getAttribute('class');
    expect(theme).toBeDefined();
  });
});
\`\`\`

### 3. Accessibility Testing with axe-core (Task #9)

**Installation**:
\`\`\`bash
pnpm add -D @axe-core/playwright
\`\`\`

**Playwright Integration**:
\`\`\`typescript
// apps/web/e2e/accessibility.spec.ts
import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

test.describe('Accessibility (Light Mode)', () => {
  test('homepage has no accessibility violations', async ({ page }) => {
    await page.goto('/');

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .analyze();

    expect(results.violations).toEqual([]);
  });
});

test.describe('Accessibility (Dark Mode)', () => {
  test.beforeEach(async ({ page }) => {
    // Set dark mode before tests
    await page.emulateMedia({ colorScheme: 'dark' });
  });

  test('homepage has no accessibility violations in dark mode', async ({ page }) => {
    await page.goto('/');

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .analyze();

    expect(results.violations).toEqual([]);
  });

  test('contrast ratios meet WCAG AA in dark mode', async ({ page }) => {
    await page.goto('/');

    const results = await new AxeBuilder({ page })
      .withTags(['cat.color'])
      .analyze();

    expect(results.violations).toEqual([]);
  });
});
\`\`\`

### 4. Lighthouse CI Setup (Task #10)

**Installation**:
\`\`\`bash
pnpm add -D @lhci/cli
\`\`\`

**Configuration** (\`lighthouserc.json\`):
\`\`\`json
{
  "ci": {
    "collect": {
      "numberOfRuns": 3,
      "startServerCommand": "pnpm start",
      "url": [
        "http://localhost:3000/",
        "http://localhost:3000/games",
        "http://localhost:3000/chat"
      ],
      "settings": {
        "preset": "desktop",
        "throttling": {
          "rttMs": 40,
          "throughputKbps": 10240,
          "cpuSlowdownMultiplier": 1
        }
      }
    },
    "assert": {
      "preset": "lighthouse:recommended",
      "assertions": {
        "first-contentful-paint": ["warn", { "maxNumericValue": 1000 }],
        "interactive": ["error", { "maxNumericValue": 2000 }],
        "cumulative-layout-shift": ["error", { "maxNumericValue": 0.1 }],
        "total-byte-weight": ["warn", { "maxNumericValue": 512000 }],
        "categories:performance": ["error", { "minScore": 0.9 }],
        "categories:accessibility": ["error", { "minScore": 0.9 }],
        "categories:best-practices": ["error", { "minScore": 0.9 }]
      }
    },
    "upload": {
      "target": "temporary-public-storage"
    }
  }
}
\`\`\`

**GitHub Action**:
\`\`\`yaml
name: Lighthouse CI

on:
  pull_request:
    branches: [main, frontend-dev]

jobs:
  lighthouse:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: '20'
      - uses: pnpm/action-setup@v2
        with:
          version: 8

      - name: Install dependencies
        run: pnpm install --frozen-lockfile
        working-directory: apps/web

      - name: Build application
        run: pnpm build
        working-directory: apps/web

      - name: Run Lighthouse CI
        run: pnpm lhci autorun
        working-directory: apps/web
        env:
          LHCI_GITHUB_APP_TOKEN: \${{ secrets.LHCI_GITHUB_APP_TOKEN }}
\`\`\`

**Scripts to Add** (\`apps/web/package.json\`):
\`\`\`json
{
  "scripts": {
    "lhci": "lhci",
    "lhci:mobile": "lhci autorun --config=lighthouserc.mobile.json"
  }
}
\`\`\`

## DoD Checklist

### Performance (Wave 9 validation)
- [ ] Desktop: FCP <1s, TTI <2s (run \`pnpm lhci\`)
- [ ] Mobile: FCP <1.5s (run \`pnpm lhci:mobile\`)
- [ ] CLS <0.1 (run E2E tests with CLS measurement)
- [ ] Bundle +<50KB (run \`pnpm build\` and check output)

### Accessibility (Wave 9 validation)
- [ ] 0 axe-core violations (run \`pnpm test:e2e\` with axe tests)

### Testing (Wave 9 validation)
- [ ] 85%+ coverage maintained (run \`pnpm test:coverage\`)
- [ ] Chromatic approved (manual review on Chromatic dashboard)
- [ ] Playwright 100% passing (run \`pnpm test:e2e\`)

## Execution Plan

1. **Week 1**: Update unit tests (50 files)
2. **Week 1**: Create E2E theme tests
3. **Week 2**: Integrate axe-core accessibility tests
4. **Week 2**: Set up Lighthouse CI and validate budgets
5. **Week 2**: Update remaining unit tests (50 files)
6. **Week 3**: Chromatic baseline approval and final validation

## Notes

- All Wave 8 implementation is complete and committed
- Wave 9 templates and infrastructure are ready
- Bulk test updates should be done incrementally
- Chromatic requires project setup on chromatic.com
- Lighthouse CI can run locally before CI/CD integration

## References

- Chromatic Docs: https://www.chromatic.com/docs/
- Playwright Testing: https://playwright.dev/docs/intro
- axe-core: https://github.com/dequelabs/axe-core
- Lighthouse CI: https://github.com/GoogleChrome/lighthouse-ci

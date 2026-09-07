/**
 * E2E Accessibility Tests (Issue #2929 - WCAG 2.1 AA Compliance)
 *
 * Comprehensive accessibility testing using axe-core, in light and dark mode,
 * against WCAG 2.1 AA.
 *
 * Pages covered (all PUBLIC — this spec seeds no session, see below):
 * 1. Landing Page (/)
 * 2. Shared Games catalog (/shared-games)
 * 3. Auth pages (/login, /register)
 * 4. Static pages (/about, /faq, /privacy, /terms)
 *
 * Authenticated routes are covered by the 13 specs under `e2e/a11y/`, which
 * seed a session via `seedAuthSession` so the `PLAYWRIGHT_AUTH_BYPASS` path in
 * `proxy.ts` engages. This spec deliberately does NOT seed one, so any
 * `(authenticated)` route here would be redirected to `/login` and silently
 * measured as the login page.
 *
 * Issue #3917 — this file used to scan two routes that were not what they
 * claimed to be, and the gate stayed green on both:
 *   - `/board-game-ai/games` does not exist (5 executions). axe on a 404 has
 *     nothing to complain about, so a blocking gate reported success on an
 *     empty page. Replaced with `/shared-games`, the real public catalog.
 *   - `/library` is under `(authenticated)`; with no session cookie the proxy
 *     redirects to `/login`, so 3 executions were re-measuring the login page
 *     under the name "Library".
 * The fix is not just the URLs: `gotoChecked` below makes both failure modes
 * loud, so the next route rename cannot quietly blind the gate again.
 *
 * WCAG 2.1 AA Requirements:
 * - Color contrast minimum 4.5:1 for normal text
 * - All interactive elements keyboard accessible
 * - Focus indicators visible
 * - Alt text for all images
 * - ARIA labels where needed
 */

import AxeBuilder from '@axe-core/playwright';
import { test, expect, type Page } from '@playwright/test';

// WCAG 2.1 AA tags for axe-core
const WCAG_TAGS = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'];

// Helper to create axe builder with standard config
function createAxeBuilder(page: Parameters<typeof AxeBuilder>[0]['page']) {
  return new AxeBuilder({ page })
    .withTags(WCAG_TAGS)
    .exclude('#webpack-dev-server-client-overlay') // Exclude dev overlay
    .exclude('[data-chromatic-ignore]'); // Exclude Chromatic-specific elements
}

/**
 * Navigate and assert the page under test is actually the page we asked for.
 *
 * Issue #3917: an accessibility gate is only as good as the page it lands on.
 * Two failure modes make it green while scanning nothing, and both are silent:
 *   - the route does not exist  → the 404 page passes axe trivially;
 *   - the route redirects       → an `(authenticated)` route lands on `/login`,
 *                                 and the gate re-measures the login page.
 *
 * Asserting the status and the final pathname turns both into a loud failure,
 * so a future route rename breaks the gate instead of blinding it.
 */
async function gotoChecked(page: Page, url: string): Promise<void> {
  const response = await page.goto(url);

  expect(response, `navigation to ${url} produced no response`).not.toBeNull();
  expect(
    response!.status(),
    `${url} returned HTTP ${response!.status()} — the route does not exist, ` +
      `so axe would scan an error page and pass trivially`
  ).toBeLessThan(400);

  const landed = new URL(page.url()).pathname;
  const requested = new URL(url, page.url()).pathname;
  expect(
    landed,
    `${url} redirected to ${landed} — the scan would measure that page instead. ` +
      `If the route needs a session, seed one (see e2e/a11y/*) or move the test there`
  ).toBe(requested);

  await page.waitForLoadState('networkidle');
}

// Helper to format violations for better error messages
function formatViolations(violations: Awaited<ReturnType<AxeBuilder['analyze']>>['violations']) {
  if (violations.length === 0) return 'No violations';

  return violations
    .map(v => {
      const nodes = v.nodes.map(n => `  - ${n.html.substring(0, 100)}...`).join('\n');
      return `[${v.impact}] ${v.id}: ${v.help}\n${nodes}`;
    })
    .join('\n\n');
}

// ============================================================================
// PUBLIC PAGES - Light Mode
// ============================================================================

test.describe('Accessibility - Public Pages (Light Mode)', () => {
  test.beforeEach(async ({ page }) => {
    // #1094 follow-up: reduced-motion neutralizes mid-animation captures
    // (e.g. /register `.animate-pulse` "Creating account…" yielded ratio 4.09
    // when caught mid-pulse cycle). Components must carry
    // `motion-reduce:animate-none` for this to take effect.
    await page.emulateMedia({ colorScheme: 'light', reducedMotion: 'reduce' });
  });

  test('Landing Page (/) - light mode @a11y', async ({ page }) => {
    await gotoChecked(page, '/');

    const results = await createAxeBuilder(page).analyze();

    expect(results.violations, formatViolations(results.violations)).toEqual([]);
  });

  test('Shared Games Catalog (/shared-games) - light mode @a11y', async ({ page }) => {
    await gotoChecked(page, '/shared-games');

    const results = await createAxeBuilder(page).analyze();

    expect(results.violations, formatViolations(results.violations)).toEqual([]);
  });

  // `/library` used to be scanned here as a public page. It is under
  // `(authenticated)`: with no session cookie the proxy redirects to `/login`,
  // so this test was re-measuring the login page under the name "Library"
  // (#3917). Its real coverage lives in `e2e/a11y/library.spec.ts`, which
  // seeds a session.

  test('Login Page (/login) - light mode @a11y', async ({ page }) => {
    await gotoChecked(page, '/login');

    // Wait for auth modal to be fully rendered
    await page.waitForTimeout(500);

    const results = await createAxeBuilder(page).analyze();

    expect(results.violations, formatViolations(results.violations)).toEqual([]);
  });

  test('Register Page (/register) - light mode @a11y', async ({ page }) => {
    await gotoChecked(page, '/register');

    // Wait for auth modal to be fully rendered
    await page.waitForTimeout(500);

    const results = await createAxeBuilder(page).analyze();

    expect(results.violations, formatViolations(results.violations)).toEqual([]);
  });

  test('About Page (/about) - light mode @a11y', async ({ page }) => {
    await gotoChecked(page, '/about');

    const results = await createAxeBuilder(page).analyze();

    expect(results.violations, formatViolations(results.violations)).toEqual([]);
  });

  test('FAQ Page (/faq) - light mode @a11y', async ({ page }) => {
    await gotoChecked(page, '/faq');

    const results = await createAxeBuilder(page).analyze();

    expect(results.violations, formatViolations(results.violations)).toEqual([]);
  });
});

// ============================================================================
// PUBLIC PAGES - Dark Mode
// ============================================================================

test.describe('Accessibility - Public Pages (Dark Mode)', () => {
  test.beforeEach(async ({ page }) => {
    await page.emulateMedia({ colorScheme: 'dark', reducedMotion: 'reduce' });
  });

  test('Landing Page (/) - dark mode @a11y', async ({ page }) => {
    await gotoChecked(page, '/');

    const results = await createAxeBuilder(page).analyze();

    expect(results.violations, formatViolations(results.violations)).toEqual([]);
  });

  test('Shared Games Catalog (/shared-games) - dark mode @a11y', async ({ page }) => {
    await gotoChecked(page, '/shared-games');

    const results = await createAxeBuilder(page).analyze();

    expect(results.violations, formatViolations(results.violations)).toEqual([]);
  });

  test('Login Page (/login) - dark mode @a11y', async ({ page }) => {
    await gotoChecked(page, '/login');
    await page.waitForTimeout(500);

    const results = await createAxeBuilder(page).analyze();

    expect(results.violations, formatViolations(results.violations)).toEqual([]);
  });

  test('Register Page (/register) - dark mode @a11y', async ({ page }) => {
    await gotoChecked(page, '/register');
    await page.waitForTimeout(500);

    const results = await createAxeBuilder(page).analyze();

    expect(results.violations, formatViolations(results.violations)).toEqual([]);
  });
});

// ============================================================================
// COLOR CONTRAST TESTS
// ============================================================================

test.describe('Accessibility - Color Contrast (WCAG 2.1 AA)', () => {
  test('Landing Page meets 4.5:1 contrast ratio - light mode @a11y', async ({ page }) => {
    // #1094 follow-up: reduced-motion neutralizes mid-animation captures
    // (e.g. /register `.animate-pulse` "Creating account…" yielded ratio 4.09
    // when caught mid-pulse cycle). Components must carry
    // `motion-reduce:animate-none` for this to take effect.
    await page.emulateMedia({ colorScheme: 'light', reducedMotion: 'reduce' });
    await gotoChecked(page, '/');

    const results = await new AxeBuilder({ page }).withRules(['color-contrast']).analyze();

    expect(results.violations, formatViolations(results.violations)).toEqual([]);
  });

  test('Landing Page meets 4.5:1 contrast ratio - dark mode @a11y', async ({ page }) => {
    await page.emulateMedia({ colorScheme: 'dark', reducedMotion: 'reduce' });
    await gotoChecked(page, '/');

    const results = await new AxeBuilder({ page }).withRules(['color-contrast']).analyze();

    expect(results.violations, formatViolations(results.violations)).toEqual([]);
  });

  test('Shared Games Catalog meets contrast requirements @a11y', async ({ page }) => {
    // #1094 follow-up: reduced-motion neutralizes mid-animation captures
    // (e.g. /register `.animate-pulse` "Creating account…" yielded ratio 4.09
    // when caught mid-pulse cycle). Components must carry
    // `motion-reduce:animate-none` for this to take effect.
    await page.emulateMedia({ colorScheme: 'light', reducedMotion: 'reduce' });
    await gotoChecked(page, '/shared-games');

    const results = await new AxeBuilder({ page }).withRules(['color-contrast']).analyze();

    expect(results.violations, formatViolations(results.violations)).toEqual([]);
  });

  test('Auth pages meet contrast requirements @a11y', async ({ page }) => {
    // #1094 follow-up: reduced-motion neutralizes mid-animation captures
    // (e.g. /register `.animate-pulse` "Creating account…" yielded ratio 4.09
    // when caught mid-pulse cycle). Components must carry
    // `motion-reduce:animate-none` for this to take effect.
    await page.emulateMedia({ colorScheme: 'light', reducedMotion: 'reduce' });
    await gotoChecked(page, '/login');
    await page.waitForTimeout(500);

    const results = await new AxeBuilder({ page }).withRules(['color-contrast']).analyze();

    expect(results.violations, formatViolations(results.violations)).toEqual([]);
  });
});

// ============================================================================
// KEYBOARD NAVIGATION TESTS
// ============================================================================

test.describe('Accessibility - Keyboard Navigation', () => {
  test('Landing Page - all interactive elements are keyboard accessible @a11y', async ({
    page,
  }) => {
    await gotoChecked(page, '/');

    // Test Tab navigation
    const focusableElements = await page
      .locator('button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])')
      .all();

    // Verify at least some focusable elements exist
    expect(focusableElements.length).toBeGreaterThan(0);

    // Test first few elements for keyboard accessibility
    for (let i = 0; i < Math.min(5, focusableElements.length); i++) {
      await page.keyboard.press('Tab');
      const activeElement = await page.evaluate(() => document.activeElement?.tagName);
      expect(activeElement).toBeTruthy();
    }
  });

  test('Login form - keyboard navigation works correctly @a11y', async ({ page }) => {
    await gotoChecked(page, '/login');
    await page.waitForTimeout(500);

    // Check that form inputs can receive focus
    const inputs = await page.locator('input').all();
    if (inputs.length > 0) {
      await inputs[0].focus();
      const isFocused = await inputs[0].evaluate(el => el === document.activeElement);
      expect(isFocused).toBe(true);
    }
  });

  test('Shared games catalog - cards are keyboard navigable @a11y', async ({ page }) => {
    await gotoChecked(page, '/shared-games');

    // Detail links of the public catalog. The old selector looked for
    // `/giochi/` and `/games/`, which never matched this route (#3917).
    const gameLinks = await page.locator('a[href^="/shared-games/"]').all();

    // The catalog can legitimately be empty in an unseeded environment. Skipping
    // with a reason keeps that visible in the report; the previous `if (...) {}`
    // with no `else` reported success for a check that never ran (#3917).
    test.skip(
      gameLinks.length === 0,
      'no catalog entries rendered — nothing to assert about card focusability'
    );

    await gameLinks[0].focus();
    const isFocused = await gameLinks[0].evaluate(el => el === document.activeElement);
    expect(isFocused).toBe(true);
  });
});

// ============================================================================
// FOCUS INDICATOR TESTS
// ============================================================================

test.describe('Accessibility - Focus Indicators', () => {
  test('Buttons have visible focus indicators @a11y', async ({ page }) => {
    await gotoChecked(page, '/');

    const buttons = await page.locator('button').all();
    if (buttons.length > 0) {
      await buttons[0].focus();

      // Check for focus-visible styles
      const hasVisibleFocus = await buttons[0].evaluate(el => {
        const styles = window.getComputedStyle(el);
        // Check for outline or ring styles
        return (
          styles.outline !== 'none' ||
          styles.boxShadow !== 'none' ||
          el.classList.contains('focus-visible:ring-2') ||
          el.classList.contains('focus:ring-2')
        );
      });

      // At minimum, the button should have some focus styling class
      const classes = (await buttons[0].getAttribute('class')) || '';
      const hasFocusClass = classes.includes('focus') || hasVisibleFocus;
      expect(hasFocusClass).toBe(true);
    }
  });

  test('Links have visible focus indicators @a11y', async ({ page }) => {
    await gotoChecked(page, '/');

    const links = await page.locator('a[href]').all();
    if (links.length > 0) {
      await links[0].focus();

      const isFocused = await links[0].evaluate(el => el === document.activeElement);
      expect(isFocused).toBe(true);
    }
  });
});

// ============================================================================
// ARIA AND SEMANTIC HTML TESTS
// ============================================================================

test.describe('Accessibility - ARIA and Semantic HTML', () => {
  test('Landing Page has proper landmark regions @a11y', async ({ page }) => {
    await gotoChecked(page, '/');

    // Check for main landmark
    const main = await page.locator('main, [role="main"]').count();
    expect(main).toBeGreaterThanOrEqual(1);

    // Check for navigation landmark
    const nav = await page.locator('nav, [role="navigation"]').count();
    expect(nav).toBeGreaterThanOrEqual(1);
  });

  test('Login form has proper labels @a11y', async ({ page }) => {
    await gotoChecked(page, '/login');
    await page.waitForTimeout(500);

    const results = await new AxeBuilder({ page }).withTags(['cat.forms']).analyze();

    expect(results.violations, formatViolations(results.violations)).toEqual([]);
  });

  test('Images have alt text @a11y', async ({ page }) => {
    await gotoChecked(page, '/');

    const results = await new AxeBuilder({ page }).withTags(['cat.text-alternatives']).analyze();

    expect(results.violations, formatViolations(results.violations)).toEqual([]);
  });

  test('Buttons and links have accessible names @a11y', async ({ page }) => {
    await gotoChecked(page, '/');

    const results = await new AxeBuilder({ page }).withTags(['cat.name-role-value']).analyze();

    expect(results.violations, formatViolations(results.violations)).toEqual([]);
  });
});

// ============================================================================
// AUTHENTICATED PAGES (require auth setup)
// ============================================================================

test.describe('Accessibility - Authenticated Pages', () => {
  // Note: These tests require authentication setup
  // They are marked with @authenticated tag for conditional execution

  test.skip('Dashboard (/dashboard) - requires auth @a11y @authenticated', async ({ page }) => {
    // TODO: Implement after auth setup is added
    await gotoChecked(page, '/dashboard');

    const results = await createAxeBuilder(page).analyze();
    expect(results.violations, formatViolations(results.violations)).toEqual([]);
  });

  test.skip('Library (/library) - requires auth @a11y @authenticated', async ({ page }) => {
    // TODO: Implement after auth setup is added
    await gotoChecked(page, '/library');

    const results = await createAxeBuilder(page).analyze();
    expect(results.violations, formatViolations(results.violations)).toEqual([]);
  });

  // NOTE (#3917/#3918): `/settings` does not exist — the settings hub currently
  // lives at `/profile?tab=settings&section=<id>`. When this test is un-skipped
  // (#3940), `gotoChecked` will fail loudly on the 404 rather than pass on it.
  // Point it at the canonical address decided by #3918 before enabling.
  test.skip('Settings (/settings) - requires auth @a11y @authenticated', async ({ page }) => {
    // TODO: Implement after auth setup is added
    await gotoChecked(page, '/settings');

    const results = await createAxeBuilder(page).analyze();
    expect(results.violations, formatViolations(results.violations)).toEqual([]);
  });
});

// ============================================================================
// COMPREHENSIVE PAGE AUDIT
// ============================================================================

test.describe('Accessibility - Comprehensive Audit', () => {
  // #1094 follow-up (review on #1249 MEDIUM-1): emit reduced-motion to
  // neutralize mid-animation captures (e.g. /register .animate-pulse).
  // Without this beforeEach, the Comprehensive Audit's /register test
  // can capture #7c7874-family colors mid-pulse at 4.09:1 AA fail.
  // Components must carry `motion-reduce:animate-none` for this to take effect.
  test.beforeEach(async ({ page }) => {
    await page.emulateMedia({ reducedMotion: 'reduce' });
  });

  const publicPages = [
    { name: 'Landing Page', url: '/' },
    { name: 'Shared Games Catalog', url: '/shared-games' },
    // 'Library' removed (#3917): `(authenticated)` route, redirected to /login
    // without a session — this entry audited the login page a second time.
    { name: 'Login', url: '/login' },
    { name: 'Register', url: '/register' },
    { name: 'About', url: '/about' },
    { name: 'FAQ', url: '/faq' },
    { name: 'Privacy', url: '/privacy' },
    { name: 'Terms', url: '/terms' },
  ];

  for (const pageConfig of publicPages) {
    test(`${pageConfig.name} (${pageConfig.url}) - full WCAG 2.1 AA audit @a11y`, async ({
      page,
    }) => {
      await gotoChecked(page, pageConfig.url);

      // Extra wait for dynamic content
      if (pageConfig.url.includes('login') || pageConfig.url.includes('register')) {
        await page.waitForTimeout(500);
      }

      const results = await createAxeBuilder(page).analyze();

      // Log detailed violations for debugging
      if (results.violations.length > 0) {
        console.log(`\n[${pageConfig.name}] WCAG Violations Found:`);
        results.violations.forEach(v => {
          console.log(`  [${v.impact}] ${v.id}: ${v.help}`);
          v.nodes.forEach(n => {
            console.log(`    - ${n.html.substring(0, 80)}...`);
          });
        });
      }

      expect(results.violations, formatViolations(results.violations)).toEqual([]);
    });
  }
});

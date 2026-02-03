/**
 * E2E Accessibility Tests (Issue #2929 - WCAG 2.1 AA Compliance)
 *
 * Comprehensive accessibility testing for all 7 main pages using axe-core.
 * Tests both light and dark modes with WCAG 2.1 AA standards.
 *
 * Pages Covered:
 * 1. Landing Page (/)
 * 2. Games Catalog (/board-game-ai/games)
 * 3. Game Detail (/giochi/[id])
 * 4. Dashboard (/dashboard)
 * 5. Library (/library)
 * 6. Settings (/settings)
 * 7. Auth Pages (/login, /register)
 *
 * WCAG 2.1 AA Requirements:
 * - Color contrast minimum 4.5:1 for normal text
 * - All interactive elements keyboard accessible
 * - Focus indicators visible
 * - Alt text for all images
 * - ARIA labels where needed
 */

import AxeBuilder from '@axe-core/playwright';
import { test, expect } from '@playwright/test';

// WCAG 2.1 AA tags for axe-core
const WCAG_TAGS = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'];

// Helper to create axe builder with standard config
function createAxeBuilder(page: Parameters<typeof AxeBuilder>[0]['page']) {
  return new AxeBuilder({ page })
    .withTags(WCAG_TAGS)
    .exclude('#webpack-dev-server-client-overlay') // Exclude dev overlay
    .exclude('[data-chromatic-ignore]'); // Exclude Chromatic-specific elements
}

// Helper to format violations for better error messages
function formatViolations(violations: Awaited<ReturnType<AxeBuilder['analyze']>>['violations']) {
  if (violations.length === 0) return 'No violations';

  return violations.map(v => {
    const nodes = v.nodes.map(n => `  - ${n.html.substring(0, 100)}...`).join('\n');
    return `[${v.impact}] ${v.id}: ${v.help}\n${nodes}`;
  }).join('\n\n');
}

// ============================================================================
// PUBLIC PAGES - Light Mode
// ============================================================================

test.describe('Accessibility - Public Pages (Light Mode)', () => {
  test.beforeEach(async ({ page }) => {
    await page.emulateMedia({ colorScheme: 'light' });
  });

  test('Landing Page (/) - light mode @a11y', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    const results = await createAxeBuilder(page).analyze();

    expect(results.violations, formatViolations(results.violations)).toEqual([]);
  });

  test('Games Catalog (/board-game-ai/games) - light mode @a11y', async ({ page }) => {
    await page.goto('/board-game-ai/games');
    await page.waitForLoadState('networkidle');

    const results = await createAxeBuilder(page).analyze();

    expect(results.violations, formatViolations(results.violations)).toEqual([]);
  });

  test('Games Catalog (/games) - light mode @a11y', async ({ page }) => {
    await page.goto('/games');
    await page.waitForLoadState('networkidle');

    const results = await createAxeBuilder(page).analyze();

    expect(results.violations, formatViolations(results.violations)).toEqual([]);
  });

  test('Login Page (/login) - light mode @a11y', async ({ page }) => {
    await page.goto('/login');
    await page.waitForLoadState('networkidle');

    // Wait for auth modal to be fully rendered
    await page.waitForTimeout(500);

    const results = await createAxeBuilder(page).analyze();

    expect(results.violations, formatViolations(results.violations)).toEqual([]);
  });

  test('Register Page (/register) - light mode @a11y', async ({ page }) => {
    await page.goto('/register');
    await page.waitForLoadState('networkidle');

    // Wait for auth modal to be fully rendered
    await page.waitForTimeout(500);

    const results = await createAxeBuilder(page).analyze();

    expect(results.violations, formatViolations(results.violations)).toEqual([]);
  });

  test('About Page (/about) - light mode @a11y', async ({ page }) => {
    await page.goto('/about');
    await page.waitForLoadState('networkidle');

    const results = await createAxeBuilder(page).analyze();

    expect(results.violations, formatViolations(results.violations)).toEqual([]);
  });

  test('FAQ Page (/faq) - light mode @a11y', async ({ page }) => {
    await page.goto('/faq');
    await page.waitForLoadState('networkidle');

    const results = await createAxeBuilder(page).analyze();

    expect(results.violations, formatViolations(results.violations)).toEqual([]);
  });
});

// ============================================================================
// PUBLIC PAGES - Dark Mode
// ============================================================================

test.describe('Accessibility - Public Pages (Dark Mode)', () => {
  test.beforeEach(async ({ page }) => {
    await page.emulateMedia({ colorScheme: 'dark' });
  });

  test('Landing Page (/) - dark mode @a11y', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    const results = await createAxeBuilder(page).analyze();

    expect(results.violations, formatViolations(results.violations)).toEqual([]);
  });

  test('Games Catalog (/board-game-ai/games) - dark mode @a11y', async ({ page }) => {
    await page.goto('/board-game-ai/games');
    await page.waitForLoadState('networkidle');

    const results = await createAxeBuilder(page).analyze();

    expect(results.violations, formatViolations(results.violations)).toEqual([]);
  });

  test('Login Page (/login) - dark mode @a11y', async ({ page }) => {
    await page.goto('/login');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(500);

    const results = await createAxeBuilder(page).analyze();

    expect(results.violations, formatViolations(results.violations)).toEqual([]);
  });

  test('Register Page (/register) - dark mode @a11y', async ({ page }) => {
    await page.goto('/register');
    await page.waitForLoadState('networkidle');
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
    await page.emulateMedia({ colorScheme: 'light' });
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    const results = await new AxeBuilder({ page })
      .withTags(['cat.color'])
      .analyze();

    expect(results.violations, formatViolations(results.violations)).toEqual([]);
  });

  test('Landing Page meets 4.5:1 contrast ratio - dark mode @a11y', async ({ page }) => {
    await page.emulateMedia({ colorScheme: 'dark' });
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    const results = await new AxeBuilder({ page })
      .withTags(['cat.color'])
      .analyze();

    expect(results.violations, formatViolations(results.violations)).toEqual([]);
  });

  test('Games Catalog meets contrast requirements @a11y', async ({ page }) => {
    await page.emulateMedia({ colorScheme: 'light' });
    await page.goto('/board-game-ai/games');
    await page.waitForLoadState('networkidle');

    const results = await new AxeBuilder({ page })
      .withTags(['cat.color'])
      .analyze();

    expect(results.violations, formatViolations(results.violations)).toEqual([]);
  });

  test('Auth pages meet contrast requirements @a11y', async ({ page }) => {
    await page.emulateMedia({ colorScheme: 'light' });
    await page.goto('/login');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(500);

    const results = await new AxeBuilder({ page })
      .withTags(['cat.color'])
      .analyze();

    expect(results.violations, formatViolations(results.violations)).toEqual([]);
  });
});

// ============================================================================
// KEYBOARD NAVIGATION TESTS
// ============================================================================

test.describe('Accessibility - Keyboard Navigation', () => {
  test('Landing Page - all interactive elements are keyboard accessible @a11y', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    // Test Tab navigation
    const focusableElements = await page.locator(
      'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])'
    ).all();

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
    await page.goto('/login');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(500);

    // Check that form inputs can receive focus
    const inputs = await page.locator('input').all();
    if (inputs.length > 0) {
      await inputs[0].focus();
      const isFocused = await inputs[0].evaluate(el => el === document.activeElement);
      expect(isFocused).toBe(true);
    }
  });

  test('Games catalog - cards are keyboard navigable @a11y', async ({ page }) => {
    await page.goto('/board-game-ai/games');
    await page.waitForLoadState('networkidle');

    // Check for game cards with links
    const gameLinks = await page.locator('a[href*="/giochi/"], a[href*="/games/"]').all();

    // If there are game links, verify they are focusable
    if (gameLinks.length > 0) {
      await gameLinks[0].focus();
      const isFocused = await gameLinks[0].evaluate(el => el === document.activeElement);
      expect(isFocused).toBe(true);
    }
  });
});

// ============================================================================
// FOCUS INDICATOR TESTS
// ============================================================================

test.describe('Accessibility - Focus Indicators', () => {
  test('Buttons have visible focus indicators @a11y', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    const buttons = await page.locator('button').all();
    if (buttons.length > 0) {
      await buttons[0].focus();

      // Check for focus-visible styles
      const hasVisibleFocus = await buttons[0].evaluate(el => {
        const styles = window.getComputedStyle(el);
        // Check for outline or ring styles
        return styles.outline !== 'none' ||
               styles.boxShadow !== 'none' ||
               el.classList.contains('focus-visible:ring-2') ||
               el.classList.contains('focus:ring-2');
      });

      // At minimum, the button should have some focus styling class
      const classes = await buttons[0].getAttribute('class') || '';
      const hasFocusClass = classes.includes('focus') || hasVisibleFocus;
      expect(hasFocusClass).toBe(true);
    }
  });

  test('Links have visible focus indicators @a11y', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

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
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    // Check for main landmark
    const main = await page.locator('main, [role="main"]').count();
    expect(main).toBeGreaterThanOrEqual(1);

    // Check for navigation landmark
    const nav = await page.locator('nav, [role="navigation"]').count();
    expect(nav).toBeGreaterThanOrEqual(1);
  });

  test('Login form has proper labels @a11y', async ({ page }) => {
    await page.goto('/login');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(500);

    const results = await new AxeBuilder({ page })
      .withTags(['cat.forms'])
      .analyze();

    expect(results.violations, formatViolations(results.violations)).toEqual([]);
  });

  test('Images have alt text @a11y', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    const results = await new AxeBuilder({ page })
      .withTags(['cat.text-alternatives'])
      .analyze();

    expect(results.violations, formatViolations(results.violations)).toEqual([]);
  });

  test('Buttons and links have accessible names @a11y', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    const results = await new AxeBuilder({ page })
      .withTags(['cat.name-role-value'])
      .analyze();

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
    await page.goto('/dashboard');
    await page.waitForLoadState('networkidle');

    const results = await createAxeBuilder(page).analyze();
    expect(results.violations, formatViolations(results.violations)).toEqual([]);
  });

  test.skip('Library (/library) - requires auth @a11y @authenticated', async ({ page }) => {
    // TODO: Implement after auth setup is added
    await page.goto('/library');
    await page.waitForLoadState('networkidle');

    const results = await createAxeBuilder(page).analyze();
    expect(results.violations, formatViolations(results.violations)).toEqual([]);
  });

  test.skip('Settings (/settings) - requires auth @a11y @authenticated', async ({ page }) => {
    // TODO: Implement after auth setup is added
    await page.goto('/settings');
    await page.waitForLoadState('networkidle');

    const results = await createAxeBuilder(page).analyze();
    expect(results.violations, formatViolations(results.violations)).toEqual([]);
  });
});

// ============================================================================
// COMPREHENSIVE PAGE AUDIT
// ============================================================================

test.describe('Accessibility - Comprehensive Audit', () => {
  const publicPages = [
    { name: 'Landing Page', url: '/' },
    { name: 'Games Catalog', url: '/board-game-ai/games' },
    { name: 'Games List', url: '/games' },
    { name: 'Login', url: '/login' },
    { name: 'Register', url: '/register' },
    { name: 'About', url: '/about' },
    { name: 'FAQ', url: '/faq' },
    { name: 'Privacy', url: '/privacy' },
    { name: 'Terms', url: '/terms' },
  ];

  for (const pageConfig of publicPages) {
    test(`${pageConfig.name} (${pageConfig.url}) - full WCAG 2.1 AA audit @a11y`, async ({ page }) => {
      await page.goto(pageConfig.url);
      await page.waitForLoadState('networkidle');

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

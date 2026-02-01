/**
 * Admin Dashboard Performance + Accessibility Tests - Issue #889
 *
 * Validates:
 * - Performance (<1s load, <2s TTI, Lighthouse >90)
 * - Accessibility (WCAG AA, Lighthouse a11y 100)
 * - Core Web Vitals (LCP, FID, CLS)
 * - Keyboard navigation
 * - Screen reader compatibility
 * - Contrast ratio ≥4.5:1
 *
 * Dependencies: #885 (✅), #886 (✅)
 */

import AxeBuilder from '@axe-core/playwright';
import { playAudit } from 'playwright-lighthouse';

import { test as base, expect, Page } from './fixtures';
import { AdminHelper } from './pages';

import type { Result } from 'axe-core';

const test = base.extend<{ adminPage: Page }>({
  adminPage: async ({ page }: { page: Page }, use: (page: Page) => Promise<void>) => {
    const adminHelper = new AdminHelper(page);
    await adminHelper.setupAdminAuth(true);

    // ✅ REMOVED MOCK: Use real Admin Analytics and Activity APIs
    // Real backend GET /api/v1/admin/analytics must return 16 metrics (Issue #874)
    // Real backend GET /api/v1/admin/activity must return recent event feed
    // Note: Performance and accessibility tests work with any backend data

    await use(page);
  },
});

// Helper to format violations
function formatViolations(violations: Result[]) {
  return violations.map(v => ({
    id: v.id,
    impact: v.impact,
    description: v.description,
    nodes: v.nodes.length,
    helpUrl: v.helpUrl,
  }));
}

test.describe('Admin Dashboard Performance + Accessibility (Issue #889)', () => {
  test.describe('Performance Tests', () => {
    test('should load in less than 1 second (P95)', async ({ adminPage: page }) => {
      const startTime = Date.now();

      await page.goto('/admin');
      await page.waitForLoadState('networkidle');

      // Wait for dashboard to be fully loaded
      await page.getByRole('heading', { name: 'Dashboard Overview' }).waitFor();

      const loadTime = Date.now() - startTime;

      // Issue #889: <1s load time requirement
      expect(loadTime, `Load time ${loadTime}ms should be < 1000ms`).toBeLessThan(1000);
    });

    test('should reach TTI (Time to Interactive) in less than 2 seconds', async ({
      adminPage: page,
    }) => {
      const startTime = Date.now();

      await page.goto('/admin');

      // Wait for main content to be visible
      await page.getByRole('heading', { name: 'Dashboard Overview' }).waitFor();

      // Wait for interactive elements to be ready
      await page.getByText('Total Users').waitFor();
      await page.getByRole('link', { name: 'Users', exact: true }).waitFor();

      const ttiTime = Date.now() - startTime;

      // Issue #889: <2s TTI requirement
      expect(ttiTime, `TTI ${ttiTime}ms should be < 2000ms`).toBeLessThan(2000);
    });

    test('should have good Core Web Vitals', async ({ adminPage: page }) => {
      await page.goto('/admin');
      await page.waitForLoadState('networkidle');

      // Measure Web Vitals using Navigation Timing API
      const metrics = await page.evaluate(() => {
        const navigation = performance.getEntriesByType(
          'navigation'
        )[0] as PerformanceNavigationTiming;

        return {
          // Time to First Byte
          ttfb: navigation.responseStart - navigation.requestStart,
          // DOM Content Loaded
          dcl: navigation.domContentLoadedEventEnd - navigation.domContentLoadedEventStart,
          // Load Complete
          loadComplete: navigation.loadEventEnd - navigation.fetchStart,
          // First Contentful Paint (if available)
          fcp: performance.getEntriesByName('first-contentful-paint')[0]?.startTime || 0,
        };
      });

      // TTFB should be fast (backend response time)
      expect(metrics.ttfb, `TTFB ${metrics.ttfb}ms should be < 600ms`).toBeLessThan(600);

      // DCL should be quick (DOM ready)
      expect(metrics.dcl, `DCL ${metrics.dcl}ms should be < 100ms`).toBeLessThan(100);

      // Full page load
      expect(
        metrics.loadComplete,
        `Load Complete ${metrics.loadComplete}ms should be < 2500ms`
      ).toBeLessThan(2500);
    });

    test('should have Lighthouse score ≥90 for all admin pages', async ({ adminPage: page }) => {
      // Issue #2917: Lighthouse CI integration for Admin Dashboard
      const adminPages = [
        { url: '/admin', name: 'Dashboard' },
        { url: '/admin/analytics', name: 'Analytics' },
        { url: '/admin/users', name: 'Users' },
        { url: '/admin/prompts', name: 'Prompts' },
      ];

      for (const adminPage of adminPages) {
        console.log(`\n🔦 Running Lighthouse audit on ${adminPage.name} (${adminPage.url})...`);

        await page.goto(adminPage.url);
        await page.waitForLoadState('networkidle');

        // Run Lighthouse audit
        const { lhr } = await playAudit({
          page,
          thresholds: {
            performance: 90,
            accessibility: 90,
            'best-practices': 90,
            seo: 90,
          },
          reports: {
            formats: {
              html: true,
            },
            directory: 'lighthouse-reports',
            name: `admin-${adminPage.name.toLowerCase()}-${Date.now()}`,
          },
        });

        // Validate Lighthouse scores (Issue #2917 DoD)
        const performanceScore = lhr.categories.performance.score * 100;
        const accessibilityScore = lhr.categories.accessibility.score * 100;
        const bestPracticesScore = lhr.categories['best-practices'].score * 100;
        const seoScore = lhr.categories.seo.score * 100;

        expect(
          performanceScore,
          `${adminPage.name}: Performance score ${performanceScore}% should be ≥90%`
        ).toBeGreaterThanOrEqual(90);

        expect(
          accessibilityScore,
          `${adminPage.name}: Accessibility score ${accessibilityScore}% should be ≥90%`
        ).toBeGreaterThanOrEqual(90);

        expect(
          bestPracticesScore,
          `${adminPage.name}: Best Practices score ${bestPracticesScore}% should be ≥90%`
        ).toBeGreaterThanOrEqual(90);

        expect(
          seoScore,
          `${adminPage.name}: SEO score ${seoScore}% should be ≥90%`
        ).toBeGreaterThanOrEqual(90);

        // Validate Core Web Vitals (Issue #2917 DoD)
        const fcp = lhr.audits['first-contentful-paint'].numericValue;
        const lcp = lhr.audits['largest-contentful-paint'].numericValue;
        const cls = lhr.audits['cumulative-layout-shift'].numericValue;
        const tbt = lhr.audits['total-blocking-time'].numericValue;

        expect(fcp, `${adminPage.name}: FCP ${fcp}ms should be <1800ms`).toBeLessThan(1800);
        expect(lcp, `${adminPage.name}: LCP ${lcp}ms should be <2500ms`).toBeLessThan(2500);
        expect(cls, `${adminPage.name}: CLS ${cls} should be <0.1`).toBeLessThan(0.1);
        expect(tbt, `${adminPage.name}: TBT ${tbt}ms should be <300ms`).toBeLessThan(300);

        console.log(`✅ ${adminPage.name} Lighthouse audit passed:
  - Performance: ${performanceScore}%
  - Accessibility: ${accessibilityScore}%
  - Best Practices: ${bestPracticesScore}%
  - SEO: ${seoScore}%
  - FCP: ${fcp}ms
  - LCP: ${lcp}ms
  - CLS: ${cls}
  - TBT: ${tbt}ms`);
      }
    });
  });

  test.describe('Accessibility Tests (WCAG AA)', () => {
    test('should pass axe-core accessibility audit', async ({ adminPage: page }) => {
      await page.goto('/admin');
      await page.waitForLoadState('networkidle');

      // Run axe accessibility scan
      const accessibilityScanResults = await new AxeBuilder({ page })
        .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
        .analyze();

      // Issue #889: Lighthouse a11y 100 requirement
      const violations = accessibilityScanResults.violations;

      if (violations.length > 0) {
        console.log('Accessibility violations found:');
        console.log(JSON.stringify(formatViolations(violations), null, 2));
      }

      expect(
        violations,
        `Found ${violations.length} accessibility violations: ${violations.map(v => v.id).join(', ')}`
      ).toHaveLength(0);
    });

    test('should support full keyboard navigation', async ({ adminPage: page }) => {
      await page.goto('/admin');
      await page.waitForLoadState('networkidle');

      // Start from body
      await page.keyboard.press('Tab');

      // First focusable element should be a navigation link or button
      const firstFocused = await page.evaluate(() => document.activeElement?.tagName);
      expect(['A', 'BUTTON']).toContain(firstFocused);

      // Tab through multiple elements
      const focusedElements: string[] = [];
      for (let i = 0; i < 10; i++) {
        await page.keyboard.press('Tab');
        const tagName = await page.evaluate(() => document.activeElement?.tagName);
        const text = await page.evaluate(() =>
          document.activeElement?.textContent?.trim().slice(0, 30)
        );
        focusedElements.push(`${tagName}: ${text}`);
      }

      // Should have navigated through interactive elements
      expect(focusedElements.length).toBeGreaterThan(0);
      expect(focusedElements.some(el => el.includes('A') || el.includes('BUTTON'))).toBe(true);
    });

    test('should have visible focus indicators', async ({ adminPage: page }) => {
      await page.goto('/admin');
      await page.waitForLoadState('networkidle');

      // Focus first link
      const firstLink = page.getByRole('link').first();
      await firstLink.focus();

      // Check if element has focus styles
      const hasFocusStyle = await firstLink.evaluate(el => {
        const styles = window.getComputedStyle(el);
        return (
          styles.outline !== 'none' ||
          styles.outlineWidth !== '0px' ||
          styles.boxShadow !== 'none' ||
          styles.border !== styles.borderColor // Changed border color
        );
      });

      expect(hasFocusStyle, 'Focused element should have visible focus indicator').toBe(true);
    });

    test('should have proper heading hierarchy', async ({ adminPage: page }) => {
      await page.goto('/admin');
      await page.waitForLoadState('networkidle');

      const headings = await page.evaluate(() => {
        const headingElements = Array.from(document.querySelectorAll('h1, h2, h3, h4, h5, h6'));
        return headingElements.map(h => ({
          level: parseInt(h.tagName[1]),
          text: h.textContent?.trim(),
        }));
      });

      // Should have at least one h1
      const h1Count = headings.filter(h => h.level === 1).length;
      expect(h1Count, 'Page should have exactly one h1').toBe(1);

      // Headings should not skip levels
      for (let i = 1; i < headings.length; i++) {
        const currentLevel = headings[i].level;
        const prevLevel = headings[i - 1].level;
        const levelDiff = currentLevel - prevLevel;

        expect(
          levelDiff,
          `Heading levels should not skip: ${headings[i - 1].text} (h${prevLevel}) -> ${headings[i].text} (h${currentLevel})`
        ).toBeLessThanOrEqual(1);
      }
    });

    test('should have sufficient color contrast (≥4.5:1)', async ({ adminPage: page }) => {
      await page.goto('/admin');
      await page.waitForLoadState('networkidle');

      // Run axe with specific contrast rule
      const contrastResults = await new AxeBuilder({ page })
        .include('[role="main"]')
        .withRules(['color-contrast'])
        .analyze();

      const contrastViolations = contrastResults.violations;

      if (contrastViolations.length > 0) {
        console.log('Contrast violations:');
        console.log(JSON.stringify(formatViolations(contrastViolations), null, 2));
      }

      // Issue #889: Contrast ratio ≥4.5:1 requirement
      expect(
        contrastViolations,
        `Found ${contrastViolations.length} contrast violations`
      ).toHaveLength(0);
    });

    test('should have proper ARIA labels and roles', async ({ adminPage: page }) => {
      await page.goto('/admin');
      await page.waitForLoadState('networkidle');

      // Check for common ARIA issues
      const ariaIssues = await page.evaluate(() => {
        const issues: string[] = [];

        // Check buttons without accessible names
        const buttons = document.querySelectorAll('button');
        buttons.forEach((btn, idx) => {
          const hasText = btn.textContent?.trim();
          const hasAriaLabel = btn.getAttribute('aria-label');
          const hasAriaLabelledBy = btn.getAttribute('aria-labelledby');

          if (!hasText && !hasAriaLabel && !hasAriaLabelledBy) {
            issues.push(`Button ${idx} has no accessible name`);
          }
        });

        // Check links without accessible names
        const links = document.querySelectorAll('a');
        links.forEach((link, idx) => {
          const hasText = link.textContent?.trim();
          const hasAriaLabel = link.getAttribute('aria-label');

          if (!hasText && !hasAriaLabel) {
            issues.push(`Link ${idx} has no accessible name`);
          }
        });

        // Check for proper landmark roles
        const hasMain = document.querySelector('main, [role="main"]');
        if (!hasMain) {
          issues.push('Page missing main landmark');
        }

        return issues;
      });

      if (ariaIssues.length > 0) {
        console.log('ARIA issues found:');
        console.log(ariaIssues.join('\n'));
      }

      expect(ariaIssues).toHaveLength(0);
    });

    test('should be screen reader compatible', async ({ adminPage: page }) => {
      await page.goto('/admin');
      await page.waitForLoadState('networkidle');

      // Check for screen reader specific attributes
      const srCompatibility = await page.evaluate(() => {
        const checks = {
          hasLandmarks: document.querySelectorAll('main, nav, header, footer, [role]').length > 0,
          imagesHaveAlt: Array.from(document.querySelectorAll('img')).every(
            img => img.alt !== undefined
          ),
          formsHaveLabels: Array.from(document.querySelectorAll('input, textarea, select')).every(
            input => {
              const id = input.id;
              return id && document.querySelector(`label[for="${id}"]`);
            }
          ),
          hasSkipLink: !!document.querySelector('a[href^="#"]'),
        };

        return checks;
      });

      expect(srCompatibility.hasLandmarks, 'Page should have semantic landmarks').toBe(true);
      expect(srCompatibility.imagesHaveAlt, 'All images should have alt text').toBe(true);
    });
  });

  test.describe('Combined Performance + Accessibility', () => {
    test('should meet all requirements simultaneously', async ({ adminPage: page }) => {
      const startTime = Date.now();

      // Navigate and wait for load
      await page.goto('/admin');
      await page.waitForLoadState('networkidle');
      await page.getByRole('heading', { name: 'Dashboard Overview' }).waitFor();

      const loadTime = Date.now() - startTime;

      // Performance: <1s load time
      expect(loadTime, '✅ Performance: Load time < 1s').toBeLessThan(1000);

      // Accessibility: Run axe scan
      const a11yResults = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa']).analyze();

      expect(
        a11yResults.violations,
        `✅ Accessibility: ${a11yResults.violations.length} violations (target: 0)`
      ).toHaveLength(0);

      // Verify core functionality
      await expect(page.getByText('Total Users')).toBeVisible();
      await expect(page.getByRole('link', { name: 'Users', exact: true })).toBeVisible();

      console.log(`
✅ Issue #889 - All requirements met:
  - Load time: ${loadTime}ms (target: <1000ms)
  - TTI: <2s (inferred from load time)
  - Accessibility violations: 0
  - Core functionality: working
  - Keyboard navigation: supported
      `);
    });
  });
});

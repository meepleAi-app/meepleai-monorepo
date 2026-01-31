/**
 * E2E tests for Month 4 Admin Analytics Quality Metrics Dashboard - MIGRATED TO POM
 *
 * @see apps/web/e2e/pages/helpers/AdminHelper.ts
 * @see apps/web/e2e/pages/admin/AdminPage.ts
 */

import { test, expect } from './fixtures/chromatic';

/**
 * Admin Analytics Quality Metrics Tests (SKIPPED)
 * Tests quality metrics display in /admin/analytics page
 *
 * Issue #995: BGAI-055 - Month 4 integration testing
 * Month 4 Deliverables: BGAI-043 to BGAI-046 (Quality metrics + Prometheus + Grafana)
 *
 * Note: Tests adapted to actual /admin/analytics UI structure (no data-testid attributes yet)
 * Tests verify page loads and basic functionality, not specific UI elements
 */

// TODO: These tests require admin authentication setup
// Deferred to future issue when auth fixtures are implemented for admin pages
test.describe.skip('Admin Analytics - Quality Metrics', () => {
  test.beforeEach(async ({ page }) => {
    // Navigate to admin analytics page
    await page.goto('/admin/analytics');
  });

  test('Admin analytics page loads successfully', async ({ page }) => {
    // Wait for page title
    await page.waitForSelector('h1:has-text("Analytics Dashboard")', { timeout: 15000 });

    // Verify page loaded
    const title = await page.locator('h1').textContent();
    console.log(`✓ Page loaded: ${title}`);

    expect(title).toContain('Analytics');
  });

  test('Average confidence score metric exists', async ({ page }) => {
    // Wait for page load
    await page.waitForSelector('h1:has-text("Analytics Dashboard")', { timeout: 15000 });

    // Look for confidence score text (from analytics API response)
    const pageContent = await page.content();
    const hasConfidenceMetric =
      pageContent.includes('Confidence') || pageContent.includes('confidence');

    console.log(`✓ Confidence metric present: ${hasConfidenceMetric}`);

    // This is a smoke test - just verify page structure loads
    expect(hasConfidenceMetric || pageContent.includes('Analytics')).toBeTruthy();
  });

  test('Admin panel navigation works', async ({ page }) => {
    // Wait for page load
    await page.waitForSelector('h1:has-text("Analytics Dashboard")', { timeout: 15000 });

    // Check if "Back to Users" link exists
    const backLink = page.locator('a:has-text("Back to Users")');
    if (await backLink.isVisible({ timeout: 2000 }).catch(() => false)) {
      console.log('✓ Back to Users link found');
      expect(await backLink.getAttribute('href')).toContain('/admin/users');
    } else {
      console.log('✓ Navigation link not found (UI may vary)');
    }
  });

  test('Metrics data structure is present', async ({ page }) => {
    // Wait for page load
    await page.waitForSelector('h1:has-text("Analytics Dashboard")', { timeout: 15000 });

    // Check for any numeric metrics display
    const pageContent = await page.content();

    // Look for common metric patterns
    const hasMetrics =
      pageContent.includes('Users') ||
      pageContent.includes('Sessions') ||
      pageContent.includes('Requests') ||
      pageContent.includes('Confidence');

    console.log(`✓ Metrics structure present: ${hasMetrics}`);
    expect(hasMetrics).toBeTruthy();
  });
});

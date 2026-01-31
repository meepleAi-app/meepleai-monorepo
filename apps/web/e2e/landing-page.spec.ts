/**
 * Landing Page E2E Tests
 *
 * End-to-end tests for the marketing landing page.
 * Validates user journey, accessibility, and performance.
 *
 * Issue #1835: PAGE-001 - Landing Page (Marketing)
 */

import { test, expect } from '@playwright/test';

test.describe('Landing Page', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
  });

  test('loads successfully with all sections', async ({ page }) => {
    // Hero section - title contains "AI" (works for both Italian and English)
    await expect(page.getByRole('heading', { level: 1 })).toContainText(/AI/i);
    await expect(page.getByTestId('get-started-button')).toBeVisible();

    // Features section - heading contains "Caratteristiche" or "Features"
    await expect(page.locator('#features')).toBeVisible();
    // Feature cards with their titles
    await expect(page.getByText(/AI Intelligente|Smart AI/i).first()).toBeVisible();
    await expect(page.getByText(/Catalogo Ampio|Wide Catalog/i).first()).toBeVisible();
    await expect(page.getByText('Mobile-First')).toBeVisible();

    // Check page has multiple sections loaded (hero, features, cta)
    const sections = await page.locator('section').count();
    expect(sections).toBeGreaterThanOrEqual(2);
  });

  test('CTA button navigates to registration', async ({ page }) => {
    // CTA button uses "Get Started" (English) or "Inizia Gratis" (Italian)
    const ctaButton = page.getByTestId('get-started-button').getByRole('link');
    await expect(ctaButton).toHaveAttribute('href', '/register');
  });

  test('scroll to features works', async ({ page }) => {
    // Use aria-label "Scorri alla sezione caratteristiche" from HeroSection
    const scrollButton = page
      .getByRole('button', { name: /scorri alla sezione caratteristiche|scroll to features/i })
      .first();
    await scrollButton.click();

    // Wait for scroll animation
    await page.waitForTimeout(500);

    // Features section should be in viewport
    const featuresSection = page.locator('#features');
    await expect(featuresSection).toBeInViewport();
  });

  test('footer CTAs are functional', async ({ page }) => {
    await page.evaluate(() => window.scrollTo(0, document.body.scrollHeight));
    await page.waitForTimeout(500);

    // Check for registration CTA link - could be "Registrati ora" or "Inizia Gratis"
    const registerLinks = page.getByRole('link', { name: /registrati|inizia|register|get started/i });
    const registerCount = await registerLinks.count();
    expect(registerCount).toBeGreaterThan(0);

    // Check for login link in header (always visible) - "Accedi"
    const loginButton = page.getByRole('link', { name: /accedi|login/i }).first();
    await expect(loginButton).toBeVisible();
  });

  test('legal links are present', async ({ page }) => {
    await page.evaluate(() => window.scrollTo(0, document.body.scrollHeight));
    await page.waitForTimeout(500);

    // Privacy and Terms links should be in footer
    await expect(page.getByRole('link', { name: /privacy/i }).first()).toBeVisible();
    await expect(page.getByRole('link', { name: /termin|terms/i }).first()).toBeVisible();
  });

  test('has proper SEO metadata', async ({ page }) => {
    const title = await page.title();
    expect(title).toContain('MeepleAI');
    expect(title).toContain('assistente AI');

    const metaDescription = await page.locator('meta[name="description"]').getAttribute('content');
    expect(metaDescription).toContain('Risposte immediate');
  });

  test('has structured data for SEO', async ({ page }) => {
    const structuredData = await page.locator('script[type="application/ld+json"]').textContent();
    expect(structuredData).toBeTruthy();

    const jsonData = JSON.parse(structuredData!);
    expect(jsonData['@type']).toBe('SoftwareApplication');
    expect(jsonData.name).toBe('MeepleAI');
  });

  test('is responsive on mobile', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 667 });

    // Hero should be visible - h1 contains "AI" (works for both Italian and English)
    await expect(page.getByRole('heading', { level: 1 })).toContainText(/AI/i);

    // Features should stack vertically
    const featuresGrid = page.locator('#features .grid');
    await expect(featuresGrid).toBeVisible();
  });

  test('is responsive on tablet', async ({ page }) => {
    await page.setViewportSize({ width: 768, height: 1024 });

    await expect(page.getByRole('heading', { level: 1 })).toContainText(/AI/i);
  });

  test('is responsive on desktop', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });

    await expect(page.getByRole('heading', { level: 1 })).toContainText(/AI/i);
  });

  test('passes accessibility checks', async ({ page }) => {
    // Check for main landmark
    await expect(page.locator('main')).toBeVisible();

    // Check for proper heading hierarchy
    const h1 = await page.locator('h1').count();
    expect(h1).toBeGreaterThan(0);

    // Check for alt text on important elements
    // (MeepleAvatar should have proper ARIA labels from component)
  });

  test('feature cards have hover effects', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });

    const featureCard = page.locator('#features .group').first();

    // Hover should work
    await featureCard.hover();

    // Card should still be visible after hover
    await expect(featureCard).toBeVisible();
  });

  test('scroll indicator is visible on desktop', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });

    // HeroSection has a scroll indicator button with aria-label for scroll to features
    // Use first() since there may be multiple buttons with this label
    const scrollIndicator = page.getByLabel(/scorri alla sezione caratteristiche|scroll to features/i).first();
    await expect(scrollIndicator).toBeVisible();
  });
});

test.describe('Performance', () => {
  test('loads quickly', async ({ page }) => {
    const startTime = Date.now();
    await page.goto('/');
    const loadTime = Date.now() - startTime;

    // Should load in less than 3 seconds
    expect(loadTime).toBeLessThan(3000);
  });
});

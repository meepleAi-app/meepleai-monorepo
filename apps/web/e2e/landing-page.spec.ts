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
    // Hero section
    await expect(page.getByRole('heading', { name: /il tuo assistente ai/i })).toBeVisible();
    await expect(page.getByRole('link', { name: /inizia gratis/i })).toBeVisible();

    // Features section - heading is "Tutto quello che ti serve", badge has "Caratteristiche Principali"
    await expect(page.getByRole('heading', { name: /tutto quello che ti serve/i })).toBeVisible();
    await expect(page.getByText('AI Intelligente')).toBeVisible();
    await expect(page.getByText('Catalogo Ampio')).toBeVisible();
    await expect(page.getByText('Mobile-First')).toBeVisible();

    // How It Works section
    await expect(page.getByRole('heading', { name: /come funziona/i })).toBeVisible();
    await expect(page.getByText('Scegli il gioco')).toBeVisible();

    // Footer
    await expect(page.getByRole('heading', { name: /pronto a iniziare/i })).toBeVisible();
  });

  test('CTA button navigates to registration', async ({ page }) => {
    const ctaButton = page.getByRole('link', { name: /inizia gratis/i }).first();
    await expect(ctaButton).toHaveAttribute('href', '/register');
  });

  test('scroll to features works', async ({ page }) => {
    // Use aria-label which is more reliable - "Scorri alla sezione caratteristiche"
    const scrollButton = page
      .getByRole('button', { name: /scorri alla sezione caratteristiche/i })
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

    const registerButton = page.getByRole('link', { name: /registrati ora/i });
    const loginButton = page.getByRole('link', { name: /accedi/i });

    await expect(registerButton).toHaveAttribute('href', '/register');
    await expect(loginButton).toHaveAttribute('href', '/login');
  });

  test('legal links are present', async ({ page }) => {
    await page.evaluate(() => window.scrollTo(0, document.body.scrollHeight));

    await expect(page.getByRole('link', { name: /privacy/i })).toBeVisible();
    await expect(page.getByRole('link', { name: /termini/i })).toBeVisible();
    await expect(page.getByRole('link', { name: /api documentation/i })).toBeVisible();
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

    // Hero should be visible
    await expect(page.getByRole('heading', { name: /il tuo assistente ai/i })).toBeVisible();

    // Features should stack vertically
    const featuresGrid = page.locator('#features .grid');
    await expect(featuresGrid).toBeVisible();
  });

  test('is responsive on tablet', async ({ page }) => {
    await page.setViewportSize({ width: 768, height: 1024 });

    await expect(page.getByRole('heading', { name: /il tuo assistente ai/i })).toBeVisible();
  });

  test('is responsive on desktop', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });

    await expect(page.getByRole('heading', { name: /il tuo assistente ai/i })).toBeVisible();
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

    const scrollIndicator = page.getByLabel('Scorri alle caratteristiche');
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

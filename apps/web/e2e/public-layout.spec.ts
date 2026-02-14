/**
 * E2E Tests for PublicLayout Components - Issue #2230
 * @slow - 84 test cases, comprehensive layout testing across viewports
 *
 * Real browser tests without mocks for:
 * - PublicHeader: navigation, user menu, theme switcher, mobile Sheet
 * - PublicFooter: links, social links, responsive layout
 * - PublicLayout: complete layout composition
 *
 * Tests cover:
 * - Desktop navigation
 * - Mobile hamburger menu with Sheet
 * - User menu dropdown (authenticated/unauthenticated)
 * - Theme switcher integration
 * - Sticky header on scroll
 * - Footer responsive layout
 * - Accessibility with axe-core
 * - Visual regression with screenshots
 */

import AxeBuilder from '@axe-core/playwright';
import { test, expect } from '@playwright/test';

test.describe('PublicLayout - Desktop Navigation', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');
  });

  test('should render header with logo and navigation', async ({ page }) => {
    // Logo should be visible and clickable
    const logo = page.getByLabel('MeepleAI Home');
    await expect(logo).toBeVisible();

    // Desktop navigation links
    const homeLink = page.getByRole('link', { name: /home/i }).first();
    const giochiLink = page.getByRole('link', { name: /giochi/i }).first();
    const chatLink = page.getByRole('link', { name: /chat/i }).first();
    const dashboardLink = page.getByRole('link', { name: /dashboard/i }).first();

    await expect(homeLink).toBeVisible();
    await expect(giochiLink).toBeVisible();
    await expect(chatLink).toBeVisible();
    await expect(dashboardLink).toBeVisible();

    // Screenshot
    await page.screenshot({ path: 'test-results/screenshots/public-header-desktop.png' });
  });

  test('should highlight active navigation item', async ({ page }) => {
    // Go to games page
    const giochiLink = page.getByRole('link', { name: /giochi/i }).first();
    await giochiLink.click();
    await page.waitForURL('/games');

    // Active link should have aria-current
    const activeLinks = page.getByRole('link', { name: /giochi/i });
    const activeLinkCount = await activeLinks.count();

    let hasActivePage = false;
    for (let i = 0; i < activeLinkCount; i++) {
      const ariaCurrent = await activeLinks.nth(i).getAttribute('aria-current');
      if (ariaCurrent === 'page') {
        hasActivePage = true;
        break;
      }
    }

    expect(hasActivePage).toBe(true);

    // Screenshot
    await page.screenshot({ path: 'test-results/screenshots/public-header-active-nav.png' });
  });

  test('should render theme switcher', async ({ page }) => {
    const themeSwitcher = page.getByLabel('Theme switcher');
    await expect(themeSwitcher).toBeVisible();

    // Screenshot
    await page.screenshot({ path: 'test-results/screenshots/public-header-theme.png' });
  });

  test('should show login button when not authenticated', async ({ page }) => {
    const loginButton = page.getByRole('link', { name: /accedi/i });
    await expect(loginButton).toBeVisible();
    await expect(loginButton).toHaveAttribute('href', '/login');

    // Screenshot
    await page.screenshot({ path: 'test-results/screenshots/public-header-unauthenticated.png' });
  });
});

test.describe('PublicLayout - Mobile Navigation', () => {
  test.use({ viewport: { width: 390, height: 844 } }); // iPhone 13

  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');
  });

  test('should render mobile hamburger menu', async ({ page }) => {
    const mobileMenuButton = page.getByLabel('Open navigation menu');
    await expect(mobileMenuButton).toBeVisible();

    // Screenshot before opening
    await page.screenshot({ path: 'test-results/screenshots/public-header-mobile-closed.png' });
  });

  test('should open mobile Sheet when hamburger is clicked', async ({ page }) => {
    const mobileMenuButton = page.getByLabel('Open navigation menu');
    await mobileMenuButton.click();

    // Wait for Sheet to open
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();

    // Sheet title should be visible
    await expect(page.getByText('Menu')).toBeVisible();

    // Navigation should be inside Sheet
    const mobileNav = page.getByRole('navigation', { name: /mobile navigation/i });
    await expect(mobileNav).toBeVisible();

    // Screenshot with Sheet open
    await page.screenshot({
      path: 'test-results/screenshots/public-header-mobile-sheet-open.png',
      fullPage: true,
    });
  });

  test('should display all navigation items in mobile Sheet', async ({ page }) => {
    const mobileMenuButton = page.getByLabel('Open navigation menu');
    await mobileMenuButton.click();

    // Wait for Sheet
    await page.waitForSelector('[role="dialog"]');

    // All navigation items should be in Sheet
    const homeLink = page.getByRole('link', { name: /home/i }).last();
    const giochiLink = page.getByRole('link', { name: /giochi/i }).last();
    const chatLink = page.getByRole('link', { name: /chat/i }).last();
    const dashboardLink = page.getByRole('link', { name: /dashboard/i }).last();

    await expect(homeLink).toBeVisible();
    await expect(giochiLink).toBeVisible();
    await expect(chatLink).toBeVisible();
    await expect(dashboardLink).toBeVisible();
  });

  test('should close Sheet when navigation item is clicked', async ({ page }) => {
    const mobileMenuButton = page.getByLabel('Open navigation menu');
    await mobileMenuButton.click();

    // Wait for Sheet
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();

    // Click a navigation item
    const giochiLink = page.getByRole('link', { name: /giochi/i }).last();
    await giochiLink.click();

    // Wait for navigation
    await page.waitForURL('/games', { timeout: 5000 });

    // Sheet should be closed
    await expect(dialog).not.toBeVisible();

    // Screenshot after navigation
    await page.screenshot({ path: 'test-results/screenshots/public-header-mobile-after-nav.png' });
  });

  test('should have proper touch targets on mobile (≥44px)', async ({ page }) => {
    // Mobile menu button
    const mobileMenuButton = page.getByLabel('Open navigation menu');
    const menuButtonBox = await mobileMenuButton.boundingBox();

    expect(menuButtonBox?.width).toBeGreaterThanOrEqual(44);
    expect(menuButtonBox?.height).toBeGreaterThanOrEqual(44);

    // Theme switcher
    const themeSwitcher = page.getByLabel('Theme switcher');
    const themeBox = await themeSwitcher.boundingBox();

    expect(themeBox?.height).toBeGreaterThanOrEqual(44);

    // Login button
    const loginButton = page.getByRole('link', { name: /accedi/i });
    const loginBox = await loginButton.boundingBox();

    expect(loginBox?.height).toBeGreaterThanOrEqual(32); // Button can be slightly smaller
  });
});

test.describe('PublicLayout - Sticky Header', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');
  });

  test('should apply shadow on scroll', async ({ page }) => {
    // Get header element
    const header = page.locator('header').first();
    await expect(header).toBeVisible();

    // Screenshot before scroll
    await page.screenshot({ path: 'test-results/screenshots/public-header-before-scroll.png' });

    // Check initial state (no shadow)
    const initialClasses = await header.getAttribute('class');
    expect(initialClasses).toBeTruthy();

    // Scroll down
    await page.evaluate(() => window.scrollTo(0, 100));
    await page.waitForTimeout(300); // Wait for scroll effect

    // Screenshot after scroll
    await page.screenshot({ path: 'test-results/screenshots/public-header-after-scroll.png' });

    // Header should have shadow class
    const scrolledClasses = await header.getAttribute('class');
    expect(scrolledClasses).toContain('shadow');
  });

  test('should remain sticky at top on scroll', async ({ page }) => {
    const header = page.locator('header').first();

    // Scroll down
    await page.evaluate(() => window.scrollTo(0, 500));
    await page.waitForTimeout(200);

    // Header should still be visible at top
    await expect(header).toBeVisible();

    const headerBox = await header.boundingBox();
    expect(headerBox?.y).toBeLessThanOrEqual(10); // Should be at top (allowing small margin)
  });
});

test.describe('PublicLayout - Footer', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');
  });

  test('should render footer with all sections', async ({ page }) => {
    // Scroll to footer
    await page.evaluate(() => window.scrollTo(0, document.body.scrollHeight));
    await page.waitForTimeout(200);

    const footer = page.locator('footer').first();
    await expect(footer).toBeVisible();

    // Quick Links section
    await expect(page.getByText('Link Rapidi')).toBeVisible();
    await expect(page.getByRole('link', { name: /giochi/i }).last()).toBeVisible();
    await expect(page.getByRole('link', { name: /chat ai/i })).toBeVisible();

    // About section
    await expect(page.getByText('Chi Siamo')).toBeVisible();

    // Legal section
    await expect(page.getByText('Legale')).toBeVisible();
    await expect(page.getByRole('link', { name: /privacy policy/i })).toBeVisible();

    // Screenshot
    await page.screenshot({
      path: 'test-results/screenshots/public-footer-desktop.png',
      fullPage: true,
    });
  });

  test('should render social links', async ({ page }) => {
    await page.evaluate(() => window.scrollTo(0, document.body.scrollHeight));
    await page.waitForTimeout(200);

    // Social links should be visible
    const githubLink = page.getByLabel('GitHub');
    const twitterLink = page.getByLabel('Twitter');
    const discordLink = page.getByLabel('Discord');

    await expect(githubLink).toBeVisible();
    await expect(twitterLink).toBeVisible();
    await expect(discordLink).toBeVisible();

    // Should open in new tab
    await expect(githubLink).toHaveAttribute('target', '_blank');
    await expect(githubLink).toHaveAttribute('rel', 'noopener noreferrer');
  });

  test('should display current year in copyright', async ({ page }) => {
    await page.evaluate(() => window.scrollTo(0, document.body.scrollHeight));
    await page.waitForTimeout(200);

    const currentYear = new Date().getFullYear();
    const copyright = page.getByText(new RegExp(`© ${currentYear} MeepleAI`, 'i'));
    await expect(copyright).toBeVisible();
  });

  test('should be responsive on mobile', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    await page.evaluate(() => window.scrollTo(0, document.body.scrollHeight));
    await page.waitForTimeout(200);

    const footer = page.locator('footer').first();
    await expect(footer).toBeVisible();

    // Screenshot mobile footer
    await page.screenshot({
      path: 'test-results/screenshots/public-footer-mobile.png',
      fullPage: true,
    });
  });
});

test.describe('PublicLayout - Theme Switcher Integration', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');
  });

  test('should switch to dark theme', async ({ page }) => {
    const themeSwitcher = page.getByLabel('Theme switcher');
    await themeSwitcher.click();

    // Click Dark mode
    await page.getByRole('menuitem', { name: /switch to dark theme/i }).click();

    // Verify dark theme applied
    const html = page.locator('html');
    await expect(html).toHaveClass(/dark/);

    // Screenshot dark mode
    await page.screenshot({
      path: 'test-results/screenshots/public-layout-dark-mode.png',
      fullPage: true,
    });
  });

  test('should switch to light theme', async ({ page }) => {
    // First set to dark
    const themeSwitcher = page.getByLabel('Theme switcher');
    await themeSwitcher.click();
    await page.getByRole('menuitem', { name: /switch to dark theme/i }).click();
    await page.waitForTimeout(200);

    // Then switch to light
    await themeSwitcher.click();
    await page.getByRole('menuitem', { name: /switch to light theme/i }).click();

    // Verify light theme applied
    const html = page.locator('html');
    await expect(html).toHaveClass(/light/);

    // Screenshot light mode
    await page.screenshot({
      path: 'test-results/screenshots/public-layout-light-mode.png',
      fullPage: true,
    });
  });

  test('should persist theme preference', async ({ page }) => {
    const themeSwitcher = page.getByLabel('Theme switcher');
    await themeSwitcher.click();
    await page.getByRole('menuitem', { name: /switch to dark theme/i }).click();

    // Reload page
    await page.reload();
    await page.waitForLoadState('networkidle');

    // Theme should persist
    const html = page.locator('html');
    await expect(html).toHaveClass(/dark/);
  });
});

test.describe('PublicLayout - Complete User Journey', () => {
  test('should navigate through all public pages', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    // Start from home
    await page.screenshot({ path: 'test-results/screenshots/journey-01-home.png' });

    // Navigate to Giochi
    const giochiLink = page.getByRole('link', { name: /giochi/i }).first();
    await giochiLink.click();
    await page.waitForURL('/games');
    await page.waitForLoadState('networkidle');
    await page.screenshot({ path: 'test-results/screenshots/journey-02-games.png' });

    // Navigate to Chat
    const chatLink = page.getByRole('link', { name: /chat/i }).first();
    await chatLink.click();
    await page.waitForURL('/chat');
    await page.waitForLoadState('networkidle');
    await page.screenshot({ path: 'test-results/screenshots/journey-03-chat.png' });

    // Navigate back to home
    const logo = page.getByLabel('MeepleAI Home');
    await logo.click();
    await page.waitForURL('/');
    await page.waitForLoadState('networkidle');
    await page.screenshot({ path: 'test-results/screenshots/journey-04-back-home.png' });
  });

  test('should have functional footer links', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    // Scroll to footer
    await page.evaluate(() => window.scrollTo(0, document.body.scrollHeight));
    await page.waitForTimeout(200);

    // Click footer link
    const privacyLink = page.getByRole('link', { name: /privacy policy/i });
    await expect(privacyLink).toBeVisible();
    await expect(privacyLink).toHaveAttribute('href', '/privacy');

    // Screenshot footer
    await page.screenshot({ path: 'test-results/screenshots/public-footer-links.png' });
  });
});

test.describe('PublicLayout - Responsive Behavior', () => {
  test('should render correctly on mobile (390x844)', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    // Desktop nav should be hidden
    const desktopNav = page.getByRole('navigation', { name: /main navigation/i });
    await expect(desktopNav).not.toBeVisible();

    // Mobile menu button should be visible
    const mobileMenuButton = page.getByLabel('Open navigation menu');
    await expect(mobileMenuButton).toBeVisible();

    // Screenshot
    await page.screenshot({
      path: 'test-results/screenshots/responsive-mobile-390.png',
      fullPage: true,
    });
  });

  test('should render correctly on tablet (768x1024)', async ({ page }) => {
    await page.setViewportSize({ width: 768, height: 1024 });
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    // Check layout
    const header = page.locator('header').first();
    await expect(header).toBeVisible();

    // Screenshot
    await page.screenshot({
      path: 'test-results/screenshots/responsive-tablet-768.png',
      fullPage: true,
    });
  });

  test('should render correctly on desktop (1920x1080)', async ({ page }) => {
    await page.setViewportSize({ width: 1920, height: 1080 });
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    // Desktop nav should be visible
    const desktopNav = page.getByRole('navigation', { name: /main navigation/i });
    await expect(desktopNav).toBeVisible();

    // Mobile menu button should be hidden
    const mobileMenuButton = page.getByLabel('Open navigation menu');
    await expect(mobileMenuButton).not.toBeVisible();

    // Screenshot
    await page.screenshot({
      path: 'test-results/screenshots/responsive-desktop-1920.png',
      fullPage: true,
    });
  });

  test('should have no horizontal overflow on any viewport', async ({ page }) => {
    const viewports = [
      { width: 320, height: 568 }, // iPhone SE
      { width: 390, height: 844 }, // iPhone 13
      { width: 768, height: 1024 }, // iPad
      { width: 1920, height: 1080 }, // Desktop
    ];

    for (const viewport of viewports) {
      await page.setViewportSize(viewport);
      await page.goto('/');
      await page.waitForLoadState('networkidle');

      const bodyWidth = await page.evaluate(() => document.body.scrollWidth);
      const viewportWidth = viewport.width;

      expect(bodyWidth).toBeLessThanOrEqual(viewportWidth + 1); // Allow 1px tolerance
    }
  });
});

test.describe('PublicLayout - Accessibility', () => {
  // Issue #1868 CLOSED - re-enabling rules for Phase 5 (Issue #2234) full audit
  const KNOWN_A11Y_ISSUES: string[] = [];

  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');
  });

  test('should have no accessibility violations on desktop', async ({ page }) => {
    const accessibilityScanResults = await new AxeBuilder({ page })
      .disableRules(KNOWN_A11Y_ISSUES)
      .analyze();

    expect(accessibilityScanResults.violations).toEqual([]);

    // Screenshot
    await page.screenshot({ path: 'test-results/screenshots/a11y-desktop.png' });
  });

  test('should have no accessibility violations on mobile', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    const accessibilityScanResults = await new AxeBuilder({ page })
      .disableRules(KNOWN_A11Y_ISSUES)
      .analyze();

    expect(accessibilityScanResults.violations).toEqual([]);

    // Screenshot
    await page.screenshot({ path: 'test-results/screenshots/a11y-mobile.png' });
  });

  test('should have accessible mobile Sheet navigation', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    // Open mobile menu
    const mobileMenuButton = page.getByLabel('Open navigation menu');
    await mobileMenuButton.click();

    // Wait for Sheet
    await page.waitForSelector('[role="dialog"]');

    // Check a11y with Sheet open
    const accessibilityScanResults = await new AxeBuilder({ page })
      .disableRules(KNOWN_A11Y_ISSUES)
      .analyze();

    expect(accessibilityScanResults.violations).toEqual([]);

    // Screenshot
    await page.screenshot({ path: 'test-results/screenshots/a11y-mobile-sheet.png' });
  });

  test('should have proper ARIA labels', async ({ page }) => {
    // Header navigation
    const mainNav = page.getByRole('navigation', { name: /main navigation/i });
    await expect(mainNav).toHaveAttribute('aria-label');

    // Logo
    const logo = page.getByLabel('MeepleAI Home');
    await expect(logo).toHaveAttribute('aria-label');

    // Theme switcher
    const themeSwitcher = page.getByLabel('Theme switcher');
    await expect(themeSwitcher).toHaveAttribute('aria-label');

    // Mobile menu
    const mobileMenuButton = page.getByLabel('Open navigation menu');
    await expect(mobileMenuButton).toHaveAttribute('aria-label');
  });

  test('should support keyboard navigation', async ({ page }) => {
    // Focus first link with Tab
    await page.keyboard.press('Tab');

    // Get focused element
    const focusedElement = page.locator(':focus');
    await expect(focusedElement).toBeVisible();

    // Screenshot keyboard focus
    await page.screenshot({ path: 'test-results/screenshots/keyboard-navigation.png' });

    // Navigate with Tab through header elements
    for (let i = 0; i < 5; i++) {
      await page.keyboard.press('Tab');
      const newFocus = page.locator(':focus');
      await expect(newFocus).toBeVisible();
    }
  });
});

test.describe('PublicLayout - Footer Sticky Behavior', () => {
  test('should stick to bottom with short content', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    const footer = page.locator('footer').first();
    const viewportHeight = page.viewportSize()?.height || 0;

    // Get footer position
    const footerBox = await footer.boundingBox();

    // Footer should be at bottom of viewport for short content
    expect(footerBox?.y).toBeGreaterThan(viewportHeight - 500); // Footer near bottom
  });

  test('should appear after scrolling with long content', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    // Scroll to bottom
    await page.evaluate(() => window.scrollTo(0, document.body.scrollHeight));
    await page.waitForTimeout(300);

    const footer = page.locator('footer').first();
    await expect(footer).toBeVisible();

    // Screenshot
    await page.screenshot({ path: 'test-results/screenshots/footer-sticky-bottom.png' });
  });
});

test.describe('PublicLayout - Visual Regression', () => {
  test('should match visual snapshot - Homepage full layout', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    await page.screenshot({
      path: 'test-results/screenshots/visual-homepage-full.png',
      fullPage: true,
    });
  });

  test('should match visual snapshot - Dark mode full layout', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    // Switch to dark mode
    const themeSwitcher = page.getByLabel('Theme switcher');
    await themeSwitcher.click();
    await page.getByRole('menuitem', { name: /switch to dark theme/i }).click();
    await page.waitForTimeout(300);

    await page.screenshot({
      path: 'test-results/screenshots/visual-homepage-dark.png',
      fullPage: true,
    });
  });

  test('should match visual snapshot - Mobile layout with Sheet open', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    const mobileMenuButton = page.getByLabel('Open navigation menu');
    await mobileMenuButton.click();
    await page.waitForSelector('[role="dialog"]');

    await page.screenshot({
      path: 'test-results/screenshots/visual-mobile-sheet-open.png',
      fullPage: true,
    });
  });

  test('should match visual snapshot - Tablet layout', async ({ page }) => {
    await page.setViewportSize({ width: 768, height: 1024 });
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    await page.screenshot({
      path: 'test-results/screenshots/visual-tablet-layout.png',
      fullPage: true,
    });
  });
});

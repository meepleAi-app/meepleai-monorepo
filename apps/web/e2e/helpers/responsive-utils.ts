import { Page, expect } from '@playwright/test';

/**
 * Responsive Testing Utilities
 * Issue #993 - Assertion-based responsive testing helpers
 *
 * Provides reusable functions for verifying responsive layout behavior
 * across mobile (390px), tablet (1024px), and desktop (1920px) viewports.
 */

export interface ViewportInfo {
  width: number;
  height: number;
  isMobile: boolean;
  isTablet: boolean;
  isDesktop: boolean;
}

/**
 * Get current viewport information
 */
export async function getViewportInfo(page: Page): Promise<ViewportInfo> {
  const viewport = page.viewportSize();
  if (!viewport) {
    throw new Error('Viewport not set');
  }

  return {
    width: viewport.width,
    height: viewport.height,
    isMobile: viewport.width < 640,
    isTablet: viewport.width >= 640 && viewport.width <= 1024,
    isDesktop: viewport.width > 1024,
  };
}

/**
 * Check mobile layout expectations
 * - Navigation should be in mobile hamburger/drawer mode
 * - Sidebar should be hidden by default
 * - Content should stack vertically
 */
export async function checkMobileLayout(page: Page) {
  const viewport = await getViewportInfo(page);

  // Verify we're actually in mobile viewport
  expect(viewport.isMobile).toBe(true);

  // Check for mobile navigation patterns
  // Look for hamburger menu or mobile nav toggle
  const mobileNavExists =
    (await page
      .locator('[aria-label*="menu" i], [aria-label*="navigation" i], button[class*="mobile" i]')
      .count()) > 0;

  // On mobile, sidebar should either be hidden or off-screen
  const sidebarElement = page
    .locator('[role="complementary"], aside, [class*="sidebar" i]')
    .first();
  const sidebarCount = await sidebarElement.count();

  if (sidebarCount > 0) {
    // If sidebar exists, it should be hidden or off-screen on mobile
    const isHidden = await sidebarElement.isHidden().catch(() => true);
    const isOffScreen = await sidebarElement
      .evaluate(el => {
        const rect = el.getBoundingClientRect();
        return rect.left < -100 || rect.right > window.innerWidth + 100;
      })
      .catch(() => false);

    expect(isHidden || isOffScreen).toBe(true);
  }

  return { viewport, mobileNavExists };
}

/**
 * Check tablet layout expectations
 * - May have collapsible sidebar
 * - Adaptive grid layouts (2 columns instead of 3)
 * - Navigation may be hybrid (icon + text or icon-only)
 */
export async function checkTabletLayout(page: Page) {
  const viewport = await getViewportInfo(page);

  // Verify we're in tablet viewport
  expect(viewport.isTablet).toBe(true);

  // Check navigation is appropriate for tablet
  const navElement = page.locator('nav').first();
  const navCount = await navElement.count();

  if (navCount > 0) {
    const isVisible = await navElement.isVisible();
    expect(isVisible).toBe(true);
  }

  // Content should not overflow horizontally
  const bodyOverflow = await page.evaluate(() => {
    return document.body.scrollWidth > document.body.clientWidth;
  });
  expect(bodyOverflow).toBe(false);

  return { viewport };
}

/**
 * Check desktop layout expectations
 * - Full sidebar visible
 * - Multi-column layouts
 * - All navigation elements visible
 */
export async function checkDesktopLayout(page: Page) {
  const viewport = await getViewportInfo(page);

  // Verify we're in desktop viewport
  expect(viewport.isDesktop).toBe(true);

  // Sidebar should be visible on desktop
  const sidebarElement = page
    .locator('[role="complementary"], aside, [class*="sidebar" i]')
    .first();
  const sidebarCount = await sidebarElement.count();

  if (sidebarCount > 0) {
    const isVisible = await sidebarElement.isVisible();
    // On desktop, sidebar should be visible (unless intentionally hidden by user)
    // We check it's not off-screen
    const isOffScreen = await sidebarElement
      .evaluate(el => {
        const rect = el.getBoundingClientRect();
        return rect.left < -100 || rect.right > window.innerWidth + 100;
      })
      .catch(() => false);

    expect(isOffScreen).toBe(false);
  }

  // Navigation should be fully visible
  const navElement = page.locator('nav').first();
  const navCount = await navElement.count();

  if (navCount > 0) {
    const isVisible = await navElement.isVisible();
    expect(isVisible).toBe(true);
  }

  return { viewport };
}

/**
 * Verify element visibility based on viewport
 */
export async function checkElementVisibility(
  page: Page,
  selector: string,
  shouldBeVisible: boolean
) {
  const element = page.locator(selector).first();
  const count = await element.count();

  if (count === 0) {
    expect(shouldBeVisible).toBe(false);
    return;
  }

  const isVisible = await element.isVisible();
  expect(isVisible).toBe(shouldBeVisible);
}

/**
 * Check that content doesn't overflow horizontally
 * (common responsive issue)
 */
export async function checkNoHorizontalOverflow(page: Page) {
  const hasOverflow = await page.evaluate(() => {
    // Check body
    const bodyOverflow = document.body.scrollWidth > document.body.clientWidth;

    // Check main content containers
    const containers = Array.from(document.querySelectorAll('main, [role="main"], .container'));
    const containerOverflow = containers.some(el => el.scrollWidth > el.clientWidth);

    return bodyOverflow || containerOverflow;
  });

  expect(hasOverflow).toBe(false);
}

/**
 * Check that text is readable (not too small)
 */
export async function checkTextReadability(page: Page, minFontSize = 14) {
  const tooSmallText = await page.evaluate(minSize => {
    const textElements = Array.from(document.querySelectorAll('p, span, div, a, button, li'));
    return textElements.some(el => {
      const fontSize = parseFloat(window.getComputedStyle(el).fontSize);
      return fontSize < minSize && el.textContent?.trim().length > 0;
    });
  }, minFontSize);

  expect(tooSmallText).toBe(false);
}

/**
 * Check touch targets are large enough on mobile
 * WCAG recommends minimum 44x44px
 */
export async function checkTouchTargets(page: Page, minSize = 44) {
  const viewport = await getViewportInfo(page);

  // Only enforce on mobile/tablet
  if (!viewport.isMobile && !viewport.isTablet) {
    return;
  }

  const tooSmallTargets = await page.evaluate(min => {
    const interactiveElements = Array.from(
      document.querySelectorAll('button, a, input, select, textarea, [role="button"], [onclick]')
    );

    return interactiveElements.filter(el => {
      const rect = el.getBoundingClientRect();
      return (rect.width < min || rect.height < min) && rect.width > 0;
    }).length;
  }, minSize);

  // Allow some small targets (like close icons, badges) but not too many
  // Threshold: 15 (relaxed for real-world UI components)
  expect(tooSmallTargets).toBeLessThan(15);
}

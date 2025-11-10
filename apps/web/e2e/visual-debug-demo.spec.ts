import { test, expect } from '@playwright/test';
import { getTextMatcher, t } from './fixtures/i18n';

/**
 * Visual Debug Demo - Investigate Portal Issue Interactively
 *
 * This test demonstrates how to use Playwright's pause() feature
 * to manually inspect the DOM and investigate issues like the
 * nextjs-portal click blocking problem.
 *
 * ⚠️  SKIPPED BY DEFAULT - These tests use page.pause() and require manual interaction
 *     This is a DEBUG FIXTURE only for development/troubleshooting
 *
 * To run interactively:
 *   pnpm playwright test visual-debug-demo.spec.ts --grep "Visual Debug Demo" --debug
 *   or
 *   pnpm test:e2e:ui visual-debug-demo.spec.ts
 *
 * When the test pauses:
 * - Inspect the DOM in the browser
 * - Check for <nextjs-portal> element
 * - Try clicking manually
 * - Use Playwright Inspector to step through
 *
 * To enable in code: Remove .skip from test.describe.skip below
 */

test.describe.skip('Visual Debug Demo', () => {
  test('investigate portal issue with manual inspection', async ({ page }) => {
    // Step 1: Navigate to home page
    console.log('📍 Step 1: Navigating to home page...');
    await page.goto('/');
    await page.waitForLoadState('domcontentloaded');

    // Step 2: Wait for page to be fully loaded
    console.log('⏳ Step 2: Waiting for network idle...');
    await page.waitForLoadState('networkidle');

    // Step 3: PAUSE - Inspect DOM manually
    console.log('🔍 Step 3: PAUSED - Inspect the DOM now!');
    console.log('   Look for:');
    console.log('   - <nextjs-portal> element');
    console.log('   - z-index on portal');
    console.log('   - pointer-events CSS property');
    console.log('   Click "Resume" in Inspector when ready to continue');

    await page.pause(); // 🛑 STOPS HERE - Use Inspector to investigate

    // Step 4: Try to find the portal
    console.log('🔍 Step 4: Checking for portal element...');
    const portal = page.locator('nextjs-portal');
    const portalCount = await portal.count();

    if (portalCount > 0) {
      console.log(`❌ Found ${portalCount} portal element(s)`);
      // Check z-index
      const zIndex = await portal.first().evaluate((el) => {
        return window.getComputedStyle(el).zIndex;
      });
      console.log(`   z-index: ${zIndex}`);

      // Check pointer-events
      const pointerEvents = await portal.first().evaluate((el) => {
        return window.getComputedStyle(el).pointerEvents;
      });
      console.log(`   pointer-events: ${pointerEvents}`);

      // Check if it's visible
      const isVisible = await portal.first().isVisible();
      console.log(`   visible: ${isVisible}`);
    } else {
      console.log('✅ No portal element found');
    }

    // Step 5: Try to click Get Started button
    console.log('🖱️  Step 5: Attempting to click Get Started...');
    const getStartedButton = page.getByTestId('hero-get-started');
    const buttonVisible = await getStartedButton.isVisible();
    console.log(`   Button visible: ${buttonVisible}`);

    // PAUSE again before click attempt
    console.log('🔍 PAUSED - Ready to attempt click');
    console.log('   Watch what happens when we try to click');
    await page.pause();

    try {
      // Try normal click (will likely fail if portal exists)
      await getStartedButton.click({ timeout: 5000 });
      console.log('✅ Click succeeded!');
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : String(error);
      console.log('❌ Click failed:', errorMessage);

      // Try force click
      console.log('🔧 Attempting force click...');
      try {
        await getStartedButton.click({ force: true, timeout: 5000 });
        console.log('✅ Force click succeeded (but handler might not fire)');
      } catch (forceError) {
        const forceErrorMessage = forceError instanceof Error ? forceError.message : String(forceError);
        console.log('❌ Force click also failed:', forceErrorMessage);
      }
    }

    // Final pause to inspect results
    console.log('🔍 FINAL PAUSE - Inspect final state');
    await page.pause();
  });

  test('compare with mock auth approach', async ({ page }) => {
    console.log('✅ Mock Auth Approach - No Portal Issues');

    // Setup mock auth (like fixtures/auth.ts does)
    await page.route('http://localhost:8080/api/v1/auth/me', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          user: {
            id: 'test-user',
            email: 'test@example.com',
            displayName: 'Test User',
            role: 'User',
          },
          expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
        }),
      });
    });

    // Navigate directly to authenticated page
    console.log('🚀 Navigating directly to /chat (bypassing home page)');
    await page.goto('/chat');
    await page.waitForLoadState('networkidle');

    // PAUSE to compare
    console.log('🔍 PAUSED - Notice:');
    console.log('   - No home page loaded');
    console.log('   - No login modal');
    console.log('   - No portal issue');
    console.log('   - Direct access to authenticated feature');
    await page.pause();

    // Check for portal
    const portal = page.locator('nextjs-portal');
    const portalCount = await portal.count();
    console.log(`Portal elements found: ${portalCount} (should be 0)`);
    expect(portalCount).toBe(0);
    console.log('✅ Test complete - No portal issues with mock auth!');
  });
});

test.describe('Automated Visual Checks', () => {
  test('detect portal on home page', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    // Check if portal exists
    const portal = page.locator('nextjs-portal');
    const exists = (await portal.count()) > 0;

    if (exists) {
      // Get portal properties
      const computedStyle = await portal.first().evaluate((el) => {
        const style = window.getComputedStyle(el);
        return {
          zIndex: style.zIndex,
          position: style.position,
          pointerEvents: style.pointerEvents,
          display: style.display,
          opacity: style.opacity,
          width: style.width,
          height: style.height,
        };
      });

      console.log('Portal detected with properties:', computedStyle);

      // Take screenshot for visual reference
      await page.screenshot({
        path: 'test-results/portal-detected.png',
        fullPage: true,
      });

      console.log('📸 Screenshot saved: test-results/portal-detected.png');

      // This test documents the issue but doesn't fail
      // (since it's a known issue)
      test.info().annotations.push({
        type: 'known-issue',
        description: 'nextjs-portal blocks clicks in headless Chromium',
      });
    } else {
      console.log('✅ No portal detected');
    }
  });

  test('verify portal absent on authenticated pages', async ({ page }) => {
    // Setup mock auth
    await page.route('http://localhost:8080/api/v1/auth/me', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          user: {
            id: 'test-user',
            email: 'user@meepleai.dev',
            displayName: 'Test User',
            role: 'User',
          },
          expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
        }),
      });
    });

    // Test multiple authenticated pages
    const authenticatedPages = ['/chat', '/upload', '/versions'];

    for (const path of authenticatedPages) {
      await page.goto(path);
      await page.waitForLoadState('networkidle');

      const portal = page.locator('nextjs-portal');
      const count = await portal.count();

      console.log(`${path}: portal count = ${count}`);
      expect(count).toBe(0);
    }

    console.log('✅ All authenticated pages are portal-free');
  });
});
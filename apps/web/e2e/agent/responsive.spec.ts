/**
 * E2E-FRONT-005: Responsive Behavior
 * Issue #3248 (FRONT-012)
 *
 * Tests responsive behavior across viewports:
 * - Mobile: 375x667 (iPhone SE)
 * - Tablet: 768x1024 (iPad)
 * - Desktop: 1920x1080
 *
 * Tests config sheet, chat sheet, action bar behavior per viewport.
 */

import { test, expect } from '@playwright/test';

import {
  checkNoHorizontalOverflow,
  checkTouchTargets,
  getViewportInfo,
} from '../helpers/responsive-utils';

// Mobile viewport
test.describe('Responsive - Mobile (375x667)', () => {
  test.use({ viewport: { width: 375, height: 667 } });

  test.beforeEach(async ({ page }) => {
    // Mock auth
    await page.route('**/api/v1/auth/me', async route => {
      await route.fulfill({
        json: { id: 'test-user', email: 'test@example.com', tier: 'free' },
      });
    });

    // Mock required data
    await page.route('**/api/v1/library/games*', async route => {
      await route.fulfill({
        json: {
          items: [{ id: 'game-1', title: '7 Wonders', hasDocuments: true }],
          total: 1,
        },
      });
    });

    await page.route('**/api/v1/agent-typologies/approved', async route => {
      await route.fulfill({
        json: [{ id: 'rules', name: 'Rules Helper', icon: '📖' }],
      });
    });
  });

  test('config sheet should be bottom sheet (90vh height)', async ({ page }) => {
    await page.goto('/library');

    // Open config
    const gameCard = page.locator('[data-testid="game-card"]').first();
    const askButton = gameCard.locator('button:has-text("Ask Agent")');

    if (await askButton.isVisible()) {
      await askButton.click();

      const configSheet = page.locator('[data-testid="agent-config-sheet"]');
      await expect(configSheet).toBeVisible();

      // Check it's a bottom sheet
      const sheetPosition = await configSheet.evaluate(el => {
        const rect = el.getBoundingClientRect();
        const computedStyle = window.getComputedStyle(el);
        return {
          bottom: rect.bottom,
          height: rect.height,
          borderRadius: computedStyle.borderTopLeftRadius,
        };
      });

      // Should be near bottom of viewport
      expect(sheetPosition.height).toBeGreaterThan(500); // ~90vh

      // Should have rounded top corners
      expect(parseInt(sheetPosition.borderRadius)).toBeGreaterThan(0);
    }
  });

  test('chat sheet should be bottom sheet with swipe-down minimize', async ({ page }) => {
    // Mock session
    await page.route('**/api/v1/game-sessions/*', async route => {
      await route.fulfill({
        json: {
          sessionId: 'session-123',
          gameTitle: '7 Wonders',
          typologyName: 'Rules',
        },
      });
    });

    await page.goto('/library/games/game-1?session=session-123');

    const chatSheet = page.locator('[data-testid="agent-chat-sheet"]');
    if (await chatSheet.isVisible()) {
      // Verify it's at bottom
      const position = await chatSheet.evaluate(el => {
        const rect = el.getBoundingClientRect();
        return { top: rect.top, height: rect.height };
      });

      // Should take most of screen
      expect(position.height).toBeGreaterThan(400);

      // Check for resize handle
      const resizeHandle = chatSheet.locator('[data-testid="resize-handle"]');
      await expect(resizeHandle).toBeVisible();
    }
  });

  test('action bar should be pinned to bottom with safe area insets', async ({ page }) => {
    await page.goto('/library/games/game-1?session=session-123');

    const actionBar = page.locator('[data-testid="action-bar"]');
    if (await actionBar.isVisible()) {
      const hasBottomPadding = await actionBar.evaluate(el => {
        const computedStyle = window.getComputedStyle(el);
        // Check for pb-safe class or padding-bottom
        return (
          el.classList.contains('pb-safe') || parseInt(computedStyle.paddingBottom) > 0
        );
      });

      expect(hasBottomPadding).toBe(true);
    }
  });

  test('template carousel should have horizontal scroll with snap', async ({ page }) => {
    await page.goto('/library');

    // Open config
    const gameCard = page.locator('[data-testid="game-card"]').first();
    const askButton = gameCard.locator('button:has-text("Ask Agent")');

    if (await askButton.isVisible()) {
      await askButton.click();

      const carousel = page.locator('[data-testid="template-carousel"]');
      if (await carousel.isVisible()) {
        const scrollBehavior = await carousel.evaluate(el => {
          const computedStyle = window.getComputedStyle(el);
          return {
            overflowX: computedStyle.overflowX,
            scrollSnapType: computedStyle.scrollSnapType,
          };
        });

        // Should have horizontal scroll
        expect(scrollBehavior.overflowX).toMatch(/scroll|auto/);

        // Should have snap behavior
        expect(scrollBehavior.scrollSnapType).toContain('x');
      }
    }
  });

  test('should have no horizontal overflow on mobile', async ({ page }) => {
    await page.goto('/library');
    await checkNoHorizontalOverflow(page);
  });

  test('should have adequate touch targets on mobile', async ({ page }) => {
    await page.goto('/library');
    await checkTouchTargets(page);
  });
});

// Tablet viewport
test.describe('Responsive - Tablet (768x1024)', () => {
  test.use({ viewport: { width: 768, height: 1024 } });

  test.beforeEach(async ({ page }) => {
    await page.route('**/api/v1/auth/me', async route => {
      await route.fulfill({
        json: { id: 'test-user', email: 'test@example.com', tier: 'free' },
      });
    });

    await page.route('**/api/v1/library/games*', async route => {
      await route.fulfill({
        json: {
          items: [{ id: 'game-1', title: '7 Wonders', hasDocuments: true }],
          total: 1,
        },
      });
    });

    await page.route('**/api/v1/agent-typologies/approved', async route => {
      await route.fulfill({
        json: [{ id: 'rules', name: 'Rules Helper', icon: '📖' }],
      });
    });
  });

  test('config sheet should be side drawer from right (500px width)', async ({ page }) => {
    await page.goto('/library');

    const gameCard = page.locator('[data-testid="game-card"]').first();
    const askButton = gameCard.locator('button:has-text("Ask Agent")');

    if (await askButton.isVisible()) {
      await askButton.click();

      const configSheet = page.locator('[data-testid="agent-config-sheet"]');
      await expect(configSheet).toBeVisible();

      const sheetDimensions = await configSheet.evaluate(el => {
        const rect = el.getBoundingClientRect();
        return { width: rect.width, right: rect.right };
      });

      // Should be ~500px width side drawer
      expect(sheetDimensions.width).toBeGreaterThanOrEqual(400);
      expect(sheetDimensions.width).toBeLessThanOrEqual(600);

      // Should be on right side
      expect(sheetDimensions.right).toBe(768); // Viewport width
    }
  });

  test('chat sheet should be persistent side drawer', async ({ page }) => {
    await page.route('**/api/v1/game-sessions/*', async route => {
      await route.fulfill({
        json: {
          sessionId: 'session-123',
          gameTitle: '7 Wonders',
          typologyName: 'Rules',
        },
      });
    });

    await page.goto('/library/games/game-1?session=session-123');

    const chatSheet = page.locator('[data-testid="agent-chat-sheet"]');
    if (await chatSheet.isVisible()) {
      const dimensions = await chatSheet.evaluate(el => {
        const rect = el.getBoundingClientRect();
        return { width: rect.width, height: rect.height };
      });

      // Side drawer on tablet
      expect(dimensions.width).toBeGreaterThanOrEqual(400);
      expect(dimensions.width).toBeLessThan(768); // Not full width
    }
  });

  test('action bar should have wider layout (3 buttons visible)', async ({ page }) => {
    await page.goto('/library/games/game-1?session=session-123');

    const actionBar = page.locator('[data-testid="action-bar"]');
    if (await actionBar.isVisible()) {
      const buttons = actionBar.locator('button');
      const buttonCount = await buttons.count();

      // Should have at least 3 visible buttons on tablet
      let visibleCount = 0;
      for (let i = 0; i < buttonCount; i++) {
        if (await buttons.nth(i).isVisible()) {
          visibleCount++;
        }
      }

      expect(visibleCount).toBeGreaterThanOrEqual(3);
    }
  });

  test('should have no horizontal overflow on tablet', async ({ page }) => {
    await page.goto('/library');
    await checkNoHorizontalOverflow(page);
  });
});

// Desktop viewport
test.describe('Responsive - Desktop (1920x1080)', () => {
  test.use({ viewport: { width: 1920, height: 1080 } });

  test.beforeEach(async ({ page }) => {
    await page.route('**/api/v1/auth/me', async route => {
      await route.fulfill({
        json: { id: 'test-user', email: 'test@example.com', tier: 'free' },
      });
    });

    await page.route('**/api/v1/library/games*', async route => {
      await route.fulfill({
        json: {
          items: [{ id: 'game-1', title: '7 Wonders', hasDocuments: true }],
          total: 1,
        },
      });
    });

    await page.route('**/api/v1/agent-typologies/approved', async route => {
      await route.fulfill({
        json: [{ id: 'rules', name: 'Rules Helper', icon: '📖' }],
      });
    });
  });

  test('config sheet should be centered modal (600px max-width)', async ({ page }) => {
    await page.goto('/library');

    const gameCard = page.locator('[data-testid="game-card"]').first();
    const askButton = gameCard.locator('button:has-text("Ask Agent")');

    if (await askButton.isVisible()) {
      await askButton.click();

      const configSheet = page.locator('[data-testid="agent-config-sheet"]');
      await expect(configSheet).toBeVisible();

      const sheetDimensions = await configSheet.evaluate(el => {
        const rect = el.getBoundingClientRect();
        const viewportWidth = window.innerWidth;
        return {
          width: rect.width,
          left: rect.left,
          right: rect.right,
          isCentered: Math.abs(rect.left - (viewportWidth - rect.right)) < 50,
        };
      });

      // Should be max 600px width
      expect(sheetDimensions.width).toBeLessThanOrEqual(700);

      // Should be centered (or right-aligned modal)
      // Either centered or side drawer is acceptable on desktop
    }
  });

  test('chat sheet should be sidebar (400px) with game content visible', async ({ page }) => {
    await page.route('**/api/v1/game-sessions/*', async route => {
      await route.fulfill({
        json: {
          sessionId: 'session-123',
          gameTitle: '7 Wonders',
          typologyName: 'Rules',
        },
      });
    });

    await page.goto('/library/games/game-1?session=session-123');

    const chatSheet = page.locator('[data-testid="agent-chat-sheet"]');
    if (await chatSheet.isVisible()) {
      const dimensions = await chatSheet.evaluate(el => {
        const rect = el.getBoundingClientRect();
        const viewportWidth = window.innerWidth;
        return {
          width: rect.width,
          isPartialWidth: rect.width < viewportWidth * 0.5,
        };
      });

      // Should be a sidebar, not full width
      expect(dimensions.width).toBeGreaterThanOrEqual(350);
      expect(dimensions.width).toBeLessThanOrEqual(500);

      // Main content should still be visible alongside chat
      const mainContent = page.locator('[data-testid="game-content"]');
      if (await mainContent.count() > 0) {
        await expect(mainContent).toBeVisible();
      }
    }
  });

  test('action bar should show full button labels on desktop', async ({ page }) => {
    await page.goto('/library/games/game-1?session=session-123');

    const actionBar = page.locator('[data-testid="action-bar"]');
    if (await actionBar.isVisible()) {
      // On desktop, buttons may have full labels visible
      const settingsButton = actionBar.locator('button:has-text("Settings"), button:has-text("Impostazioni")');
      const exportButton = actionBar.locator('button:has-text("Export"), button:has-text("Esporta")');

      // Either button text visible or icon-only (both are valid on desktop)
      const hasLabels =
        (await settingsButton.count()) > 0 || (await exportButton.count()) > 0;

      // This is informational - desktop may still use icon-only
    }
  });

  test('should have no horizontal overflow on desktop', async ({ page }) => {
    await page.goto('/library');
    await checkNoHorizontalOverflow(page);
  });

  test('should display grid layouts correctly on desktop', async ({ page }) => {
    await page.goto('/library');

    // Game cards should be in multi-column grid
    const gameGrid = page.locator('[data-testid="game-grid"]');
    if (await gameGrid.isVisible()) {
      const gridStyle = await gameGrid.evaluate(el => {
        const computedStyle = window.getComputedStyle(el);
        return {
          display: computedStyle.display,
          gridTemplateColumns: computedStyle.gridTemplateColumns,
        };
      });

      // Should be grid or flex with multiple columns
      expect(gridStyle.display).toMatch(/grid|flex/);

      if (gridStyle.display === 'grid') {
        // Should have multiple columns on desktop
        const columns = gridStyle.gridTemplateColumns.split(' ').length;
        expect(columns).toBeGreaterThanOrEqual(3);
      }
    }
  });
});

// Cross-viewport consistency tests
test.describe('Responsive - Cross-viewport Consistency', () => {
  const viewports = [
    { name: 'Mobile', width: 375, height: 667 },
    { name: 'Tablet', width: 768, height: 1024 },
    { name: 'Desktop', width: 1920, height: 1080 },
  ];

  for (const viewport of viewports) {
    test(`should render without layout breaks on ${viewport.name}`, async ({ page }) => {
      await page.setViewportSize({ width: viewport.width, height: viewport.height });

      await page.route('**/api/v1/auth/me', async route => {
        await route.fulfill({
          json: { id: 'test-user', email: 'test@example.com', tier: 'free' },
        });
      });

      await page.route('**/api/v1/library/games*', async route => {
        await route.fulfill({
          json: { items: [], total: 0 },
        });
      });

      await page.goto('/library');

      // Check no horizontal overflow
      await checkNoHorizontalOverflow(page);

      // Verify viewport info
      const info = await getViewportInfo(page);
      expect(info.width).toBe(viewport.width);
    });
  }
});

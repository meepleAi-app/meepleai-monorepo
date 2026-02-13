/**
 * E2E Test: PDF Progress UI Components (Issue #4210)
 *
 * Tests all 4 progress components with visual regression snapshots:
 * - ProgressModal: Full-screen modal with step indicators
 * - ProgressCard: Compact card for list views
 * - ProgressToast: Non-blocking toast notifications
 * - ProgressBadge: Minimal icon-only indicator
 *
 * Total scenarios: 23 test cases
 */

import { test, expect } from '@playwright/test';

test.describe('PDF Progress UI Components (Issue #4210)', () => {
  test.describe('ProgressModal - Full-screen Modal', () => {
    test.beforeEach(async ({ page }) => {
      // Navigate to Storybook story
      await page.goto('http://localhost:6006/?path=/story/pdf-progressmodal--default');
      await page.waitForLoadState('networkidle');
    });

    test('should display progress bar with correct value', async ({ page }) => {
      const iframe = page.frameLocator('#storybook-preview-iframe');

      // Open modal
      await iframe.locator('button', { hasText: 'Open Modal' }).click();

      // Check progress bar
      const progressBar = iframe.locator('[role="progressbar"]');
      await expect(progressBar).toBeVisible();
      await expect(progressBar).toHaveAttribute('aria-label', /Processing progress:/);

      // Visual snapshot
      await expect(iframe.locator('[role="dialog"]')).toHaveScreenshot('progress-modal-default.png');
    });

    test('should highlight current step indicator', async ({ page }) => {
      await page.goto('http://localhost:6006/?path=/story/pdf-progressmodal--chunking');
      await page.waitForLoadState('networkidle');

      const iframe = page.frameLocator('#storybook-preview-iframe');
      await iframe.locator('button', { hasText: 'Open Modal' }).click();

      // Step 3 (Chunking) should be highlighted with animation
      const currentStep = iframe.locator('[aria-current="step"]');
      await expect(currentStep).toBeVisible();
      await expect(currentStep).toHaveClass(/animate-pulse/);

      // Previous steps should show checkmarks
      const completedSteps = iframe.locator('.bg-green-500.text-white');
      expect(await completedSteps.count()).toBeGreaterThan(0);

      await expect(iframe.locator('[role="dialog"]')).toHaveScreenshot('progress-modal-chunking.png');
    });

    test('should display metrics correctly', async ({ page }) => {
      await page.goto('http://localhost:6006/?path=/story/pdf-progressmodal--extracting');
      await page.waitForLoadState('networkidle');

      const iframe = page.frameLocator('#storybook-preview-iframe');
      await iframe.locator('button', { hasText: 'Open Modal' }).click();

      // Check metrics display
      await expect(iframe.locator('text=Pages Processed')).toBeVisible();
      await expect(iframe.locator('text=Duration')).toBeVisible();
      await expect(iframe.locator('text=ETA')).toBeVisible();

      // Check glassmorphic styling
      const metricsBox = iframe.locator('.backdrop-blur-md').first();
      await expect(metricsBox).toBeVisible();

      await expect(iframe.locator('[role="dialog"]')).toHaveScreenshot('progress-modal-metrics.png');
    });

    test('should show cancel button and confirmation dialog', async ({ page }) => {
      await page.goto('http://localhost:6006/?path=/story/pdf-progressmodal--embedding');
      await page.waitForLoadState('networkidle');

      const iframe = page.frameLocator('#storybook-preview-iframe');
      await iframe.locator('button', { hasText: 'Open Modal' }).click();

      // Click cancel
      await iframe.locator('button', { hasText: 'Cancel Processing' }).click();

      // Confirmation dialog should appear
      await expect(iframe.locator('text=Cancel Processing?')).toBeVisible();
      await expect(iframe.locator('text=/progress will be lost/i')).toBeVisible();

      // Snapshot of confirmation
      await expect(iframe.locator('body')).toHaveScreenshot('progress-modal-cancel-confirm.png');
    });

    test('should show completion state', async ({ page }) => {
      await page.goto('http://localhost:6006/?path=/story/pdf-progressmodal--complete');
      await page.waitForLoadState('networkidle');

      const iframe = page.frameLocator('#storybook-preview-iframe');
      await iframe.locator('button', { hasText: 'Open Modal' }).click();

      // Check completion UI
      await expect(iframe.locator('text=Processing Complete')).toBeVisible();
      await expect(iframe.locator('.text-green-500')).toBeVisible(); // Checkmark

      await expect(iframe.locator('[role="dialog"]')).toHaveScreenshot('progress-modal-complete.png');
    });

    test('should show error state', async ({ page }) => {
      await page.goto('http://localhost:6006/?path=/story/pdf-progressmodal--failed');
      await page.waitForLoadState('networkidle');

      const iframe = page.frameLocator('#storybook-preview-iframe');
      await iframe.locator('button', { hasText: 'Open Modal' }).click();

      // Check error UI
      await expect(iframe.locator('text=Processing Failed')).toBeVisible();
      await expect(iframe.locator('.text-destructive')).toBeVisible();

      await expect(iframe.locator('[role="dialog"]')).toHaveScreenshot('progress-modal-failed.png');
    });

    test('should show polling fallback indicator', async ({ page }) => {
      await page.goto('http://localhost:6006/?path=/story/pdf-progressmodal--pollingfallback');
      await page.waitForLoadState('networkidle');

      const iframe = page.frameLocator('#storybook-preview-iframe');
      await iframe.locator('button', { hasText: 'Open Modal' }).click();

      // Check warning indicator
      await expect(iframe.locator('text=⚠️')).toBeVisible();

      await expect(iframe.locator('[role="dialog"]')).toHaveScreenshot('progress-modal-polling.png');
    });
  });

  test.describe('ProgressCard - Compact Card', () => {
    test.beforeEach(async ({ page }) => {
      await page.goto('http://localhost:6006/?path=/story/pdf-progresscard--processing');
      await page.waitForLoadState('networkidle');
    });

    test('should display circular progress ring', async ({ page }) => {
      const iframe = page.frameLocator('#storybook-preview-iframe');

      // Check progress ring renders
      const progressRing = iframe.locator('svg circle').first();
      await expect(progressRing).toBeVisible();

      // Check percentage text
      await expect(iframe.locator('text=100%')).toBeVisible();

      await expect(iframe.locator('.rounded-lg.border').first()).toHaveScreenshot('progress-card-ring.png');
    });

    test('should show status badge with correct color', async ({ page }) => {
      await page.goto('http://localhost:6006/?path=/story/pdf-progresscard--ready');
      await page.waitForLoadState('networkidle');

      const iframe = page.frameLocator('#storybook-preview-iframe');

      // Ready state should have green text
      const badge = iframe.locator('[class*="text-green"]').first();
      await expect(badge).toBeVisible();

      await expect(iframe.locator('.rounded-lg').first()).toHaveScreenshot('progress-card-ready.png');
    });

    test('should expand and collapse details', async ({ page }) => {
      const iframe = page.frameLocator('#storybook-preview-iframe');

      // Expand
      await iframe.locator('button', { hasText: 'Show Details' }).click();
      await expect(iframe.locator('text=Pages')).toBeVisible();
      await expect(iframe.locator('text=Duration')).toBeVisible();

      // Snapshot expanded
      await expect(iframe.locator('.rounded-lg').first()).toHaveScreenshot('progress-card-expanded.png');

      // Collapse
      await iframe.locator('button', { hasText: 'Collapse' }).click();
      await expect(iframe.locator('text=Pages')).not.toBeVisible();

      // Snapshot collapsed
      await expect(iframe.locator('.rounded-lg').first()).toHaveScreenshot('progress-card-collapsed.png');
    });

    test('should display error message when failed', async ({ page }) => {
      await page.goto('http://localhost:6006/?path=/story/pdf-progresscard--failed');
      await page.waitForLoadState('networkidle');

      const iframe = page.frameLocator('#storybook-preview-iframe');

      // Error state UI
      await expect(iframe.locator('.text-destructive')).toBeVisible();
      await expect(iframe.locator('.bg-destructive\\/10')).toBeVisible();

      await expect(iframe.locator('.rounded-lg').first()).toHaveScreenshot('progress-card-failed.png');
    });

    test('should render multiple cards in list', async ({ page }) => {
      await page.goto('http://localhost:6006/?path=/story/pdf-progresscard--multiplecards');
      await page.waitForLoadState('networkidle');

      const iframe = page.frameLocator('#storybook-preview-iframe');

      const cards = iframe.locator('.rounded-lg.border');
      expect(await cards.count()).toBeGreaterThan(3);

      await expect(iframe.locator('body')).toHaveScreenshot('progress-card-list.png');
    });
  });

  test.describe('ProgressToast - Toast Notifications', () => {
    test.beforeEach(async ({ page }) => {
      await page.goto('http://localhost:6006/?path=/story/pdf-progresstoast--uploading');
      await page.waitForLoadState('networkidle');
    });

    test('should display toast with progress bar', async ({ page }) => {
      const iframe = page.frameLocator('#storybook-preview-iframe');

      const toast = iframe.locator('[role="status"]');
      await expect(toast).toBeVisible();

      // Progress bar inside toast
      const progressBar = iframe.locator('[role="progressbar"]');
      await expect(progressBar).toBeVisible();

      await expect(toast).toHaveScreenshot('progress-toast-default.png');
    });

    test('should show action buttons', async ({ page }) => {
      const iframe = page.frameLocator('#storybook-preview-iframe');

      await expect(iframe.locator('button', { hasText: 'View Details' })).toBeVisible();
      await expect(iframe.locator('button[aria-label="Dismiss notification"]')).toBeVisible();

      await expect(iframe.locator('[role="status"]')).toHaveScreenshot('progress-toast-actions.png');
    });

    test('should display completion state without progress bar', async ({ page }) => {
      await page.goto('http://localhost:6006/?path=/story/pdf-progresstoast--complete');
      await page.waitForLoadState('networkidle');

      const iframe = page.frameLocator('#storybook-preview-iframe');

      await expect(iframe.locator('text=Processing complete')).toBeVisible();
      await expect(iframe.locator('.text-green-500')).toBeVisible(); // Checkmark

      // Progress bar should not be visible for terminal state
      const progressBar = iframe.locator('[role="progressbar"]');
      await expect(progressBar).not.toBeVisible();

      await expect(iframe.locator('[role="status"]')).toHaveScreenshot('progress-toast-complete.png');
    });

    test('should display error state with error message', async ({ page }) => {
      await page.goto('http://localhost:6006/?path=/story/pdf-progresstoast--failed');
      await page.waitForLoadState('networkidle');

      const iframe = page.frameLocator('#storybook-preview-iframe');

      await expect(iframe.locator('text=Processing failed')).toBeVisible();
      await expect(iframe.locator('.bg-destructive\\/10')).toBeVisible();

      await expect(iframe.locator('[role="status"]')).toHaveScreenshot('progress-toast-failed.png');
    });

    test('should have correct positioning (bottom-right, 400px max)', async ({ page }) => {
      const iframe = page.frameLocator('#storybook-preview-iframe');

      const toast = iframe.locator('[role="status"]').first();
      const box = await toast.boundingBox();

      expect(box?.width).toBeLessThanOrEqual(400);

      // Check slide-in animation class
      await expect(toast).toHaveClass(/slide-in-from-right/);
    });
  });

  test.describe('ProgressBadge - Icon Status Indicator', () => {
    test.beforeEach(async ({ page }) => {
      await page.goto('http://localhost:6006/?path=/story/pdf-progressbadge--allstates');
      await page.waitForLoadState('networkidle');
    });

    test('should render all state variants with correct colors', async ({ page }) => {
      const iframe = page.frameLocator('#storybook-preview-iframe');

      // Check all badges render
      const badges = iframe.locator('[role="status"]');
      expect(await badges.count()).toBe(8); // 8 states

      // Visual snapshot
      await expect(iframe.locator('body')).toHaveScreenshot('progress-badge-all-states.png');
    });

    test('should have correct size (24x24px)', async ({ page }) => {
      const iframe = page.frameLocator('#storybook-preview-iframe');

      const badge = iframe.locator('[role="status"]').first();
      const box = await badge.boundingBox();

      expect(box?.width).toBeCloseTo(24, 2);
      expect(box?.height).toBeCloseTo(24, 2);
    });

    test('should show pulse animation for processing states', async ({ page }) => {
      await page.goto('http://localhost:6006/?path=/story/pdf-progressbadge--embedding');
      await page.waitForLoadState('networkidle');

      const iframe = page.frameLocator('#storybook-preview-iframe');

      const badge = iframe.locator('[role="status"]');
      await expect(badge).toHaveClass(/animate-pulse/);

      await expect(badge).toHaveScreenshot('progress-badge-pulse.png');
    });

    test('should render uploading state with amber color', async ({ page }) => {
      await page.goto('http://localhost:6006/?path=/story/pdf-progressbadge--uploading');
      await page.waitForLoadState('networkidle');

      const iframe = page.frameLocator('#storybook-preview-iframe');

      const badge = iframe.locator('[role="status"]');
      await expect(badge).toHaveClass(/text-amber/);

      await expect(badge).toHaveScreenshot('progress-badge-uploading.png');
    });

    test('should render ready state with green color', async ({ page }) => {
      await page.goto('http://localhost:6006/?path=/story/pdf-progressbadge--ready');
      await page.waitForLoadState('networkidle');

      const iframe = page.frameLocator('#storybook-preview-iframe');

      const badge = iframe.locator('[role="status"]');
      await expect(badge).toHaveClass(/text-green/);

      await expect(badge).toHaveScreenshot('progress-badge-ready.png');
    });

    test('should render failed state with red color', async ({ page }) => {
      await page.goto('http://localhost:6006/?path=/story/pdf-progressbadge--failed');
      await page.waitForLoadState('networkidle');

      const iframe = page.frameLocator('#storybook-preview-iframe');

      const badge = iframe.locator('[role="status"]');
      await expect(badge).toHaveClass(/text-destructive/);

      await expect(badge).toHaveScreenshot('progress-badge-failed.png');
    });
  });

  test.describe('Accessibility', () => {
    test('ProgressModal should have ARIA live regions', async ({ page }) => {
      await page.goto('http://localhost:6006/?path=/story/pdf-progressmodal--chunking');
      await page.waitForLoadState('networkidle');

      const iframe = page.frameLocator('#storybook-preview-iframe');
      await iframe.locator('button', { hasText: 'Open Modal' }).click();

      // Check ARIA live region
      const liveRegion = iframe.locator('[aria-live="polite"]');
      await expect(liveRegion).toBeVisible();

      // Check ARIA atomic
      await expect(liveRegion).toHaveAttribute('aria-atomic', 'true');
    });

    test('ProgressModal step indicators should have proper ARIA labels', async ({ page }) => {
      await page.goto('http://localhost:6006/?path=/story/pdf-progressmodal--extracting');
      await page.waitForLoadState('networkidle');

      const iframe = page.frameLocator('#storybook-preview-iframe');
      await iframe.locator('button', { hasText: 'Open Modal' }).click();

      // Each step should have aria-label
      const steps = iframe.locator('[aria-label^="Step"]');
      expect(await steps.count()).toBeGreaterThanOrEqual(6);

      // Current step should have aria-current
      const currentStep = iframe.locator('[aria-current="step"]');
      await expect(currentStep).toBeVisible();
    });

    test('ProgressCard should be keyboard navigable', async ({ page }) => {
      await page.goto('http://localhost:6006/?path=/story/pdf-progresscard--processing');
      await page.waitForLoadState('networkidle');

      const iframe = page.frameLocator('#storybook-preview-iframe');

      // Tab to expand button
      await iframe.locator('button', { hasText: 'Show Details' }).focus();

      // Enter should activate
      await page.keyboard.press('Enter');

      await expect(iframe.locator('text=Pages')).toBeVisible();
    });

    test('ProgressBadge should have descriptive aria-label', async ({ page }) => {
      await page.goto('http://localhost:6006/?path=/story/pdf-progressbadge--chunking');
      await page.waitForLoadState('networkidle');

      const iframe = page.frameLocator('#storybook-preview-iframe');

      const badge = iframe.locator('[role="status"]');
      await expect(badge).toHaveAttribute('aria-label', /PDF status:/);
    });
  });

  test.describe('Responsive Design', () => {
    test('ProgressModal should adapt to mobile viewport', async ({ page }) => {
      await page.setViewportSize({ width: 375, height: 667 });
      await page.goto('http://localhost:6006/?path=/story/pdf-progressmodal--chunking');
      await page.waitForLoadState('networkidle');

      const iframe = page.frameLocator('#storybook-preview-iframe');
      await iframe.locator('button', { hasText: 'Open Modal' }).click();

      // Modal should be responsive
      const modal = iframe.locator('[role="dialog"]');
      await expect(modal).toBeVisible();

      await expect(modal).toHaveScreenshot('progress-modal-mobile.png');
    });

    test('ProgressCard should stack on mobile', async ({ page }) => {
      await page.setViewportSize({ width: 375, height: 667 });
      await page.goto('http://localhost:6006/?path=/story/pdf-progresscard--processing');
      await page.waitForLoadState('networkidle');

      const iframe = page.frameLocator('#storybook-preview-iframe');

      const card = iframe.locator('.rounded-lg').first();
      const box = await card.boundingBox();

      // Should fit mobile viewport
      expect(box?.width).toBeLessThanOrEqual(375);

      await expect(card).toHaveScreenshot('progress-card-mobile.png');
    });
  });

  test.describe('Dark Mode', () => {
    test('ProgressModal should render correctly in dark mode', async ({ page }) => {
      await page.goto('http://localhost:6006/?path=/story/pdf-progressmodal--chunking&globals=backgrounds.value:dark');
      await page.waitForLoadState('networkidle');

      const iframe = page.frameLocator('#storybook-preview-iframe');

      // Add dark class to trigger dark mode
      await iframe.locator('body').evaluate((el) => el.classList.add('dark'));

      await iframe.locator('button', { hasText: 'Open Modal' }).click();

      await expect(iframe.locator('[role="dialog"]')).toHaveScreenshot('progress-modal-dark.png');
    });

    test('ProgressCard should have proper dark mode colors', async ({ page }) => {
      await page.goto('http://localhost:6006/?path=/story/pdf-progresscard--processing');
      await page.waitForLoadState('networkidle');

      const iframe = page.frameLocator('#storybook-preview-iframe');
      await iframe.locator('body').evaluate((el) => el.classList.add('dark'));

      const card = iframe.locator('.rounded-lg').first();
      await expect(card).toHaveClass(/dark:bg-white\\/5/);

      await expect(card).toHaveScreenshot('progress-card-dark.png');
    });
  });

  test.describe('Animation Performance', () => {
    test('ProgressModal step transitions should be smooth', async ({ page }) => {
      await page.goto('http://localhost:6006/?path=/story/pdf-progressmodal--indexing');
      await page.waitForLoadState('networkidle');

      const iframe = page.frameLocator('#storybook-preview-iframe');
      await iframe.locator('button', { hasText: 'Open Modal' }).click();

      // Check animation duration class
      const stepIndicators = iframe.locator('.transition-all.duration-300');
      expect(await stepIndicators.count()).toBeGreaterThan(5);
    });

    test('Progress bar should have smooth transitions', async ({ page }) => {
      await page.goto('http://localhost:6006/?path=/story/pdf-progressmodal--embedding');
      await page.waitForLoadState('networkidle');

      const iframe = page.frameLocator('#storybook-preview-iframe');
      await iframe.locator('button', { hasText: 'Open Modal' }).click();

      const progressIndicator = iframe.locator('.transition-all').first();
      await expect(progressIndicator).toBeVisible();
    });
  });

  test.describe('Integration Scenarios', () => {
    test('Multiple components should update together', async ({ page }) => {
      // This would require a real integration page, placeholder for now
      // Verifies that Modal, Card, Toast, Badge all update from same usePdfProgress hook
      test.skip();
    });

    test('SSE connection status should be visible', async ({ page }) => {
      await page.goto('http://localhost:6006/?path=/story/pdf-progressmodal--pollingfallback');
      await page.waitForLoadState('networkidle');

      const iframe = page.frameLocator('#storybook-preview-iframe');
      await iframe.locator('button', { hasText: 'Open Modal' }).click();

      // Polling fallback indicator
      await expect(iframe.locator('text=⚠️')).toBeVisible();
      await expect(iframe.locator('text=/Checking status/i')).toBeVisible();
    });
  });
});

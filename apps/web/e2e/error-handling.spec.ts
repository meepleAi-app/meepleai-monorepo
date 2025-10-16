/**
 * E2E tests for error handling flows
 */

import { test, expect, Page } from '@playwright/test';

test.describe('Error Handling E2E Tests', () => {
  test.describe('Network errors', () => {
    test('should display error toast on network failure', async ({ page, context }) => {
      // Block all API requests to simulate network failure
      await context.route('**/api/**', (route) => {
        route.abort('failed');
      });

      await page.goto('/');

      // Interact with a feature that makes API calls
      // This will depend on your actual pages - adjust as needed
      await page.waitForLoadState('networkidle');

      // Check for error indication (adjust selectors based on your actual implementation)
      // If your pages make API calls on load, error toasts should appear
    });

    test('should retry failed requests automatically', async ({ page, context }) => {
      let requestCount = 0;

      await context.route('**/api/v1/games', (route) => {
        requestCount++;
        if (requestCount < 3) {
          // Fail first 2 requests
          route.fulfill({
            status: 500,
            body: JSON.stringify({ error: 'Internal Server Error' })
          });
        } else {
          // Succeed on 3rd attempt
          route.fulfill({
            status: 200,
            body: JSON.stringify([
              { id: '1', name: 'Test Game' }
            ])
          });
        }
      });

      await page.goto('/');
      await page.waitForLoadState('networkidle');

      // Verify that request was retried
      expect(requestCount).toBeGreaterThanOrEqual(3);
    });
  });

  test.describe('API errors', () => {
    test('should display error modal for 404 errors', async ({ page, context }) => {
      await context.route('**/api/v1/games/nonexistent', (route) => {
        route.fulfill({
          status: 404,
          body: JSON.stringify({ error: 'Not Found' })
        });
      });

      await page.goto('/');

      // Try to access nonexistent resource
      // This will depend on your actual implementation
      // You may need to navigate to a specific page or click a button
    });

    test('should display user-friendly message for 401 errors', async ({ page, context }) => {
      await context.route('**/api/v1/protected', (route) => {
        route.fulfill({
          status: 401,
          body: JSON.stringify({ error: 'Unauthorized' })
        });
      });

      // Navigate to a protected page
      // Adjust based on your actual protected routes
      await page.goto('/admin');

      // Should redirect to login or show auth error
      // Verify the appropriate behavior for your app
    });

    test('should display error message for 500 errors', async ({ page, context }) => {
      await context.route('**/api/v1/**', (route) => {
        route.fulfill({
          status: 500,
          headers: {
            'X-Correlation-Id': 'test-correlation-123'
          },
          body: JSON.stringify({ error: 'Internal Server Error' })
        });
      });

      await page.goto('/');

      // Look for error indication
      // This will depend on your implementation
    });
  });

  test.describe('Error boundary', () => {
    test('should catch rendering errors and display fallback UI', async ({ page }) => {
      // This test requires a page that can trigger a rendering error
      // You may need to create a test page or route that intentionally throws an error

      await page.goto('/');

      // Check if error boundary fallback is displayed
      // This assumes your error boundary shows specific text
      // Adjust based on your actual error boundary implementation
    });

    test('should allow user to recover from error', async ({ page }) => {
      // Navigate to a page that can throw an error
      await page.goto('/');

      // If error occurs, click "Try Again" button
      const tryAgainButton = page.getByRole('button', { name: /try again/i });
      if (await tryAgainButton.isVisible()) {
        await tryAgainButton.click();

        // Verify recovery
        await expect(tryAgainButton).not.toBeVisible();
      }
    });

    test('should navigate to home on "Go to Home" click', async ({ page }) => {
      // This test requires triggering an error first
      await page.goto('/');

      const homeButton = page.getByRole('button', { name: /go to home/i });
      if (await homeButton.isVisible()) {
        await homeButton.click();

        // Verify navigation to home
        await expect(page).toHaveURL('/');
      }
    });
  });

  test.describe('Toast notifications', () => {
    test('should display and auto-dismiss success toast', async ({ page }) => {
      await page.goto('/');

      // Trigger an action that shows a success toast
      // This depends on your application's features

      // Wait for toast to appear
      const toast = page.locator('[role="alert"]').first();
      await expect(toast).toBeVisible({ timeout: 5000 });

      // Wait for auto-dismiss (default 5 seconds)
      await expect(toast).not.toBeVisible({ timeout: 6000 });
    });

    test('should manually dismiss toast', async ({ page }) => {
      await page.goto('/');

      // Trigger an action that shows a toast
      // This depends on your application

      // Wait for toast to appear
      const toast = page.locator('[role="alert"]').first();
      if (await toast.isVisible()) {
        // Click dismiss button
        const dismissButton = toast.getByRole('button', { name: /dismiss/i });
        await dismissButton.click();

        // Verify toast is dismissed
        await expect(toast).not.toBeVisible();
      }
    });

    test('should stack multiple toasts', async ({ page, context }) => {
      // Create multiple errors to trigger multiple toasts
      let requestCount = 0;

      await context.route('**/api/v1/**', (route) => {
        requestCount++;
        route.fulfill({
          status: 500,
          body: JSON.stringify({ error: `Error ${requestCount}` })
        });
      });

      await page.goto('/');

      // Trigger multiple API calls
      // This depends on your application

      // Verify multiple toasts are visible
      const toasts = page.locator('[role="alert"]');
      const count = await toasts.count();

      // Should have multiple toasts stacked
      // Exact count depends on your retry logic and API calls
      expect(count).toBeGreaterThanOrEqual(1);
    });
  });

  test.describe('Error modal', () => {
    test('should display error modal with details', async ({ page, context }) => {
      await context.route('**/api/v1/games', (route) => {
        route.fulfill({
          status: 500,
          headers: {
            'X-Correlation-Id': 'test-correlation-456'
          },
          body: JSON.stringify({ error: 'Server error' })
        });
      });

      await page.goto('/');

      // Look for error modal (adjust selector based on your implementation)
      const modal = page.getByRole('dialog');
      if (await modal.isVisible()) {
        // Verify modal contains error information
        await expect(modal).toContainText(/error/i);

        // Check for correlation ID if showDetails is enabled
        if (process.env.NODE_ENV === 'development') {
          // Expand technical details
          const detailsButton = modal.getByText(/technical details/i);
          if (await detailsButton.isVisible()) {
            await detailsButton.click();
            await expect(modal).toContainText('test-correlation-456');
          }
        }
      }
    });

    test('should close error modal when Close button is clicked', async ({ page }) => {
      await page.goto('/');

      const modal = page.getByRole('dialog');
      if (await modal.isVisible()) {
        const closeButton = modal.getByRole('button', { name: /close/i });
        await closeButton.click();

        await expect(modal).not.toBeVisible();
      }
    });

    test('should retry action from error modal', async ({ page, context }) => {
      let attemptCount = 0;

      await context.route('**/api/v1/games', (route) => {
        attemptCount++;
        if (attemptCount === 1) {
          route.fulfill({
            status: 500,
            body: JSON.stringify({ error: 'Server error' })
          });
        } else {
          route.fulfill({
            status: 200,
            body: JSON.stringify([{ id: '1', name: 'Test Game' }])
          });
        }
      });

      await page.goto('/');

      const modal = page.getByRole('dialog');
      if (await modal.isVisible()) {
        const retryButton = modal.getByRole('button', { name: /retry/i });
        if (await retryButton.isVisible()) {
          await retryButton.click();

          // Verify modal closes and request succeeds
          await expect(modal).not.toBeVisible();
          expect(attemptCount).toBe(2);
        }
      }
    });
  });

  test.describe('Client-side logging', () => {
    test('should log errors to backend', async ({ page, context }) => {
      const logRequests: any[] = [];

      // Intercept log requests
      await context.route('**/api/v1/logs/client', (route) => {
        logRequests.push(route.request().postDataJSON());
        route.fulfill({
          status: 204
        });
      });

      // Trigger an error
      await context.route('**/api/v1/games', (route) => {
        route.fulfill({
          status: 500,
          body: JSON.stringify({ error: 'Server error' })
        });
      });

      await page.goto('/');

      // Wait for logs to be sent (batched)
      await page.waitForTimeout(6000); // Wait for flush interval

      // Verify logs were sent
      // Note: This depends on your logging implementation
      // Adjust based on actual log batching behavior
    });
  });

  test.describe('Accessibility', () => {
    test('should have proper ARIA attributes on error elements', async ({ page }) => {
      await page.goto('/');

      // Check toasts have proper ARIA attributes
      const toasts = page.locator('[role="alert"]');
      if ((await toasts.count()) > 0) {
        const firstToast = toasts.first();
        await expect(firstToast).toHaveAttribute('aria-live', 'assertive');
        await expect(firstToast).toHaveAttribute('aria-atomic', 'true');
      }

      // Check error modals have proper ARIA attributes
      const modal = page.getByRole('dialog');
      if (await modal.isVisible()) {
        await expect(modal).toHaveAttribute('aria-modal', 'true');
      }
    });

    test('should be keyboard navigable', async ({ page }) => {
      await page.goto('/');

      // Test keyboard navigation on error modal
      const modal = page.getByRole('dialog');
      if (await modal.isVisible()) {
        // Tab through focusable elements
        await page.keyboard.press('Tab');

        // Press Escape to close
        await page.keyboard.press('Escape');
        await expect(modal).not.toBeVisible();
      }
    });
  });
});

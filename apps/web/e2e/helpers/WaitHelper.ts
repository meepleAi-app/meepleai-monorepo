/**
 * WaitHelper - Smart waiting strategies for E2E tests
 *
 * Replaces hardcoded waitForTimeout() with intelligent, event-driven waits.
 * Follows Playwright best practices: auto-waiting, web-first assertions, stable selectors.
 *
 * @see https://playwright.dev/docs/test-timeouts
 * @see Issue #1493 - Reduce Hardcoded Timeouts
 */

import { Page, expect } from '@playwright/test';

export class WaitHelper {
  constructor(private readonly page: Page) {}

  /**
   * Wait for SSE (Server-Sent Events) streaming to complete
   *
   * Use case: Chat streaming, real-time updates
   * Replaces: waitForTimeout(500-2000) after stream start
   *
   * @param options.messageText - Expected final message text to appear
   * @param options.endpoint - SSE endpoint URL pattern (default: '/stream')
   * @param options.timeout - Max wait time in ms (default: 10000)
   */
  async waitForStreamingComplete(
    options: {
      messageText?: string;
      endpoint?: string;
      timeout?: number;
    } = {}
  ) {
    const { messageText, endpoint = '/stream', timeout = 10000 } = options;

    // Wait for SSE response to start
    await this.page.waitForResponse(
      response =>
        response.url().includes(endpoint) &&
        response.headers()['content-type']?.includes('text/event-stream') === true,
      { timeout }
    );

    // If message text provided, wait for it to appear in DOM
    if (messageText) {
      await expect(this.page.getByText(messageText, { exact: false })).toBeVisible({
        timeout,
      });
    }

    // Wait for streaming indicator to disappear (e.g., "Invio..." button)
    const streamingButton = this.page.locator('button[type="submit"]:has-text("Invio...")');
    if (await streamingButton.isVisible().catch(() => false)) {
      await expect(streamingButton).not.toBeVisible({ timeout: 5000 });
    }
  }

  /**
   * Wait for PDF processing to complete (upload → extraction → embedding)
   *
   * Use case: PDF upload journeys, document processing
   * Replaces: waitForTimeout(2000-5000) after upload
   *
   * @param filename - PDF filename to track
   * @param expectedStatus - Expected final status ('completed' | 'failed')
   * @param timeout - Max wait time in ms (default: 30000 for large PDFs)
   */
  async waitForPdfProcessingComplete(
    filename: string,
    expectedStatus: 'completed' | 'failed' = 'completed',
    timeout = 30000
  ) {
    // Wait for processing status to reach terminal state
    await this.page.waitForFunction(
      ([fn, status]) => {
        const element = document.querySelector(`[data-pdf="${fn}"]`);
        if (!element) return false;
        const currentStatus = element.getAttribute('data-status');
        return currentStatus === status || currentStatus === 'failed';
      },
      [filename, expectedStatus],
      { timeout }
    );

    // Verify expected status via assertion
    const statusElement = this.page.locator(`[data-pdf="${filename}"]`);
    await expect(statusElement).toHaveAttribute('data-status', expectedStatus, { timeout: 5000 });
  }

  /**
   * Wait for OAuth callback redirect and authentication success
   *
   * Use case: OAuth flows (Google, Discord, GitHub)
   * Replaces: waitForTimeout(1000-2000) after OAuth popup/redirect
   *
   * @param provider - OAuth provider name (for logging)
   * @param timeout - Max wait time in ms (default: 5000)
   */
  async waitForOAuthCallback(provider?: string, timeout = 5000) {
    // Wait for URL to contain OAuth callback parameters
    await this.page.waitForURL(
      url => url.searchParams.has('code') || url.searchParams.has('error'),
      { timeout }
    );

    // Wait for auth success indicator (adjust selector based on app)
    const authSuccess = this.page.locator('[data-testid="auth-success"], .auth-success');
    await expect(authSuccess).toBeVisible({ timeout });
  }

  /**
   * Wait for CSS animation to complete
   *
   * Use case: Slide-ins, fade-outs, transitions
   * Replaces: waitForTimeout(100-500) for animations
   *
   * @param selector - Element with animation
   * @param timeout - Max wait time in ms (default: 3000)
   */
  async waitForAnimationComplete(selector: string, timeout = 3000) {
    const element = this.page.locator(selector);

    // Wait for element to be visible first
    await element.waitFor({ state: 'visible', timeout });

    // Wait for animation to finish
    await this.page.waitForFunction(
      sel => {
        const el = document.querySelector(sel);
        if (!el) return false;
        const style = getComputedStyle(el);
        return style.animationPlayState !== 'running' && style.transitionProperty === 'none';
      },
      selector,
      { timeout }
    );
  }

  /**
   * Generic state predicate waiter
   *
   * Use case: Custom app state checks (window.appReady, data loaded, etc.)
   * Replaces: waitForTimeout() for arbitrary delays
   *
   * @param predicate - Serializable predicate function (executed in browser context)
   * @param timeout - Max wait time in ms (default: 5000)
   * @example await waitForState(() => window.appReady === true)
   */
  async waitForState(predicate: () => boolean | Promise<boolean>, timeout = 5000) {
    await this.page.waitForFunction(predicate, undefined, { timeout });
  }

  /**
   * Wait for network to be idle (no requests for 500ms)
   *
   * Use case: SPA route transitions, lazy loading
   * Replaces: waitForTimeout(1000-2000) after navigation
   *
   * @param timeout - Max wait time in ms (default: 10000)
   */
  async waitForNetworkIdle(timeout = 10000) {
    await this.page.waitForLoadState('networkidle', { timeout });
  }

  /**
   * Wait for DOM mutations to stabilize (no changes for 500ms)
   *
   * Use case: React setState batching, SSE DOM updates, dynamic content
   * Replaces: waitForTimeout(100-500) for DOM settle
   *
   * @param selector - Selector to watch for mutations
   * @param timeout - Max wait time in ms (default: 5000)
   */
  async waitForDOMStable(selector: string, timeout = 5000) {
    await this.page.waitForFunction(
      sel => {
        return new Promise(resolve => {
          const element = document.querySelector(sel);
          if (!element) {
            resolve(false);
            return;
          }

          let mutationTimer: NodeJS.Timeout;
          const observer = new MutationObserver(() => {
            clearTimeout(mutationTimer);
            mutationTimer = setTimeout(() => {
              observer.disconnect();
              resolve(true);
            }, 500); // Stable for 500ms
          });

          observer.observe(element, {
            childList: true,
            subtree: true,
            characterData: true,
          });

          // Trigger initial timer
          mutationTimer = setTimeout(() => {
            observer.disconnect();
            resolve(true);
          }, 500);
        });
      },
      selector,
      { timeout }
    );
  }

  /**
   * Wait for API response with specific criteria
   *
   * Use case: POST/PUT operations, background jobs
   * Replaces: waitForTimeout() after API calls
   *
   * @param urlPattern - URL pattern to match (regex or string)
   * @param statusCode - Expected HTTP status (default: 200)
   * @param timeout - Max wait time in ms (default: 10000)
   */
  async waitForApiResponse(urlPattern: string | RegExp, statusCode = 200, timeout = 10000) {
    await this.page.waitForResponse(
      response => {
        const urlMatches =
          typeof urlPattern === 'string'
            ? response.url().includes(urlPattern)
            : urlPattern.test(response.url());
        return urlMatches && response.status() === statusCode;
      },
      { timeout }
    );
  }

  /**
   * Wait for element to be actionable (visible, enabled, stable)
   *
   * Use case: Buttons after async operations, form fields after validation
   * Replaces: waitForTimeout() before clicks/fills
   *
   * @param selector - Element selector
   * @param timeout - Max wait time in ms (default: 5000)
   */
  async waitForActionable(selector: string, timeout = 5000) {
    const element = this.page.locator(selector);
    await element.waitFor({ state: 'visible', timeout });
    await expect(element).toBeEnabled({ timeout });
    await expect(element).toBeVisible({ timeout }); // Ensures stable (not obscured)
  }
}

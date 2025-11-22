import { Page, Locator, expect } from '@playwright/test';
import { IBasePage, WaitOptions, ClickOptions } from '../../types/pom-interfaces';

/**
 * BasePage - Foundation for all page objects
 *
 * Provides common utilities for page interactions:
 * - Navigation and URL management
 * - Element waiting strategies
 * - Basic interactions (click, fill, select)
 * - Screenshot capture for debugging
 *
 * Usage:
 *   export class MyPage extends BasePage {
 *     async goto() { await this.page.goto('/my-page'); }
 *   }
 */
export abstract class BasePage implements IBasePage {
  constructor(public readonly page: Page) {}

  /**
   * Navigate to page URL (must be implemented by subclass)
   * @example
   * async goto() {
   *   await this.page.goto('/chat');
   *   await this.waitForLoad();
   * }
   */
  abstract goto(): Promise<void>;

  /**
   * Wait for page to be fully loaded (networkidle state)
   */
  async waitForLoad(): Promise<void> {
    await this.page.waitForLoadState('networkidle');
  }

  /**
   * Wait for element to be visible with configurable timeout
   * @param locator - Playwright locator
   * @param options - Wait options (timeout, state)
   */
  async waitForElement(locator: Locator, options: WaitOptions = {}): Promise<void> {
    await expect(locator).toBeVisible({
      timeout: options.timeout || 10000,
    });
  }

  /**
   * Wait for element to disappear (useful for loading spinners, modals)
   * @param locator - Playwright locator
   * @param options - Wait options (timeout)
   */
  async waitForElementToDisappear(locator: Locator, options: WaitOptions = {}): Promise<void> {
    await expect(locator).not.toBeVisible({
      timeout: options.timeout || 10000,
    });
  }

  /**
   * Wait for URL to match pattern (for navigation assertions)
   * @param pattern - String or RegExp to match URL
   */
  async waitForUrl(pattern: string | RegExp): Promise<void> {
    await this.page.waitForURL(pattern, { timeout: 10000 });
  }

  /**
   * Take screenshot for debugging (saved to e2e-screenshots/)
   * @param name - Screenshot filename (without extension)
   */
  async screenshot(name: string): Promise<void> {
    await this.page.screenshot({
      path: `e2e-screenshots/${name}.png`,
      fullPage: true,
    });
  }

  /**
   * Get current page URL
   */
  getUrl(): string {
    return this.page.url();
  }

  /**
   * Check if element is currently visible
   * @param locator - Playwright locator
   */
  async isVisible(locator: Locator): Promise<boolean> {
    try {
      return await locator.isVisible({ timeout: 1000 });
    } catch {
      return false;
    }
  }

  /**
   * Fill input field (with auto-waiting)
   * @param locator - Input locator
   * @param value - Text to fill
   */
  async fill(locator: Locator, value: string): Promise<void> {
    await locator.fill(value);
  }

  /**
   * Click element (with auto-waiting)
   * @param locator - Element locator
   * @param options - Click options (force, timeout)
   */
  async click(locator: Locator, options: ClickOptions = {}): Promise<void> {
    await locator.click(options);
  }

  /**
   * Select option from dropdown
   * @param locator - Select locator
   * @param value - Option value
   */
  async selectOption(locator: Locator, value: string): Promise<void> {
    await locator.selectOption(value);
  }

  /**
   * Upload file to file input
   * @param locator - File input locator
   * @param filePath - Path to file
   */
  async uploadFile(locator: Locator, filePath: string): Promise<void> {
    await locator.setInputFiles(filePath);
  }
}

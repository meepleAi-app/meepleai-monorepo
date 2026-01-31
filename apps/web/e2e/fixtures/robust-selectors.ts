/**
 * Robust Selector Helpers for E2E Tests
 *
 * Priority-based selector strategy for stable, maintainable tests:
 * 1. data-testid (highest priority - immune to text/i18n changes)
 * 2. getByRole (semantic ARIA - accessible and stable)
 * 3. getByText (lowest priority - fragile to copywriting changes)
 *
 * Issue #2542: E2E Test Suite Infrastructure Improvements
 */

import type { Page, Locator } from '@playwright/test';

/**
 * Get element by data-testid with optional text filter
 * @param page Playwright page object
 * @param testId data-testid attribute value
 * @param textFilter Optional regex to filter by text content
 * @returns Locator for the element
 *
 * @example
 * // Basic usage
 * await getByDataTestId(page, 'stat-card').first()
 *
 * // With text filter (multi-language support)
 * await getByDataTestId(page, 'stat-card-label', /users|utenti/i)
 */
export function getByDataTestId(page: Page, testId: string, textFilter?: RegExp): Locator {
  const locator = page.getByTestId(testId);
  return textFilter ? locator.filter({ hasText: textFilter }) : locator;
}

/**
 * Get button by role with fallback to data-testid
 * @param page Playwright page object
 * @param name Button name (supports i18n regex)
 * @param testId Optional data-testid fallback
 * @returns Locator for the button
 *
 * @example
 * // Semantic role selector
 * await getButton(page, /export|esporta/i)
 *
 * // With data-testid fallback
 * await getButton(page, /export|esporta/i, 'export-button-trigger')
 */
export function getButton(page: Page, name: string | RegExp, testId?: string): Locator {
  if (testId) {
    // Try data-testid first
    const byTestId = page.getByTestId(testId);
    if (byTestId) return byTestId;
  }
  // Fallback to role
  return page.getByRole('button', { name });
}

/**
 * Get navigation link by role with fallback to data-testid
 * @param page Playwright page object
 * @param name Link name (supports i18n regex)
 * @param testId Optional data-testid fallback
 * @returns Locator for the link
 */
export function getNavLink(page: Page, name: string | RegExp, testId?: string): Locator {
  if (testId) {
    const byTestId = page.getByTestId(testId);
    if (byTestId) return byTestId;
  }
  return page.getByRole('link', { name });
}

/**
 * Get form input by label with fallback to data-testid
 * @param page Playwright page object
 * @param label Input label (supports i18n regex)
 * @param testId Optional data-testid fallback
 * @returns Locator for the input
 */
export function getFormInput(page: Page, label: string | RegExp, testId?: string): Locator {
  if (testId) {
    const byTestId = page.getByTestId(testId);
    if (byTestId) return byTestId;
  }
  return page.getByLabel(label);
}

/**
 * Wait for analytics metrics to load
 * Robust helper for admin analytics pages
 * @param page Playwright page object
 * @param minMetrics Minimum expected metric cards (default: 8)
 * @param timeout Maximum wait time in ms (default: 15000)
 */
export async function waitForMetricsGrid(
  page: Page,
  minMetrics = 8,
  timeout = 15000
): Promise<void> {
  await page.getByTestId('metrics-grid').waitFor({ state: 'visible', timeout });
  const cards = page.getByTestId('stat-card');
  await cards.first().waitFor({ state: 'visible', timeout });

  // Verify minimum count
  const count = await cards.count();
  if (count < minMetrics) {
    throw new Error(`Expected at least ${minMetrics} metric cards, found ${count}`);
  }
}

/**
 * Wait for admin page header to load
 * @param page Playwright page object
 */
export async function waitForAdminHeader(page: Page): Promise<void> {
  await page.getByTestId('admin-header').waitFor({ state: 'visible' });
}

/**
 * Wait for admin sidebar to load
 * @param page Playwright page object
 */
export async function waitForAdminSidebar(page: Page): Promise<void> {
  // Desktop or mobile sidebar
  const sidebar =
    page.getByTestId('admin-sidebar-desktop').or(page.getByTestId('admin-sidebar-mobile-trigger'));
  await sidebar.first().waitFor({ state: 'visible' });
}

/**
 * Click admin navigation link by path segment
 * @param page Playwright page object
 * @param pathSegment Path segment (e.g., 'analytics', 'users', 'api-keys')
 */
export async function clickAdminNavLink(page: Page, pathSegment: string): Promise<void> {
  await page.getByTestId(`admin-nav-link-${pathSegment}`).click();
}

/**
 * Submit login form with robust selectors
 * @param page Playwright page object
 * @param email User email
 * @param password User password
 */
export async function submitLogin(page: Page, email: string, password: string): Promise<void> {
  await page.getByTestId('login-email').fill(email);
  await page.getByTestId('login-password').fill(password);
  await page.getByTestId('login-submit').click();
}

/**
 * Submit registration form with robust selectors
 * @param page Playwright page object
 * @param data Registration data
 */
export async function submitRegistration(
  page: Page,
  data: {
    email: string;
    password: string;
    confirmPassword: string;
    displayName?: string;
    role?: string;
  }
): Promise<void> {
  await page.getByTestId('register-email').fill(data.email);
  if (data.displayName) {
    await page.getByTestId('register-display-name').fill(data.displayName);
  }
  await page.getByTestId('register-password').fill(data.password);
  await page.getByTestId('register-confirm-password').fill(data.confirmPassword);
  if (data.role) {
    await page.getByTestId('register-role-select').click();
    await page.getByRole('option', { name: data.role }).click();
  }
  await page.getByTestId('register-submit').click();
}

/**
 * Send chat message with robust selectors
 * @param page Playwright page object
 * @param message Message text to send
 */
export async function sendChatMessage(page: Page, message: string): Promise<void> {
  await page.getByTestId('message-input').fill(message);
  await page.getByTestId('send-message-button').click();
}

/**
 * Stop streaming response with robust selector
 * @param page Playwright page object
 */
export async function stopStreaming(page: Page): Promise<void> {
  await page.getByTestId('stop-streaming-button').click();
}

/**
 * Get export button with robust selector
 * @param page Playwright page object
 * @returns Locator for export button
 */
export function getExportButton(page: Page): Locator {
  return page.getByTestId('export-button-trigger');
}

/**
 * Export dashboard as CSV
 * @param page Playwright page object
 */
export async function exportAsCSV(page: Page): Promise<void> {
  await page.getByTestId('export-button-trigger').click();
  await page.getByTestId('export-button-csv').click();
}

/**
 * Export dashboard as PDF
 * @param page Playwright page object
 */
export async function exportAsPDF(page: Page): Promise<void> {
  await page.getByTestId('export-button-trigger').click();
  await page.getByTestId('export-button-pdf').click();
}

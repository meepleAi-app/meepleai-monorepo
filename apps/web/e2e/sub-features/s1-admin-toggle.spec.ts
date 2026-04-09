/**
 * S1 — Admin↔User view mode toggle E2E smoke tests.
 *
 * Validates the toggle is rendered for admin users, clicking it flips the
 * view mode cookie, and the redirect happens as expected.
 *
 * Related spec: docs/superpowers/specs/2026-04-09-library-to-game-epic-design.md §4.1
 */
import { test, expect } from '@playwright/test';

import { loginAsAdmin } from '../fixtures/auth';

test.describe('S1 · Admin↔User view mode toggle', () => {
  test.beforeEach(async ({ context }) => {
    // Ensure no stale view mode cookie from previous tests
    const existing = await context.cookies();
    const keep = existing.filter(c => c.name !== 'meepleai_view_mode');
    await context.clearCookies();
    if (keep.length > 0) {
      await context.addCookies(keep);
    }
  });

  test('toggle is visible for admin users on user home', async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto('/');

    const toggle = page.getByTestId('view-mode-toggle');
    await expect(toggle).toBeVisible();
    await expect(toggle).toHaveAttribute('role', 'switch');
    // On '/' with no cookie, default mode is 'user'
    await expect(toggle).toHaveAttribute('aria-checked', 'false');
  });

  test('toggle is visible on admin dashboard', async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto('/admin/overview');

    const toggle = page.getByTestId('view-mode-toggle');
    await expect(toggle).toBeVisible();
    await expect(toggle).toHaveAttribute('role', 'switch');
    // On /admin with no cookie, default mode is 'admin'
    await expect(toggle).toHaveAttribute('aria-checked', 'true');
  });

  test('clicking toggle from admin redirects to user shell', async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto('/admin/overview');

    const toggle = page.getByTestId('view-mode-toggle');
    await toggle.click();

    // Should navigate away from /admin/*
    await page.waitForURL(url => !url.pathname.startsWith('/admin'), { timeout: 5000 });
    expect(page.url()).not.toContain('/admin');
  });

  test('cookie is set after clicking toggle', async ({ page, context }) => {
    await loginAsAdmin(page);
    await page.goto('/admin/overview');

    await page.getByTestId('view-mode-toggle').click();
    await page.waitForLoadState('networkidle');

    const cookies = await context.cookies();
    const viewModeCookie = cookies.find(c => c.name === 'meepleai_view_mode');
    expect(viewModeCookie?.value).toBe('user');
  });

  test('SSR redirects admin layout to / when cookie is user (no flash)', async ({
    page,
    context,
    baseURL,
  }) => {
    await loginAsAdmin(page);
    // Navigate to establish a real document origin before adding cookies.
    const origin = baseURL || 'http://localhost:3000';
    await page.goto('/');

    // Pre-set the cookie with an absolute URL
    await context.addCookies([
      {
        name: 'meepleai_view_mode',
        value: 'user',
        url: origin,
        sameSite: 'Lax',
      },
    ]);

    await page.goto('/admin/overview');
    // Server-side redirect should send user to '/' before admin shell renders
    expect(page.url()).not.toContain('/admin/overview');
  });

  test('toggle returns to admin when clicked from user shell', async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto('/');

    const toggle = page.getByTestId('view-mode-toggle');
    await expect(toggle).toBeVisible();
    await expect(toggle).toHaveAttribute('aria-checked', 'false');

    await toggle.click();
    await page.waitForURL(url => url.pathname.startsWith('/admin'), { timeout: 5000 });
    expect(page.url()).toContain('/admin');
  });
});

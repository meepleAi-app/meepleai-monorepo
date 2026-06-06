/**
 * Admin Providers — Rotate Key flow E2E (#1859 Phase 12)
 *
 * Happy-path coverage:
 * - Superadmin navigates to /admin/providers
 * - Clicks "Rotate" on the DeepSeek row
 * - Fills typed-confirm + new API key, submits
 * - First attempt → 401 + step_up_required → StepUpTwoFactorModal opens
 * - Enters 6-digit TOTP code → step-up succeeds → rotation auto-retries
 * - Second attempt → 200, fingerprint visible in success card
 *
 * **Mocking strategy** (Option A from #1859 Phase 12):
 *   The BE rotate-key endpoint live-probes the provider before persistence.
 *   In E2E we never want to hit real DeepSeek / OpenRouter — instead we mock
 *   POST /admin/providers/{name}/rotate-key + POST /auth/2fa/step-up at the
 *   route layer. Tests assert the FE wires the modal, retries, and surfaces
 *   the fingerprint correctly.
 *
 * Auth: `AuthHelper.mockAuthenticatedSession({...role: 'SuperAdmin'})` so the
 * trigger button is enabled (RotateKeyModal gates on `isSuperAdmin`).
 */

import { test, expect } from './fixtures';
import { AdminHelper, AuthHelper, USER_FIXTURES } from './pages';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

test.describe('Admin Providers — Rotate Key (#1859)', () => {
  test.beforeEach(async ({ page }) => {
    const authHelper = new AuthHelper(page);
    const adminHelper = new AdminHelper(page);
    await page.emulateMedia({ reducedMotion: 'reduce' });

    // Bootstrap auth as Admin (UserFixture type does not yet include SuperAdmin)
    // then override /auth/me below to upgrade to SuperAdmin so the RotateKeyModal
    // trigger button renders enabled.
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.admin);

    // Override /auth/me with a SuperAdmin user — this MUST come AFTER
    // `mockAuthenticatedSession` so the later route registration wins.
    await page.route(`${API_BASE}/api/v1/auth/me`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          user: {
            id: 'superadmin-test-1',
            email: 'superadmin@meepleai.dev',
            displayName: 'SuperAdmin User',
            role: 'SuperAdmin',
          },
          expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
        }),
      });
    });

    // Catch-all for admin endpoints (prevents unmocked route errors).
    // The specific rotate-key route below is registered AFTER this and wins.
    await adminHelper.setupAdminAuth(true);

    // Mock the providers landing page quota / probe endpoints so the table
    // renders without hitting real upstreams. The exact data does not matter;
    // we only need the row + Rotate button to be present.
    await page.route(/\/api\/v1\/admin\/providers\/[^/]+\/quota$/, async route => {
      const url = new URL(route.request().url());
      const name = url.pathname.split('/').reverse()[1];
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          providerName: name,
          quotaSupported: false,
          tokenConfigured: true,
          usedUsd: null,
          limitUsd: null,
          remainingUsd: null,
          resetAt: null,
          errorCode: null,
          errorMessage: null,
          fetchedAt: new Date().toISOString(),
          cacheTtlSeconds: 300,
        }),
      });
    });

    await page.route(/\/api\/v1\/admin\/circuit-breakers$/, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([]),
      });
    });
  });

  test('happy path: rotate DeepSeek key with step-up retry', async ({ page }) => {
    // ── BE mocks ───────────────────────────────────────────────────────────

    let rotateCallCount = 0;

    // Rotate-key endpoint: first call → 401 step_up_required, second → 200.
    await page.route(/\/api\/v1\/admin\/providers\/deepseek\/rotate-key$/, async route => {
      rotateCallCount += 1;
      if (rotateCallCount === 1) {
        await route.fulfill({
          status: 401,
          contentType: 'application/json',
          headers: {
            'WWW-Authenticate': 'TOTP-StepUp realm="meepleai-admin"',
          },
          body: JSON.stringify({
            error: 'two_factor_required',
            subcode: 'step_up_required',
            message: 'Step-up 2FA verification required.',
            correlationId: 'e2e-corr-1',
            timestamp: new Date().toISOString(),
          }),
        });
        return;
      }
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          providerName: 'deepseek',
          newKeyFingerprint: 'sk-rot01',
          rotatedAt: new Date().toISOString(),
          previousKeyDisabledAt: new Date().toISOString(),
        }),
      });
    });

    // Step-up endpoint: success on any 6-digit code (the FE auto-submits at len=6).
    await page.route(/\/api\/v1\/auth\/2fa\/step-up$/, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          success: true,
          lastTotpVerifiedAt: new Date().toISOString(),
        }),
      });
    });

    // ── Flow ───────────────────────────────────────────────────────────────

    await page.goto('/admin/providers');
    await page.waitForLoadState('networkidle');

    // The rotate button for DeepSeek must be present and enabled for SuperAdmin.
    const rotateButton = page.getByTestId('rotate-key-button-deepseek');
    await expect(rotateButton).toBeVisible({ timeout: 10000 });
    await expect(rotateButton).toBeEnabled();
    await rotateButton.click();

    // Modal opens.
    const modal = page.getByTestId('rotate-key-modal-deepseek');
    await expect(modal).toBeVisible();

    // Fill typed-confirm + new key.
    await page.getByTestId('rotate-key-confirm-input-deepseek').fill('deepseek');
    await page.getByTestId('rotate-key-new-input-deepseek').fill('sk-test-1234567890abcdef');

    // Submit.
    const submit = page.getByTestId('rotate-key-submit-deepseek');
    await expect(submit).toBeEnabled();
    await submit.click();

    // First attempt → step-up modal opens.
    const stepUpModal = page.getByTestId('step-up-2fa-modal');
    await expect(stepUpModal).toBeVisible({ timeout: 5000 });

    // Enter TOTP — autoSubmit triggers on len=6.
    await page.getByTestId('2fa-code-input').fill('123456');

    // Second attempt fires automatically → success card visible.
    const successCard = page.getByTestId('rotate-key-success-deepseek');
    await expect(successCard).toBeVisible({ timeout: 5000 });

    const fingerprint = page.getByTestId('rotate-key-fingerprint-deepseek');
    await expect(fingerprint).toHaveText('sk-rot01');

    // Sanity: rotate endpoint was called exactly twice (initial + auto-retry).
    expect(rotateCallCount).toBe(2);
  });
});

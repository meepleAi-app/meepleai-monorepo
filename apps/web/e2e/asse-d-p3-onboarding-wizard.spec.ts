import { test, expect } from '@playwright/test';

/**
 * Asse D follow-up P3 (#1895 / #1899) — /onboarding 3-step wizard generic flow.
 *
 * Scope: smoke verification that `/onboarding` either mounts the new
 * `OnboardingGenericWizard` (asse-B `WizardModal` based) or redirects to
 * login/library depending on auth + onboardingCompleted state. Full wizard
 * step-through is covered by unit tests; here we only assert the public
 * surface (mount OR redirect) without crashing.
 *
 * Chromium-only for speed, consistent with the existing
 * `asse-b-drawer-stack-flow.spec.ts` and `asse-d-p2-games-discover-hub.spec.ts`
 * patterns.
 */

test.describe('Asse D (#1899) P3 — onboarding wizard skeleton', () => {
  test.skip(({ browserName }) => browserName !== 'chromium', 'Chromium-only for speed');

  test('/onboarding renders wizard or redirects to login/library', async ({ page }) => {
    await page.goto('/onboarding');

    // Wait for either the wizard dialog OR a redirect to the auth flow / library.
    const wizard = page.locator('[role="dialog"], [data-slot="wizard-modal"]');
    const redirectPromise = page.waitForURL(/\/(login|auth|sign-in|library)/, { timeout: 4000 });

    const outcome = await Promise.race([
      wizard
        .first()
        .waitFor({ state: 'visible', timeout: 4000 })
        .then(() => 'wizard' as const)
        .catch(() => null),
      redirectPromise.then(() => 'redirect' as const).catch(() => null),
    ]);

    expect(['wizard', 'redirect']).toContain(outcome);
  });
});

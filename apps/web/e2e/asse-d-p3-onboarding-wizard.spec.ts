import { test, expect } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from './_helpers/seedAuthSession';
import { seedCookieConsent } from './_helpers/seedCookieConsent';

/**
 * Asse D follow-up P3 (#1895 / #1899) — /onboarding 3-step wizard generic flow.
 *
 * Originally tolerant (wizard OR redirect via Promise.race). Issue #1927
 * (Task A) wires the pre-existing seedAuthSession + mockAuthEndpoints
 * helpers AND extends `mockAuthEndpoints` with an `onboardingCompleted`
 * override (default `true`) so /onboarding can be reached by setting it
 * to `false`. The fallback URL race is dropped and replaced with direct
 * assertions on the WizardModal primitive surface.
 *
 * Full wizard step-through (Interests → FirstGame → Invite) is covered by
 * the unit test in `apps/web/src/app/(authenticated)/onboarding/__tests__/
 * OnboardingGenericWizard.test.tsx`. Here we assert that step 1 mounts on
 * a fresh /onboarding visit, including the step indicator and dialog
 * structural slots so renames of the WizardModal data-slot/testid surface
 * a hard E2E failure.
 */

test.describe('Asse D (#1899) P3 — onboarding wizard skeleton', () => {
  test.skip(({ browserName }) => browserName !== 'chromium', 'Chromium-only for speed');

  test.beforeEach(async ({ page }) => {
    await seedCookieConsent(page);
    await seedAuthSession(page);
    // CRITICAL: /onboarding redirects to /library when onboardingCompleted=true
    // (default). Override to false so the wizard mounts.
    await mockAuthEndpoints(page, { onboardingCompleted: false });
  });

  test('/onboarding mounts WizardModal with step indicator', async ({ page }) => {
    await page.goto('/onboarding');

    await expect(page).not.toHaveURL(/\/(login|auth|sign-in|library)/);

    // WizardModal primitive surface (slots defined in
    // `apps/web/src/components/ui/wizard-modal/wizard-modal.tsx`).
    const modal = page.locator('[data-slot="wizard-modal"]');
    await expect(modal).toBeVisible({ timeout: 10_000 });

    // Step indicator label is "Step N di M" (rendered by the primitive).
    // Asserting on the testid avoids coupling to Italian/English copy.
    const stepIndicator = page.locator('[data-testid="wizard-step-indicator"]');
    await expect(stepIndicator).toBeVisible();
  });

  test('/onboarding renders step 1 (Interests) on initial mount', async ({ page }) => {
    await page.goto('/onboarding');

    // Step 1 title varies with the seeded displayName ("Fixture User"):
    //   "Ciao Fixture User, scegli i tuoi interessi"
    // We assert on the wizard-title slot (aria-labelledby="wizard-title")
    // and the "scegli i tuoi interessi" substring to remain robust against
    // upstream copy tweaks.
    const wizardTitle = page.locator('#wizard-title');
    await expect(wizardTitle).toBeVisible({ timeout: 10_000 });
    await expect(wizardTitle).toContainText(/scegli i tuoi interessi/i);
  });

  test('/onboarding renders Next button (gated until step interaction)', async ({ page }) => {
    await page.goto('/onboarding');

    // The wizard-next CTA is always rendered for non-terminal steps; its
    // disabled state is driven by the step `validate` callback (here
    // `interestsCompleted` flag — `false` on first mount). We assert
    // presence + disabled-on-mount as the canonical step-1 invariant.
    const nextButton = page.locator('[data-slot="wizard-next"]');
    await expect(nextButton).toBeVisible({ timeout: 10_000 });
    await expect(nextButton).toBeDisabled();
  });
});

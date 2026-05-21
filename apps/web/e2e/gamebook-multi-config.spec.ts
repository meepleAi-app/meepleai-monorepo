import { test, expect } from '@playwright/test';

/**
 * Gamebook multi-book generalization (2026-05-19) — Phase F4 multi-config E2E.
 *
 * Validates the four canonical seed profiles from `GameBookSeeder`:
 *
 *   - Nanolith        → N>1 narrative books  → BookPicker visible
 *   - Maracaibo       → N=1 narrative book   → BookPicker hidden
 *   - Fighting Fantasy → 1 multi-role book   → Q&A works without picker
 *   - 7th Continent   → 0 books               → companion mode disabled (FM-19)
 *
 * IMPORTANT: This suite requires seeded data + a running stack (api + web +
 * postgres). It is NOT part of CI smoke — tagged `@gamebook-multi-config`
 * and run on demand:
 *
 *   pnpm test:e2e --grep "@gamebook-multi-config"
 *
 * Most tests are gated behind `test.skip()` until the seed is wired into
 * `CatalogSeedLayer` (deferred in F1+F2+F3 — see GameBookSeeder.cs class
 * doc). Once the orchestrator pass-through is added, flip the
 * `requiresSeededData` flag to `false`.
 *
 * Spec: docs/for-developers/specs/2026-05-19-gamebook-multi-book-generalization-design.md
 * Plan: docs/superpowers/plans/2026-05-19-gamebook-multi-book-generalization.md §F4
 */

const requiresSeededData = true; // flip when seed orchestrator wiring lands

test.describe('Gamebook multi-config @gamebook-multi-config', () => {
  test('BookPicker visible only when N>1 narrative books (Nanolith)', async ({ page }) => {
    test.skip(
      requiresSeededData,
      'requires seeded Nanolith with 4 GameBooks (Phase F orchestrator wiring deferred)'
    );

    await page.goto('/library/games/nanolith/play');
    await page.getByTestId('photo-translate-cta').click();

    const picker = page.getByRole('radiogroup', { name: 'Seleziona libro' });
    await expect(picker).toBeVisible();
    await expect(page.getByRole('radio', { name: 'Storybook' })).toBeVisible();
    await expect(page.getByRole('radio', { name: 'Encounter Book' })).toBeVisible();
  });

  test('BookPicker hidden when 1 narrative book (Maracaibo)', async ({ page }) => {
    test.skip(
      requiresSeededData,
      'requires seeded Maracaibo with 2 GameBooks (rulebook+story); orchestrator wiring deferred'
    );

    await page.goto('/library/games/maracaibo/play');
    await page.getByTestId('photo-translate-cta').click();

    await expect(page.getByRole('radiogroup', { name: 'Seleziona libro' })).not.toBeVisible();
  });

  test('Case A: Fighting Fantasy all-in-one Q&A works on single multi-role book', async ({
    page,
  }) => {
    test.skip(
      requiresSeededData,
      'requires seeded Fighting Fantasy + manifest entry (FF not yet in catalog manifests)'
    );

    await page.goto('/library/games/fighting-fantasy/play');
    await page.getByTestId('gamebook-open-chat').click();

    const chatInput = page.getByTestId('gamebook-chat-input');
    await expect(chatInput).toBeVisible();
    await chatInput.fill('come si crea il personaggio?');
    await chatInput.press('Enter');

    const assistantBubble = page
      .locator('[data-testid="chat-message-bubble"][data-role="assistant"]')
      .last();
    await expect(assistantBubble).toBeVisible({ timeout: 30_000 });
    await expect(assistantBubble).toContainText(/.{20,}/);

    // No "Press Start vs Rules" dropdown — single book
    await expect(page.getByText(/Press Start/i)).not.toBeVisible();
  });

  test('Case B: Maracaibo 2-libri — Setup query routes via Tutorial role on Rulebook', async ({
    page,
  }) => {
    test.skip(
      requiresSeededData,
      'requires seeded Maracaibo + role-aware query routing (D7 wiring)'
    );

    await page.goto('/library/games/maracaibo/play');
    await page.getByTestId('gamebook-open-chat').click();

    const chatInput = page.getByTestId('gamebook-chat-input');
    await expect(chatInput).toBeVisible();
    await chatInput.fill('setup per 3 giocatori');
    await chatInput.press('Enter');

    const assistantBubble = page
      .locator('[data-testid="chat-message-bubble"][data-role="assistant"]')
      .last();
    await expect(assistantBubble).toBeVisible({ timeout: 30_000 });
  });

  test('Case C: 7th Continent — companion disabled when no GameBook (FM-19)', async ({ page }) => {
    test.skip(
      requiresSeededData,
      'requires seeded 7th Continent SharedGame WITHOUT a GameBook (manifest only, no GameBookSeeder call)'
    );

    await page.goto('/library/games/7th-continent/play');

    await expect(page.getByText(/Modalità companion non disponibile/i)).toBeVisible();
    await expect(page.getByTestId('gamebook-open-chat')).toBeDisabled();
  });
});

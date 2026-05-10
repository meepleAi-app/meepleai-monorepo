/**
 * Smoke E2E — out-of-context state.
 *
 * Mock SSE V2: confidence=0, citations=[] → useGameChat deriva outOfContext=true
 * → OutOfContextActions rendered (3 pill: switch-game, find-agent, stay)
 *
 * Spec: docs/superpowers/specs/2026-05-10-e2e-smoke-game-night-design.md §3.3
 */
import { test, expect } from '@playwright/test';

import { loginSmokeAaron, mockNoChatHistory, mockQaStreamV2 } from './_helpers';
import { SEED_GAME_ID } from './_helpers.fixtures';

test.describe('Smoke — out of context (mocked)', () => {
  test('shows OutOfContextActions when confidence=0 and no citations', async ({ page }) => {
    await loginSmokeAaron(page);
    await mockNoChatHistory(page, SEED_GAME_ID);
    await mockQaStreamV2(page, {
      tokens: ['Non ho informazioni su questo argomento.'],
      citations: [],
      confidence: 0.0,
    });

    await page.goto(`/library/games/${SEED_GAME_ID}?tab=aiChat`);
    await page.waitForLoadState('networkidle');

    const input = page.locator('[data-testid="message-input"]');
    await input.fill('domanda fuori contesto');
    await page.locator('[data-testid="send-message-button"]').click();

    // OutOfContextActions visible
    const actions = page.locator('[data-slot="out-of-context-actions"]');
    await expect(actions).toBeVisible({ timeout: 10_000 });

    // 3 specific action pills (data-action-kind verified)
    await expect(page.locator('button[data-action-kind="switch-game"]')).toBeVisible();
    await expect(page.locator('button[data-action-kind="find-agent"]')).toBeVisible();
    await expect(page.locator('button[data-action-kind="stay"]')).toBeVisible();
  });
});

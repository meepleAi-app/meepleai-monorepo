/**
 * Smoke E2E — out-of-context state (nightly real-backend variant).
 *
 * Mock SSE V2: confidence=0, citations=[] → useGameChat deriva outOfContext=true
 * → OutOfContextActions rendered (3 pill: switch-game, find-agent, stay)
 *
 * Auth model: smoke-user (admin) + inline seedGameForChat — no snapshot DB needed,
 * compatible with `make dev-core` stack used by nightly cron.
 *
 * Spec: docs/superpowers/specs/2026-05-10-e2e-smoke-game-night-integration-design.md §4.1
 */
import { test, expect } from '@playwright/test';

import { smokeLogin, applySessionToPage } from './_helpers/auth';
import {
  mockNoChatHistory,
  mockQaStreamV2,
  mockReadyKbDocuments,
  seedGameForChat,
} from './_helpers/game-chat';

test.describe('SMOKE — game-chat out of context', () => {
  test('shows OutOfContextActions when confidence=0 and no citations', async ({
    page,
    request,
  }) => {
    const { cookieHeader } = await smokeLogin(request);
    await applySessionToPage(page, cookieHeader);
    const gameId = await seedGameForChat(request, cookieHeader);
    await mockNoChatHistory(page, gameId);
    await mockReadyKbDocuments(page, gameId);
    await mockQaStreamV2(page, {
      tokens: ['Non ho informazioni su questo argomento.'],
      citations: [],
      confidence: 0.0,
    });

    await page.goto(`/library/games/${gameId}?tab=aiChat`, { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('[data-testid="message-input"]', { timeout: 30_000 });

    const input = page.locator('[data-testid="message-input"]');
    await input.fill('domanda fuori contesto');
    await page.locator('[data-testid="send-message-button"]').click();

    const actions = page.locator('[data-slot="out-of-context-actions"]');
    await expect(actions).toBeVisible({ timeout: 10_000 });
    await expect(page.locator('button[data-action-kind="switch-game"]')).toBeVisible();
    await expect(page.locator('button[data-action-kind="find-agent"]')).toBeVisible();
    await expect(page.locator('button[data-action-kind="stay"]')).toBeVisible();
  });
});

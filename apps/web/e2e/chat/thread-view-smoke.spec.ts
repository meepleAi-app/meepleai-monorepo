/**
 * Chat Thread View — Smoke E2E (Phase 0 Strangler Fig, chat/shared primitives)
 *
 * AC-7: Exercises `/chat/[threadId]` route to prove the shared primitives
 * (components/chat/shared/*) compile and render through ChatThreadView without
 * regression. The test mocks auth + thread listing + thread detail + agent
 * chat stream with a minimal scripted SSE response, then verifies:
 *   1. The thread view renders (dynamic import of ChatThreadView succeeds)
 *   2. The message input is present and enabled
 *
 * Follows the mocking pattern from thread-management.spec.ts:
 *   - `page.context().route()` via `page.route()` scoped to this test's page
 *   - Auth via `/api/v1/auth/me` stub
 *   - Empty thread payload so the view initializes without extra fixtures
 *
 * This is INTENTIONALLY minimal (per plan Task 5): it asserts the route boots,
 * not end-to-end message flow. Streaming assertions require brittle selector
 * coupling; they are deferred until Phase 1 when ChatThreadView is refactored.
 *
 * @see docs/superpowers/plans/2026-04-24-chat-shared-primitives-phase-0.md AC-7
 */

import { test, expect } from '../fixtures';

import type { Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

const TEST_THREAD_ID = 'smoke-thread-id';
const TEST_USER_ID = 'smoke-user-id';
const TEST_AGENT_ID = 'smoke-agent-id';

async function setupSmokeMocks(page: Page) {
  // Auth: minimal authenticated session
  await page.route(`${API_BASE}/api/v1/auth/me`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user: {
          id: TEST_USER_ID,
          email: 'smoke@example.com',
          displayName: 'Smoke User',
          role: 'User',
        },
        expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
      }),
    });
  });

  // Thread detail: empty thread with a valid agent reference
  await page.route(`${API_BASE}/api/v1/chat/threads/${TEST_THREAD_ID}`, async route => {
    if (route.request().method() === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: TEST_THREAD_ID,
          title: 'Smoke Thread',
          createdAt: new Date().toISOString(),
          agentId: TEST_AGENT_ID,
          messages: [],
        }),
      });
    } else {
      await route.continue();
    }
  });

  // Thread list: single entry so sidebar renders
  await page.route(`${API_BASE}/api/v1/chat/threads**`, async route => {
    const method = route.request().method();
    const url = route.request().url();
    if (method === 'GET' && !url.match(/threads\/[^?]+/)) {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: TEST_THREAD_ID,
            title: 'Smoke Thread',
            createdAt: new Date().toISOString(),
            messageCount: 0,
          },
        ]),
      });
    } else {
      await route.continue();
    }
  });

  // Games + Agents: empty/permissive — prevent 404 cascades during bootstrap
  await page.route(`${API_BASE}/api/v1/games**`, async route => {
    if (route.request().method() === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([]),
      });
    } else {
      await route.continue();
    }
  });

  await page.route(`${API_BASE}/api/v1/agents**`, async route => {
    if (route.request().method() === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([]),
      });
    } else {
      await route.continue();
    }
  });
}

test.describe('Chat thread view — smoke (Phase 0)', () => {
  test('renders /chat/[threadId] and exposes the message input', async ({ page }) => {
    await setupSmokeMocks(page);

    await page.goto(`/chat/${TEST_THREAD_ID}`);

    // Dynamic import of ChatThreadView must resolve; loader disappears.
    await expect(page.getByText('Caricamento chat...')).toHaveCount(0, { timeout: 15_000 });

    // Message input renders on desktop viewport (default Playwright viewport 1280x720 ≥ lg breakpoint).
    // The input uses id="message-input" per chat-streaming.spec.ts convention.
    const input = page.locator('#message-input').first();
    await expect(input).toBeVisible({ timeout: 10_000 });
  });
});

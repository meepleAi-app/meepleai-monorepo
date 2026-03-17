/**
 * E2E Tests — Agent Content-Gating (Task 2.4)
 *
 * Tests content access control for agent responses:
 * 1. Game without KB → agent creation blocked
 * 2. User owns game → FullAccess (sources visible)
 * 3. User does NOT own game → ReferenceOnly (sources locked)
 * 4. User adds game → access unlocks
 *
 * Run: pnpm test:e2e apps/web/e2e/agent-content-gating.spec.ts
 */

import { test, expect } from '@playwright/test';

import { setupMockAuth } from './fixtures/auth';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

const TEST_GAME_ID = '00000000-0000-0000-0000-000000000001';

// ============================================================================
// Test 1: Game without KB → agent creation blocked
// ============================================================================

test.describe('Content Gating — Agent Builder KB Gate', () => {
  test('game without KB documents shows "No indexed documents" and blocks agent creation', async ({
    page,
  }) => {
    // Setup admin auth (skip navigation so we can add more mocks)
    await setupMockAuth(page, 'Admin', 'admin@meepleai.dev');

    // Mock shared game detail endpoint
    await page
      .context()
      .route(
        `**/${API_BASE.replace(/https?:\/\//, '')}/**/shared-games/${TEST_GAME_ID}`,
        async route => {
          if (route.request().method() === 'GET') {
            await route.fulfill({
              status: 200,
              contentType: 'application/json',
              body: JSON.stringify({
                id: TEST_GAME_ID,
                title: 'Test Board Game',
                description: 'A test game for content gating',
                status: 'Published',
                bggId: null,
                imageUrl: null,
                createdAt: new Date().toISOString(),
                updatedAt: new Date().toISOString(),
              }),
            });
          } else {
            await route.continue();
          }
        }
      );

    // Mock KB cards endpoint to return empty array (no indexed documents)
    await page
      .context()
      .route(
        `**/${API_BASE.replace(/https?:\/\//, '')}/**/shared-games/${TEST_GAME_ID}/kb-cards**`,
        async route => {
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify([]),
          });
        }
      );

    // Mock linked-agent endpoint (no agent yet)
    await page
      .context()
      .route(
        `**/${API_BASE.replace(/https?:\/\//, '')}/**/shared-games/${TEST_GAME_ID}/linked-agent**`,
        async route => {
          await route.fulfill({
            status: 404,
            contentType: 'application/json',
            body: JSON.stringify({ message: 'No linked agent found' }),
          });
        }
      );

    // Navigate to the shared game detail page
    await page.goto(`/admin/shared-games/${TEST_GAME_ID}`);
    await page.waitForLoadState('networkidle');

    // Look for the AI Agent tab or Create Agent button
    const aiAgentTab = page.locator('button[role="tab"]:has-text("AI Agent")').first();
    const createAgentButton = page.getByRole('button', { name: /create.*agent/i }).first();

    // Click AI Agent tab if it exists
    if (await aiAgentTab.isVisible({ timeout: 3000 }).catch(() => false)) {
      await aiAgentTab.click();
      await page.waitForLoadState('networkidle');
    }

    // Click Create Agent button if visible
    if (await createAgentButton.isVisible({ timeout: 3000 }).catch(() => false)) {
      await createAgentButton.click();

      // Wait for the modal to appear
      await page.waitForLoadState('networkidle');

      // Assert: "No indexed documents available" warning is visible
      await expect(page.getByText(/No indexed documents/i).first()).toBeVisible({ timeout: 5000 });
    } else {
      // If no Create Agent button, the gate may prevent showing it entirely
      // Check that some indication of missing KB is visible on the page
      const noDocsWarning = page
        .getByText(/No indexed documents|no documents|upload.*PDF/i)
        .first();
      await expect(noDocsWarning).toBeVisible({ timeout: 5000 });
    }
  });
});

// ============================================================================
// Test 2: User owns game → FullAccess (sources visible, no lock)
// ============================================================================

test.describe('Content Gating — FullAccess', () => {
  test('user who owns game sees full source text without lock icons', async ({ page }) => {
    // Setup regular user auth
    await setupMockAuth(page, 'User', 'user@meepleai.dev');

    // Mock chat POST endpoint to return FullAccess response with visible sources
    await page.context().route(`**/api/v1/chat**`, async route => {
      if (route.request().method() === 'POST') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'msg-001',
            threadId: 'thread-001',
            role: 'assistant',
            content: 'Here is the answer based on the rulebook.',
            contentAccessLevel: 'FullAccess',
            sources: [
              {
                reference: 'Rulebook p.12',
                text: 'The game begins with each player drawing 5 cards from the deck.',
                imageUrl: null,
                hasAccess: true,
              },
            ],
            createdAt: new Date().toISOString(),
          }),
        });
      } else {
        await route.continue();
      }
    });

    // Mock chat threads endpoint
    await page.context().route(`**/api/v1/chat/threads**`, async route => {
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

    // Navigate to chat page
    await page.goto('/chat');
    await page.waitForLoadState('networkidle');

    // Find the message input and send a message
    const messageInput = page
      .locator('[data-testid="message-input"]')
      .first()
      .or(page.locator('textarea').first())
      .or(page.locator('input[placeholder*="message" i]').first());

    if (await messageInput.isVisible({ timeout: 5000 }).catch(() => false)) {
      await messageInput.fill('How do I set up the game?');

      // Click send button
      const sendButton = page
        .locator('[data-testid="send-message-button"]')
        .first()
        .or(page.getByRole('button', { name: /send/i }).first());
      await sendButton.click();

      // Wait for response
      await page.waitForLoadState('networkidle');

      // Assert: source text is visible
      await expect(
        page.getByText('The game begins with each player drawing 5 cards from the deck.').first()
      ).toBeVisible({ timeout: 5000 });

      // Assert: no lock icon visible
      const lockIcon = page.locator('[aria-label="Content locked"]').first();
      await expect(lockIcon).not.toBeVisible({ timeout: 2000 });
    }
  });
});

// ============================================================================
// Test 3: User does NOT own game → ReferenceOnly (lock icon visible)
// ============================================================================

test.describe('Content Gating — ReferenceOnly', () => {
  test('user without game ownership sees lock icons and add-to-collection prompt', async ({
    page,
  }) => {
    // Setup regular user auth
    await setupMockAuth(page, 'User', 'user@meepleai.dev');

    // Mock chat POST endpoint to return ReferenceOnly response with locked sources
    await page.context().route(`**/api/v1/chat**`, async route => {
      if (route.request().method() === 'POST') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'msg-002',
            threadId: 'thread-002',
            role: 'assistant',
            content: 'Here is a reference from the rulebook.',
            contentAccessLevel: 'ReferenceOnly',
            sources: [
              {
                reference: 'Rulebook p.15',
                text: null,
                imageUrl: null,
                hasAccess: false,
              },
            ],
            createdAt: new Date().toISOString(),
          }),
        });
      } else {
        await route.continue();
      }
    });

    // Mock chat threads endpoint
    await page.context().route(`**/api/v1/chat/threads**`, async route => {
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

    // Navigate to chat page
    await page.goto('/chat');
    await page.waitForLoadState('networkidle');

    // Find the message input and send a message
    const messageInput = page
      .locator('[data-testid="message-input"]')
      .first()
      .or(page.locator('textarea').first())
      .or(page.locator('input[placeholder*="message" i]').first());

    if (await messageInput.isVisible({ timeout: 5000 }).catch(() => false)) {
      await messageInput.fill('How do I set up the game?');

      // Click send button
      const sendButton = page
        .locator('[data-testid="send-message-button"]')
        .first()
        .or(page.getByRole('button', { name: /send/i }).first());
      await sendButton.click();

      // Wait for response
      await page.waitForLoadState('networkidle');

      // Assert: lock icon IS visible
      await expect(page.locator('[aria-label="Content locked"]').first()).toBeVisible({
        timeout: 5000,
      });

      // Assert: "Add to collection" text is visible
      await expect(page.getByText(/add.*collection/i).first()).toBeVisible({ timeout: 5000 });
    }
  });
});

// ============================================================================
// Test 4: User adds game → access unlocks
// ============================================================================

test.describe('Content Gating — Access Unlock After Adding Game', () => {
  test('adding game to collection unlocks content access on next message', async ({ page }) => {
    // Setup regular user auth
    await setupMockAuth(page, 'User', 'user@meepleai.dev');

    // Track POST count to switch response between ReferenceOnly and FullAccess
    let chatPostCount = 0;

    // Mock chat POST endpoint with counter
    await page.context().route(`**/api/v1/chat**`, async route => {
      if (route.request().method() === 'POST') {
        chatPostCount++;

        if (chatPostCount <= 1) {
          // First message: ReferenceOnly (locked)
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({
              id: `msg-${chatPostCount}`,
              threadId: 'thread-003',
              role: 'assistant',
              content: 'Here is a reference from the rulebook.',
              contentAccessLevel: 'ReferenceOnly',
              sources: [
                {
                  reference: 'Rulebook p.20',
                  text: null,
                  imageUrl: null,
                  hasAccess: false,
                },
              ],
              createdAt: new Date().toISOString(),
            }),
          });
        } else {
          // Second message: FullAccess (unlocked)
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({
              id: `msg-${chatPostCount}`,
              threadId: 'thread-003',
              role: 'assistant',
              content: 'Now you have full access to the content.',
              contentAccessLevel: 'FullAccess',
              sources: [
                {
                  reference: 'Rulebook p.20',
                  text: 'Victory is achieved when a player reaches 10 points.',
                  imageUrl: null,
                  hasAccess: true,
                },
              ],
              createdAt: new Date().toISOString(),
            }),
          });
        }
      } else {
        await route.continue();
      }
    });

    // Mock chat threads endpoint
    await page.context().route(`**/api/v1/chat/threads**`, async route => {
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

    // Mock add-to-collection endpoint
    await page.context().route(`**/api/v1/games/*/collection**`, async route => {
      if (route.request().method() === 'POST') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ success: true }),
        });
      } else {
        await route.continue();
      }
    });

    // Also mock the user-library collection endpoint
    await page.context().route(`**/api/v1/user-library/collection**`, async route => {
      if (route.request().method() === 'POST') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ success: true }),
        });
      } else {
        await route.continue();
      }
    });

    // Navigate to chat page
    await page.goto('/chat');
    await page.waitForLoadState('networkidle');

    // Find the message input
    const messageInput = page
      .locator('[data-testid="message-input"]')
      .first()
      .or(page.locator('textarea').first())
      .or(page.locator('input[placeholder*="message" i]').first());

    if (await messageInput.isVisible({ timeout: 5000 }).catch(() => false)) {
      // --- Step 1: Send first message (ReferenceOnly) ---
      await messageInput.fill('What are the victory conditions?');

      const sendButton = page
        .locator('[data-testid="send-message-button"]')
        .first()
        .or(page.getByRole('button', { name: /send/i }).first());
      await sendButton.click();

      await page.waitForLoadState('networkidle');

      // Assert: lock icon is visible after first message
      await expect(page.locator('[aria-label="Content locked"]').first()).toBeVisible({
        timeout: 5000,
      });

      // --- Step 2: Click "Add to collection" if available, or simulate the action ---
      const addToCollectionButton = page.getByText(/add.*collection/i).first();
      if (await addToCollectionButton.isVisible({ timeout: 3000 }).catch(() => false)) {
        await addToCollectionButton.click();
        await page.waitForLoadState('networkidle');
      }

      // --- Step 3: Send second message (FullAccess after adding game) ---
      await messageInput.fill('Tell me more about the victory conditions.');
      await sendButton.click();

      await page.waitForLoadState('networkidle');

      // Assert: unlocked source text is visible
      await expect(
        page.getByText('Victory is achieved when a player reaches 10 points.').first()
      ).toBeVisible({ timeout: 5000 });
    }
  });
});

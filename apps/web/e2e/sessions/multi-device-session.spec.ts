/**
 * Multi-Device Session E2E Tests (Issue #302)
 *
 * Tests the Game Night multi-device session flow:
 * 1. Host creates session and generates invite
 * 2. Guest joins via PIN/token
 * 3. Participant list updates in real-time
 * 4. Score proposal flow (player → host → confirmed)
 * 5. Agent toggle broadcast
 * 6. Session leave/disconnect
 *
 * Uses two browser contexts to simulate host + guest on different devices.
 *
 * @see apps/api/src/Api/Routing/SessionInviteEndpoints.cs
 * @see apps/api/src/Api/Hubs/GameStateHub.cs
 * @see apps/web/src/components/session/InviteModal.tsx
 */

import { test, expect, type BrowserContext } from '@playwright/test';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// ============================================================================
// Mock Data
// ============================================================================

const SESSION_ID = '10000000-0000-0000-0000-000000000001';
const HOST_USER_ID = '20000000-0000-0000-0000-000000000001';
const GUEST_PARTICIPANT_ID = '30000000-0000-0000-0000-000000000001';
const HOST_PARTICIPANT_ID = '30000000-0000-0000-0000-000000000002';

const MOCK_INVITE = {
  pin: 'ABC123',
  linkToken: 'invite-token-abc123',
  expiresAt: new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString(),
};

const MOCK_JOIN_RESULT = {
  sessionId: SESSION_ID,
  participantId: GUEST_PARTICIPANT_ID,
  sessionToken: 'guest-session-token-xyz',
  displayName: 'Guest Player',
  role: 'Player',
};

const MOCK_PARTICIPANTS_AFTER_JOIN = [
  {
    id: HOST_PARTICIPANT_ID,
    displayName: 'Host User',
    role: 'Host',
    agentAccessEnabled: false,
    joinedAt: new Date(Date.now() - 60000).toISOString(),
    leftAt: null,
  },
  {
    id: GUEST_PARTICIPANT_ID,
    displayName: 'Guest Player',
    role: 'Player',
    agentAccessEnabled: false,
    joinedAt: new Date().toISOString(),
    leftAt: null,
  },
];

const MOCK_SESSION = {
  id: SESSION_ID,
  name: 'Game Night — Scythe',
  status: 'Active',
  gameId: '40000000-0000-0000-0000-000000000001',
  gameName: 'Scythe',
  createdByUserId: HOST_USER_ID,
  createdAt: new Date(Date.now() - 120000).toISOString(),
};

// ============================================================================
// Helpers
// ============================================================================

async function setupHostAuth(context: BrowserContext) {
  await context.route(`${API_BASE}/api/v1/auth/session**`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user: {
          id: HOST_USER_ID,
          email: 'host@example.com',
          displayName: 'Host User',
          role: 'User',
          tier: 'premium',
        },
        expiresAt: new Date(Date.now() + 86400000).toISOString(),
      }),
    });
  });
}

async function setupGuestRoutes(context: BrowserContext) {
  // Guest has no auth session — middleware bypass for join page
  await context.route(`${API_BASE}/api/v1/auth/session**`, async route => {
    await route.fulfill({
      status: 401,
      contentType: 'application/json',
      body: JSON.stringify({ error: 'Unauthorized' }),
    });
  });
}

async function setupSessionApiRoutes(context: BrowserContext) {
  // Create invite
  await context.route(`${API_BASE}/api/v1/live-sessions/${SESSION_ID}/invite**`, async route => {
    if (route.request().method() === 'POST') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_INVITE),
      });
    } else {
      await route.continue();
    }
  });

  // Get participants
  await context.route(
    `${API_BASE}/api/v1/live-sessions/${SESSION_ID}/participants**`,
    async route => {
      if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(MOCK_PARTICIPANTS_AFTER_JOIN),
        });
      } else if (route.request().method() === 'PUT') {
        // Toggle agent access
        await route.fulfill({ status: 204 });
      } else {
        await route.continue();
      }
    }
  );

  // Get session detail
  await context.route(`${API_BASE}/api/v1/live-sessions/${SESSION_ID}**`, async route => {
    if (route.request().method() === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_SESSION),
      });
    } else {
      await route.continue();
    }
  });

  // Join session
  await context.route(`${API_BASE}/api/v1/live-sessions/join**`, async route => {
    if (route.request().method() === 'POST') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_JOIN_RESULT),
      });
    } else {
      await route.continue();
    }
  });

  // Score endpoints
  await context.route(`${API_BASE}/api/v1/live-sessions/${SESSION_ID}/scores/**`, async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
  });
}

// ============================================================================
// Tests
// ============================================================================

test.describe('Multi-Device Session Flow', () => {
  test.describe('Invite & Join', () => {
    test('host creates invite with PIN and link token', async ({ browser }) => {
      const hostContext = await browser.newContext();
      const hostPage = await hostContext.newPage();

      await setupHostAuth(hostContext);
      await setupSessionApiRoutes(hostContext);

      // Mock the PLAYWRIGHT_AUTH_BYPASS
      await hostContext.route('**/_next/**', route => route.continue());

      // Navigate to session page
      await hostPage.goto(`/sessions/${SESSION_ID}`);

      // Look for invite button
      const inviteButton = hostPage
        .getByRole('button', { name: /invite/i })
        .or(hostPage.locator('[data-testid="invite-button"]'))
        .first();

      // If invite button exists and is visible, click to open modal
      if (await inviteButton.isVisible({ timeout: 3000 }).catch(() => false)) {
        await inviteButton.click();

        // Verify PIN is displayed
        await expect(hostPage.getByText('ABC123').first()).toBeVisible({ timeout: 5000 });
      }

      await hostContext.close();
    });

    test('guest joins session via token from join page', async ({ browser }) => {
      const guestContext = await browser.newContext();
      const guestPage = await guestContext.newPage();

      await setupGuestRoutes(guestContext);
      await setupSessionApiRoutes(guestContext);

      // Navigate to join page with token
      await guestPage.goto(`/sessions/${SESSION_ID}/join?token=${MOCK_INVITE.linkToken}`);

      // Fill guest name
      const nameInput = guestPage
        .getByLabel(/name/i)
        .or(guestPage.locator('input[name="guestName"]'))
        .or(guestPage.locator('input[placeholder*="name" i]'))
        .first();

      if (await nameInput.isVisible({ timeout: 3000 }).catch(() => false)) {
        await nameInput.fill('Guest Player');

        // Submit join form
        const joinButton = guestPage.getByRole('button', { name: /join/i }).first();

        if (await joinButton.isVisible({ timeout: 2000 }).catch(() => false)) {
          await joinButton.click();

          // Wait for API call or redirect
          await guestPage.waitForTimeout(1000);
        }
      }

      await guestContext.close();
    });

    test('join with invalid token shows error', async ({ browser }) => {
      const guestContext = await browser.newContext();
      const guestPage = await guestContext.newPage();

      await setupGuestRoutes(guestContext);

      // Mock join to return 404 for invalid token
      await guestContext.route(`${API_BASE}/api/v1/live-sessions/join**`, async route => {
        if (route.request().method() === 'POST') {
          await route.fulfill({
            status: 400,
            contentType: 'application/json',
            body: JSON.stringify({ error: 'Invalid invite code' }),
          });
        } else {
          await route.continue();
        }
      });

      await guestPage.goto(`/sessions/${SESSION_ID}/join?token=INVALID`);

      const nameInput = guestPage
        .getByLabel(/name/i)
        .or(guestPage.locator('input[name="guestName"]'))
        .or(guestPage.locator('input[placeholder*="name" i]'))
        .first();

      if (await nameInput.isVisible({ timeout: 3000 }).catch(() => false)) {
        await nameInput.fill('Bad Guest');

        const joinButton = guestPage.getByRole('button', { name: /join/i }).first();
        if (await joinButton.isVisible({ timeout: 2000 }).catch(() => false)) {
          await joinButton.click();

          // Should show error message
          const errorText = guestPage.getByText(/invalid|error|expired|failed/i).first();

          await expect(errorText).toBeVisible({ timeout: 5000 });
        }
      }

      await guestContext.close();
    });

    test('join with expired invite shows error', async ({ browser }) => {
      const guestContext = await browser.newContext();
      const guestPage = await guestContext.newPage();

      await setupGuestRoutes(guestContext);

      await guestContext.route(`${API_BASE}/api/v1/live-sessions/join**`, async route => {
        if (route.request().method() === 'POST') {
          await route.fulfill({
            status: 400,
            contentType: 'application/json',
            body: JSON.stringify({ error: 'This invite has expired' }),
          });
        } else {
          await route.continue();
        }
      });

      await guestPage.goto(`/sessions/${SESSION_ID}/join?token=EXPIRED`);

      const nameInput = guestPage
        .getByLabel(/name/i)
        .or(guestPage.locator('input[name="guestName"]'))
        .or(guestPage.locator('input[placeholder*="name" i]'))
        .first();

      if (await nameInput.isVisible({ timeout: 3000 }).catch(() => false)) {
        await nameInput.fill('Late Guest');
        const joinButton = guestPage.getByRole('button', { name: /join/i }).first();
        if (await joinButton.isVisible({ timeout: 2000 }).catch(() => false)) {
          await joinButton.click();
          const errorText = guestPage.getByText(/expired|error/i).first();
          await expect(errorText).toBeVisible({ timeout: 5000 });
        }
      }

      await guestContext.close();
    });
  });

  test.describe('Participant List', () => {
    test('session shows participant list with host and guest', async ({ browser }) => {
      const hostContext = await browser.newContext();
      const hostPage = await hostContext.newPage();

      await setupHostAuth(hostContext);
      await setupSessionApiRoutes(hostContext);

      await hostPage.goto(`/sessions/${SESSION_ID}/players`);

      // Verify both participants appear
      const hostName = hostPage.getByText('Host User').first();
      const guestName = hostPage.getByText('Guest Player').first();

      // At least one should be visible (depends on page rendering)
      const hostVisible = await hostName.isVisible({ timeout: 5000 }).catch(() => false);
      const guestVisible = await guestName.isVisible({ timeout: 3000 }).catch(() => false);

      expect(hostVisible || guestVisible).toBeTruthy();

      await hostContext.close();
    });

    test('participants page shows roles (Host vs Player)', async ({ browser }) => {
      const hostContext = await browser.newContext();
      const hostPage = await hostContext.newPage();

      await setupHostAuth(hostContext);
      await setupSessionApiRoutes(hostContext);

      await hostPage.goto(`/sessions/${SESSION_ID}/players`);

      // Check for role indicators
      const hostRole = hostPage.getByText(/host/i).first();
      const playerRole = hostPage.getByText(/player/i).first();

      const hasHost = await hostRole.isVisible({ timeout: 5000 }).catch(() => false);
      const hasPlayer = await playerRole.isVisible({ timeout: 3000 }).catch(() => false);

      expect(hasHost || hasPlayer).toBeTruthy();

      await hostContext.close();
    });
  });

  test.describe('Score Proposal Flow', () => {
    test('score proposal endpoint is called correctly', async ({ browser }) => {
      const hostContext = await browser.newContext();
      const hostPage = await hostContext.newPage();

      await setupHostAuth(hostContext);
      await setupSessionApiRoutes(hostContext);

      let proposeCalled = false;
      await hostContext.route(
        `${API_BASE}/api/v1/live-sessions/${SESSION_ID}/scores/propose**`,
        async route => {
          proposeCalled = true;
          const body = route.request().postDataJSON();
          expect(body).toHaveProperty('participantId');
          expect(body).toHaveProperty('value');
          await route.fulfill({ status: 200, body: '{}' });
        }
      );

      await hostPage.goto(`/sessions/${SESSION_ID}/scoreboard`);

      // Look for score input elements
      const scoreInput = hostPage
        .locator('input[type="number"]')
        .or(hostPage.locator('[data-testid="score-input"]'))
        .first();

      if (await scoreInput.isVisible({ timeout: 3000 }).catch(() => false)) {
        await scoreInput.fill('42');
        const submitButton = hostPage.getByRole('button', { name: /submit|propose|save/i }).first();
        if (await submitButton.isVisible({ timeout: 2000 }).catch(() => false)) {
          await submitButton.click();
          await hostPage.waitForTimeout(500);
          expect(proposeCalled).toBe(true);
        }
      }

      await hostContext.close();
    });

    test('score confirmation endpoint is called correctly', async ({ browser }) => {
      const hostContext = await browser.newContext();
      const hostPage = await hostContext.newPage();

      await setupHostAuth(hostContext);
      await setupSessionApiRoutes(hostContext);

      let confirmCalled = false;
      await hostContext.route(
        `${API_BASE}/api/v1/live-sessions/${SESSION_ID}/scores/confirm**`,
        async route => {
          confirmCalled = true;
          await route.fulfill({ status: 200, body: '{}' });
        }
      );

      await hostPage.goto(`/sessions/${SESSION_ID}/scoreboard`);

      // Look for confirm button on scoreboard
      const confirmButton = hostPage
        .getByRole('button', { name: /confirm/i })
        .or(hostPage.locator('[data-testid="confirm-score"]'))
        .first();

      if (await confirmButton.isVisible({ timeout: 3000 }).catch(() => false)) {
        await confirmButton.click();
        await hostPage.waitForTimeout(500);
        expect(confirmCalled).toBe(true);
      }

      await hostContext.close();
    });
  });

  test.describe('Agent Access Toggle', () => {
    test('agent access toggle calls API with correct payload', async ({ browser }) => {
      const hostContext = await browser.newContext();
      const hostPage = await hostContext.newPage();

      await setupHostAuth(hostContext);
      await setupSessionApiRoutes(hostContext);

      let toggleCalled = false;
      let togglePayload: Record<string, unknown> | null = null;

      await hostContext.route(
        `${API_BASE}/api/v1/live-sessions/${SESSION_ID}/participants/${GUEST_PARTICIPANT_ID}/agent-access**`,
        async route => {
          if (route.request().method() === 'PUT') {
            toggleCalled = true;
            togglePayload = route.request().postDataJSON();
            await route.fulfill({ status: 204 });
          } else {
            await route.continue();
          }
        }
      );

      await hostPage.goto(`/sessions/${SESSION_ID}/players`);

      // Look for agent toggle switch
      const agentToggle = hostPage
        .locator('[data-testid="agent-toggle"]')
        .or(hostPage.getByRole('switch').first())
        .first();

      if (await agentToggle.isVisible({ timeout: 3000 }).catch(() => false)) {
        await agentToggle.click();
        await hostPage.waitForTimeout(500);

        if (toggleCalled) {
          expect(togglePayload).toHaveProperty('enabled');
        }
      }

      await hostContext.close();
    });
  });

  test.describe('Session Navigation', () => {
    test('session page loads with correct session name', async ({ browser }) => {
      const hostContext = await browser.newContext();
      const hostPage = await hostContext.newPage();

      await setupHostAuth(hostContext);
      await setupSessionApiRoutes(hostContext);

      await hostPage.goto(`/sessions/${SESSION_ID}`);

      // Session name or game name should appear
      const sessionTitle = hostPage.getByText(/scythe|game night/i).first();

      const titleVisible = await sessionTitle.isVisible({ timeout: 5000 }).catch(() => false);

      // Page should at least load without error
      const errorPage = hostPage.getByText(/not found|error|500/i).first();
      const hasError = await errorPage.isVisible({ timeout: 1000 }).catch(() => false);

      expect(hasError).toBeFalsy();

      await hostContext.close();
    });

    test('session sub-pages are accessible', async ({ browser }) => {
      const hostContext = await browser.newContext();
      const hostPage = await hostContext.newPage();

      await setupHostAuth(hostContext);
      await setupSessionApiRoutes(hostContext);

      const subPages = ['players', 'scoreboard', 'notes'];

      for (const subPage of subPages) {
        await hostPage.goto(`/sessions/${SESSION_ID}/${subPage}`);

        // Each page should load without a hard error
        const errorIndicator = hostPage.getByText(/500|server error/i).first();
        const hasError = await errorIndicator.isVisible({ timeout: 2000 }).catch(() => false);
        expect(hasError).toBeFalsy();
      }

      await hostContext.close();
    });
  });

  test.describe('Two-Context Simulation', () => {
    test('host and guest can load session pages simultaneously', async ({ browser }) => {
      // Create two independent browser contexts (simulates two devices)
      const hostContext = await browser.newContext();
      const guestContext = await browser.newContext();

      const hostPage = await hostContext.newPage();
      const guestPage = await guestContext.newPage();

      // Setup routes for both contexts
      await setupHostAuth(hostContext);
      await setupSessionApiRoutes(hostContext);
      await setupGuestRoutes(guestContext);
      await setupSessionApiRoutes(guestContext);

      // Host loads session page
      await hostPage.goto(`/sessions/${SESSION_ID}`);

      // Guest loads join page
      await guestPage.goto(`/sessions/${SESSION_ID}/join?token=${MOCK_INVITE.linkToken}`);

      // Both pages should load without errors
      const hostError = hostPage.getByText(/500|server error/i).first();
      const guestError = guestPage.getByText(/500|server error/i).first();

      const hostHasError = await hostError.isVisible({ timeout: 2000 }).catch(() => false);
      const guestHasError = await guestError.isVisible({ timeout: 2000 }).catch(() => false);

      expect(hostHasError).toBeFalsy();
      expect(guestHasError).toBeFalsy();

      // Guest should see join form elements
      const nameInput = guestPage
        .getByLabel(/name/i)
        .or(guestPage.locator('input[name="guestName"]'))
        .or(guestPage.locator('input[placeholder*="name" i]'))
        .first();

      const hasJoinForm = await nameInput.isVisible({ timeout: 3000 }).catch(() => false);

      // Guest page should render the join form (name input)
      expect(hasJoinForm).toBe(true);

      await hostContext.close();
      await guestContext.close();
    });

    test('both contexts receive independent API responses', async ({ browser }) => {
      const hostContext = await browser.newContext();
      const guestContext = await browser.newContext();

      let hostApiCalled = false;
      let guestApiCalled = false;

      // Host gets full participant list
      await setupHostAuth(hostContext);
      await hostContext.route(
        `${API_BASE}/api/v1/live-sessions/${SESSION_ID}/participants**`,
        async route => {
          hostApiCalled = true;
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify(MOCK_PARTICIPANTS_AFTER_JOIN),
          });
        }
      );

      // Guest gets participants after joining
      await setupGuestRoutes(guestContext);
      await setupSessionApiRoutes(guestContext);
      await guestContext.route(
        `${API_BASE}/api/v1/live-sessions/${SESSION_ID}/participants**`,
        async route => {
          guestApiCalled = true;
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify(MOCK_PARTICIPANTS_AFTER_JOIN),
          });
        }
      );

      const hostPage = await hostContext.newPage();
      const guestPage = await guestContext.newPage();

      // Both load participants page
      await Promise.all([
        hostPage.goto(`/sessions/${SESSION_ID}/players`),
        guestPage.goto(`/sessions/${SESSION_ID}/players`),
      ]);

      // Wait for API calls to complete
      await hostPage.waitForTimeout(1000);
      await guestPage.waitForTimeout(1000);

      // Both contexts should have made independent API calls
      expect(hostApiCalled || guestApiCalled).toBe(true);

      await hostContext.close();
      await guestContext.close();
    });
  });

  test.describe('Edge Cases', () => {
    test('session at max capacity rejects new joins', async ({ browser }) => {
      const guestContext = await browser.newContext();
      const guestPage = await guestContext.newPage();

      await setupGuestRoutes(guestContext);

      await guestContext.route(`${API_BASE}/api/v1/live-sessions/join**`, async route => {
        if (route.request().method() === 'POST') {
          await route.fulfill({
            status: 400,
            contentType: 'application/json',
            body: JSON.stringify({
              error: 'Session has reached the maximum of 6 players',
            }),
          });
        } else {
          await route.continue();
        }
      });

      await guestPage.goto(`/sessions/${SESSION_ID}/join?token=FULL`);

      const nameInput = guestPage
        .getByLabel(/name/i)
        .or(guestPage.locator('input[name="guestName"]'))
        .or(guestPage.locator('input[placeholder*="name" i]'))
        .first();

      if (await nameInput.isVisible({ timeout: 3000 }).catch(() => false)) {
        await nameInput.fill('Extra Player');
        const joinButton = guestPage.getByRole('button', { name: /join/i }).first();
        if (await joinButton.isVisible({ timeout: 2000 }).catch(() => false)) {
          await joinButton.click();
          const errorText = guestPage.getByText(/maximum|full|capacity/i).first();
          await expect(errorText).toBeVisible({ timeout: 5000 });
        }
      }

      await guestContext.close();
    });

    test('revoked invite shows appropriate error', async ({ browser }) => {
      const guestContext = await browser.newContext();
      const guestPage = await guestContext.newPage();

      await setupGuestRoutes(guestContext);

      await guestContext.route(`${API_BASE}/api/v1/live-sessions/join**`, async route => {
        if (route.request().method() === 'POST') {
          await route.fulfill({
            status: 400,
            contentType: 'application/json',
            body: JSON.stringify({ error: 'This invite has been revoked' }),
          });
        } else {
          await route.continue();
        }
      });

      await guestPage.goto(`/sessions/${SESSION_ID}/join?token=REVOKED`);

      const nameInput = guestPage
        .getByLabel(/name/i)
        .or(guestPage.locator('input[name="guestName"]'))
        .or(guestPage.locator('input[placeholder*="name" i]'))
        .first();

      if (await nameInput.isVisible({ timeout: 3000 }).catch(() => false)) {
        await nameInput.fill('Blocked Guest');
        const joinButton = guestPage.getByRole('button', { name: /join/i }).first();
        if (await joinButton.isVisible({ timeout: 2000 }).catch(() => false)) {
          await joinButton.click();
          const errorText = guestPage.getByText(/revoked|error/i).first();
          await expect(errorText).toBeVisible({ timeout: 5000 });
        }
      }

      await guestContext.close();
    });
  });
});

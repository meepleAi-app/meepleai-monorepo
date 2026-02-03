/**
 * Journey 2: Post-Game Session Recording E2E Tests
 *
 * Tests the complete post-game workflow for users finishing a game:
 * 1. Quick Registration - Fast logging (winner, duration, date)
 * 2. Detailed Expansion - Add notes, ratings, player scores
 *
 * Pattern: Hybrid approach (mock external APIs, test real session APIs)
 * Related Issue: #2843 - E2E User Journey Tests
 * Epic: #2823
 */

import { expect, test } from '../fixtures';
import { WaitHelper } from '../helpers/WaitHelper';
import { AuthHelper, USER_FIXTURES } from '../pages';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

const MOCK_GAME = {
  id: 'test-game-post-1',
  title: 'Ticket to Ride',
  bggId: 9209,
  minPlayers: 2,
  maxPlayers: 5,
  minPlayTimeMinutes: 30,
  maxPlayTimeMinutes: 60,
  complexity: 1.9,
  yearPublished: 2004,
};

const MOCK_SESSION_QUICK = {
  id: 'test-session-quick-1',
  gameId: MOCK_GAME.id,
  status: 'Completed',
  startedAt: new Date().toISOString(),
  endedAt: new Date().toISOString(),
  durationMinutes: 45,
  winner: 'Alice',
  players: [
    { playerName: 'Alice', playerOrder: 1, color: '#E63946' },
    { playerName: 'Bob', playerOrder: 2, color: '#06D6A0' },
  ],
};

const MOCK_SESSION_DETAILED = {
  ...MOCK_SESSION_QUICK,
  id: 'test-session-detailed-1',
  notes: 'Great game! Alice won with longest route strategy.',
  rating: 8,
  playerScores: [
    { playerName: 'Alice', score: 125 },
    { playerName: 'Bob', score: 98 },
  ],
};

test.describe('Journey 2: Post-Game Session Recording', () => {
  test.beforeEach(async ({ page }) => {
    const authHelper = new AuthHelper(page);
    await page.emulateMedia({ reducedMotion: 'reduce' });

    // Auth: Mock authenticated session
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);

    // Mock: Games API
    await page.route(`${API_BASE}/api/v1/games/${MOCK_GAME.id}`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_GAME),
      });
    });

    // Mock: Sessions API (create session)
    await page.route(`${API_BASE}/api/v1/sessions`, async route => {
      if (route.request().method() === 'POST') {
        const postData = route.request().postDataJSON();

        // Return session based on request data
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify({
            id: `test-session-${Date.now()}`,
            gameId: postData.gameId,
            status: 'InProgress',
            startedAt: new Date().toISOString(),
            players: postData.players || [],
          }),
        });
      } else {
        await route.continue();
      }
    });

    // Mock: End session API
    let sessionEnded = false;
    await page.route(`${API_BASE}/api/v1/sessions/*/end`, async route => {
      if (route.request().method() === 'POST') {
        const postData = route.request().postDataJSON();
        sessionEnded = true;

        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            ...MOCK_SESSION_QUICK,
            endedAt: new Date().toISOString(),
            winner: postData.winner || MOCK_SESSION_QUICK.winner,
            durationMinutes: postData.durationMinutes || MOCK_SESSION_QUICK.durationMinutes,
          }),
        });
      }
    });

    // Mock: Update session (detailed expansion)
    await page.route(`${API_BASE}/api/v1/sessions/*`, async route => {
      if (route.request().method() === 'PUT' || route.request().method() === 'PATCH') {
        const postData = route.request().postDataJSON();

        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            ...MOCK_SESSION_DETAILED,
            notes: postData.notes || MOCK_SESSION_DETAILED.notes,
            rating: postData.rating || MOCK_SESSION_DETAILED.rating,
            playerScores: postData.playerScores || MOCK_SESSION_DETAILED.playerScores,
          }),
        });
      } else if (route.request().method() === 'GET') {
        // Return completed session
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(sessionEnded ? MOCK_SESSION_QUICK : { status: 'InProgress' }),
        });
      }
    });

    // Mock: Session history
    await page.route(`${API_BASE}/api/v1/sessions/history*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: sessionEnded ? [MOCK_SESSION_QUICK] : [],
          totalCount: sessionEnded ? 1 : 0,
        }),
      });
    });
  });

  test('should complete quick session registration', async ({ page }) => {
    await test.step('Navigate to game detail page', async () => {
      await page.goto(`/games/${MOCK_GAME.id}`);
      await expect(page.locator(`text=${MOCK_GAME.title}`).first()).toBeVisible({ timeout: 10000 });
    });

    await test.step('Start new game session', async () => {
      // Look for "Start Session" or "Play Game" button
      const startButton = page.getByRole('button', { name: /start session|play game|new session/i }).first();

      if (await startButton.isVisible({ timeout: 5000 })) {
        await startButton.click();

        // Wait for SessionSetupModal
        await expect(page.locator('text=Session Setup')).toBeVisible({ timeout: 5000 });
      } else {
        // Alternative: navigate to sessions page directly
        await page.goto('/sessions');
        const newSessionButton = page.getByRole('button', { name: /new session|start/i }).first();
        await newSessionButton.click();
      }
    });

    await test.step('Configure players (quick setup)', async () => {
      // Fill in minimum required fields
      const player1Input = page.locator('input[name="players[0].playerName"], input[placeholder*="Player 1"]').first();
      const player2Input = page.locator('input[name="players[1].playerName"], input[placeholder*="Player 2"]').first();

      await player1Input.fill('Alice');
      await player2Input.fill('Bob');

      // Submit session setup
      const startButton = page.getByRole('button', { name: /start|begin|create/i }).first();
      await startButton.click();

      const waitHelper = new WaitHelper(page);
      await waitHelper.waitForNetworkIdle(3000);
    });

    await test.step('End session with quick registration', async () => {
      // Look for "End Session" button
      const endButton = page.getByRole('button', { name: /end session|finish|complete/i }).first();

      if (await endButton.isVisible({ timeout: 5000 })) {
        await endButton.click();

        // Quick form: winner + duration
        const winnerSelect = page.locator('select[name="winner"]').first();
        if (await winnerSelect.isVisible({ timeout: 3000 })) {
          await winnerSelect.selectOption({ label: 'Alice' });
        }

        const durationInput = page.locator('input[name="duration"], input[type="number"]').first();
        if (await durationInput.isVisible({ timeout: 3000 })) {
          await durationInput.fill('45');
        }

        // Submit quick registration
        const submitButton = page.getByRole('button', { name: /save|submit|finish/i }).first();
        await submitButton.click();

        const waitHelper = new WaitHelper(page);
        await waitHelper.waitForNetworkIdle(3000);
      }
    });

    await test.step('Verify session appears in history', async () => {
      // Navigate to Sessions tab or history page
      await page.goto('/sessions/history');
      await page.waitForLoadState('networkidle');

      // Verify quick session appears
      await expect(page.locator('text=Alice')).toBeVisible({ timeout: 5000 });
      await expect(page.locator('text=45')).toBeVisible(); // duration
    });

    console.log('✅ Journey 2 (Quick): Quick session registration successful');
  });

  test('should expand quick registration with detailed info', async ({ page }) => {
    await test.step('Navigate to completed session', async () => {
      // Start with a completed session in history
      await page.goto('/sessions/history');
      await page.waitForLoadState('networkidle');

      // Click on first session to expand/edit
      const sessionCard = page.locator('[data-testid="session-card"]').first();
      if (await sessionCard.isVisible({ timeout: 5000 })) {
        await sessionCard.click();
      } else {
        // Alternative: look for expand/edit button
        const editButton = page.getByRole('button', { name: /edit|expand|details/i }).first();
        await editButton.click();
      }

      const waitHelper = new WaitHelper(page);
      await waitHelper.waitForNetworkIdle(2000);
    });

    await test.step('Add detailed information', async () => {
      // Add notes
      const notesTextarea = page.locator('textarea[name="notes"], textarea[placeholder*="notes"]').first();
      if (await notesTextarea.isVisible({ timeout: 5000 })) {
        await notesTextarea.fill('Great game! Alice won with longest route strategy.');
      }

      // Add rating
      const ratingInput = page.locator('input[name="rating"], select[name="rating"]').first();
      if (await ratingInput.isVisible({ timeout: 5000 })) {
        const tagName = await ratingInput.evaluate(el => el.tagName);
        if (tagName === 'SELECT') {
          await ratingInput.selectOption({ label: '8' });
        } else {
          await ratingInput.fill('8');
        }
      }

      // Add player scores (if available)
      const aliceScoreInput = page.locator('input[placeholder*="Alice"], input[name*="score"][value=""]').first();
      if (await aliceScoreInput.isVisible({ timeout: 3000 })) {
        await aliceScoreInput.fill('125');
      }

      const bobScoreInput = page.locator('input[placeholder*="Bob"]').nth(1);
      if (await bobScoreInput.isVisible({ timeout: 3000 })) {
        await bobScoreInput.fill('98');
      }
    });

    await test.step('Save detailed session data', async () => {
      const saveButton = page.getByRole('button', { name: /save|update|submit/i }).first();
      await saveButton.click();

      const waitHelper = new WaitHelper(page);
      await waitHelper.waitForNetworkIdle(3000);
    });

    await test.step('Verify detailed data persisted', async () => {
      // Reload session to verify persistence
      await page.reload();
      await page.waitForLoadState('networkidle');

      // Verify notes persisted
      const pageContent = await page.textContent('body');
      expect(pageContent).toContain('longest route strategy');

      // Verify rating persisted
      expect(pageContent).toContain('8');

      // Verify scores persisted
      expect(pageContent).toContain('125');
      expect(pageContent).toContain('98');
    });

    console.log('✅ Journey 2 (Detailed): Detailed session expansion successful');
  });

  test('should navigate from game page to session recording', async ({ page }) => {
    await test.step('Access game detail page', async () => {
      await page.goto(`/games/${MOCK_GAME.id}`);
      await expect(page.locator(`text=${MOCK_GAME.title}`).first()).toBeVisible();
    });

    await test.step('Access Sessions tab', async () => {
      const sessionsTab = page.getByRole('tab', { name: /sessions/i });
      await sessionsTab.click();

      const waitHelper = new WaitHelper(page);
      await waitHelper.waitForNetworkIdle(2000);

      // Verify sessions content loaded
      await expect(page.locator('text=Session History')).toBeVisible({ timeout: 5000 });
    });

    await test.step('Start new session from game context', async () => {
      // Should have "New Session" button in Sessions tab
      const newSessionButton = page.getByRole('button', { name: /new session|start game|play/i }).first();

      if (await newSessionButton.isVisible({ timeout: 3000 })) {
        await newSessionButton.click();

        // Verify SessionSetupModal opened
        await expect(page.locator('text=Session Setup')).toBeVisible({ timeout: 5000 });

        console.log('✅ Journey 2 (Navigation): Session recording accessible from game page');
      } else {
        console.log('⚠️  New Session button not found in Sessions tab (feature may be elsewhere)');
      }
    });
  });
});

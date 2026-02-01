/**
 * Game Sessions Flow - Visual Documentation
 *
 * Captures visual documentation for game session flows:
 * - Create session
 * - Track game state
 * - Player management
 * - Save/restore snapshots
 *
 * @see docs/08-user-flows/user-role/05-game-sessions.md
 */

import { test } from '../../fixtures';
import { AuthHelper, USER_FIXTURES } from '../../pages';
import {
  ScreenshotHelper,
  USER_FLOWS,
  disableAnimations,
  waitForStableState,
  ANNOTATION_COLORS,
} from '../fixtures/screenshot-helpers';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// Mock session data
const MOCK_SESSIONS = [
  {
    id: 'session-1',
    gameId: 'game-1',
    gameName: 'Ticket to Ride',
    status: 'active',
    players: [
      { id: 'p1', name: 'Alice', color: 'blue' },
      { id: 'p2', name: 'Bob', color: 'red' },
    ],
    createdAt: '2026-01-19T18:00:00Z',
    lastUpdatedAt: '2026-01-19T20:00:00Z',
  },
];

const MOCK_SESSION_STATE = {
  sessionId: 'session-1',
  currentTurn: 'p1',
  roundNumber: 5,
  players: [
    { id: 'p1', name: 'Alice', score: 45, resources: { trains: 35, cards: 4 } },
    { id: 'p2', name: 'Bob', score: 38, resources: { trains: 40, cards: 6 } },
  ],
};

test.describe('Game Sessions Flow - Visual Documentation', () => {
  let helper: ScreenshotHelper;
  let authHelper: AuthHelper;

  test.beforeEach(async ({ page }) => {
    helper = new ScreenshotHelper({
      outputDir: USER_FLOWS.gameSessions.outputDir,
      flow: USER_FLOWS.gameSessions.name,
      role: USER_FLOWS.gameSessions.role,
    });
    authHelper = new AuthHelper(page);
    await disableAnimations(page);

    // Setup authenticated session
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);

    // Mock sessions endpoints
    await page.route(`${API_BASE}/api/v1/sessions*`, async route => {
      const url = route.request().url();
      if (route.request().method() === 'POST') {
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'new-session',
            gameId: 'game-1',
            status: 'active',
          }),
        });
      } else if (url.includes('/sessions/session-1/state')) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(MOCK_SESSION_STATE),
        });
      } else if (url.includes('/sessions/session-1')) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(MOCK_SESSIONS[0]),
        });
      } else {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(MOCK_SESSIONS),
        });
      }
    });

    // Mock games
    await page.route(`${API_BASE}/api/v1/games*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'game-1', title: 'Ticket to Ride', minPlayers: 2, maxPlayers: 5 },
        ]),
      });
    });
  });

  test('create session - setup flow', async ({ page }) => {
    // Step 1: Navigate to sessions
    await page.goto('/sessions');
    await waitForStableState(page);

    await helper.capture(page, {
      step: 1,
      title: 'Sessions Overview',
      description: 'View active and past game sessions',
      annotations: [
        { selector: 'button:has-text("New Session"), button:has-text("Start"), [data-testid="new-session"]', label: 'New Session', color: ANNOTATION_COLORS.success },
      ],
      nextAction: 'Create new session',
    });

    // Step 2: Click new session
    const newSessionBtn = page.locator('button:has-text("New Session"), button:has-text("Start"), [data-testid="new-session"]').first();
    if (await newSessionBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await newSessionBtn.click();
      await waitForStableState(page);

      // Step 3: Session setup modal/page
      const setupForm = page.locator('[data-testid="session-setup"], [role="dialog"], .session-setup').first();
      if (await setupForm.isVisible({ timeout: 2000 }).catch(() => false)) {
        await helper.capture(page, {
          step: 2,
          title: 'Session Setup',
          description: 'Configure new game session - select game and add players',
          annotations: [
            { selector: '[data-testid="game-select"], select', label: 'Select Game', color: ANNOTATION_COLORS.primary },
          ],
          previousAction: 'Click new session',
          nextAction: 'Select game',
        });
      }
    }

    // Step 4: Player setup
    const playerSection = page.locator('[data-testid="players"], .players-setup, text=/player/i').first();
    if (await playerSection.isVisible({ timeout: 2000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 3,
        title: 'Add Players',
        description: 'Add players to the game session',
        annotations: [
          { selector: 'button:has-text("Add Player"), [data-testid="add-player"]', label: 'Add Player', color: ANNOTATION_COLORS.info },
        ],
        previousAction: 'Select game',
        nextAction: 'Add players',
      });
    }

    helper.setTotalSteps(3);
    console.log(`\n✅ Create session captured: ${helper.getCapturedSteps().length} screenshots`);
  });

  test('active session - game state tracking', async ({ page }) => {
    // Step 1: Navigate to active session
    await page.goto('/sessions/session-1');
    await waitForStableState(page);

    await helper.capture(page, {
      step: 1,
      title: 'Active Session',
      description: 'Live game session with player states',
      annotations: [
        { selector: '[data-testid="session-header"], h1', label: 'Session', color: ANNOTATION_COLORS.primary },
      ],
      nextAction: 'Track game state',
    });

    // Step 2: Player scoreboard
    const scoreboard = page.locator('[data-testid="scoreboard"], .scoreboard, .player-scores').first();
    if (await scoreboard.isVisible({ timeout: 3000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 2,
        title: 'Scoreboard',
        description: 'Track player scores and resources',
        annotations: [
          { selector: '[data-testid="scoreboard"], .scoreboard', label: 'Scores', color: ANNOTATION_COLORS.success },
        ],
        previousAction: 'View session',
        nextAction: 'Update scores',
      });
    }

    // Step 3: Turn indicator
    const turnIndicator = page.locator('[data-testid="current-turn"], .turn-indicator, text=/turn/i').first();
    if (await turnIndicator.isVisible({ timeout: 2000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 3,
        title: 'Turn Tracker',
        description: 'Shows current player turn and round number',
        annotations: [
          { selector: '[data-testid="current-turn"], .turn-indicator', label: 'Current Turn', color: ANNOTATION_COLORS.info },
        ],
        previousAction: 'View scores',
        nextAction: 'Pass turn',
      });
    }

    helper.setTotalSteps(3);
    console.log(`\n✅ Active session captured: ${helper.getCapturedSteps().length} screenshots`);
  });

  test('session state updates', async ({ page }) => {
    // Step 1: Navigate to session
    await page.goto('/sessions/session-1');
    await waitForStableState(page);

    await helper.capture(page, {
      step: 1,
      title: 'Session State',
      description: 'Current game state ready for updates',
      nextAction: 'Update player state',
    });

    // Step 2: State input controls
    const stateInput = page.locator('input[type="number"], [data-testid="score-input"], .score-input').first();
    if (await stateInput.isVisible({ timeout: 3000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 2,
        title: 'Score Update',
        description: 'Input controls for updating player scores',
        annotations: [
          { selector: 'input[type="number"], [data-testid="score-input"]', label: 'Score Input', color: ANNOTATION_COLORS.primary },
        ],
        previousAction: 'View state',
        nextAction: 'Enter new score',
      });

      await stateInput.fill('50');
      await waitForStableState(page);

      await helper.capture(page, {
        step: 3,
        title: 'Score Entered',
        description: 'New score value ready to save',
        annotations: [
          { selector: 'input[type="number"], [data-testid="score-input"]', label: 'New Score', color: ANNOTATION_COLORS.success },
          { selector: 'button:has-text("Save"), button:has-text("Update")', label: 'Save', color: ANNOTATION_COLORS.primary },
        ],
        previousAction: 'Enter score',
        nextAction: 'Save changes',
      });
    }

    helper.setTotalSteps(3);
    console.log(`\n✅ State updates captured: ${helper.getCapturedSteps().length} screenshots`);
  });

  test('session snapshots - save and restore', async ({ page }) => {
    // Mock snapshot endpoints
    await page.route(`${API_BASE}/api/v1/sessions/session-1/state/snapshots*`, async route => {
      if (route.request().method() === 'POST') {
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify({ snapshotId: 'snap-1', createdAt: new Date().toISOString() }),
        });
      } else {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([
            { id: 'snap-1', name: 'End of Round 5', createdAt: '2026-01-19T19:00:00Z' },
            { id: 'snap-2', name: 'Before final scoring', createdAt: '2026-01-19T20:00:00Z' },
          ]),
        });
      }
    });

    // Step 1: Navigate to session
    await page.goto('/sessions/session-1');
    await waitForStableState(page);

    // Step 2: Save snapshot button
    const saveSnapshotBtn = page.locator('button:has-text("Save Snapshot"), button:has-text("Save State"), [data-testid="save-snapshot"]').first();
    if (await saveSnapshotBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 1,
        title: 'Save Snapshot',
        description: 'Save current game state as a snapshot',
        annotations: [
          { selector: 'button:has-text("Save Snapshot"), [data-testid="save-snapshot"]', label: 'Save Snapshot', color: ANNOTATION_COLORS.primary },
        ],
        nextAction: 'Click save snapshot',
      });
    }

    // Step 3: Snapshots list
    const snapshotsList = page.locator('[data-testid="snapshots"], .snapshots-list').first();
    if (await snapshotsList.isVisible({ timeout: 2000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 2,
        title: 'Saved Snapshots',
        description: 'List of saved game state snapshots',
        annotations: [
          { selector: '[data-testid="snapshots"], .snapshots-list', label: 'Snapshots', color: ANNOTATION_COLORS.info },
          { selector: 'button:has-text("Restore"), [data-testid="restore-snapshot"]', label: 'Restore', color: ANNOTATION_COLORS.warning },
        ],
        previousAction: 'View snapshots',
        nextAction: 'Restore snapshot',
      });
    }

    helper.setTotalSteps(2);
    console.log(`\n✅ Snapshots captured: ${helper.getCapturedSteps().length} screenshots`);
  });

  test('end session - completion flow', async ({ page }) => {
    // Step 1: Navigate to session
    await page.goto('/sessions/session-1');
    await waitForStableState(page);

    await helper.capture(page, {
      step: 1,
      title: 'Session to End',
      description: 'Active session ready to be completed',
      nextAction: 'End session',
    });

    // Step 2: End session button
    const endSessionBtn = page.locator('button:has-text("End Session"), button:has-text("Complete"), [data-testid="end-session"]').first();
    if (await endSessionBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 2,
        title: 'End Session',
        description: 'Complete the game session and record final scores',
        annotations: [
          { selector: 'button:has-text("End Session"), [data-testid="end-session"]', label: 'End Session', color: ANNOTATION_COLORS.warning },
        ],
        previousAction: 'Review state',
        nextAction: 'Confirm end',
      });

      await endSessionBtn.click();
      await waitForStableState(page);

      // Step 3: Confirmation/summary
      const summary = page.locator('[role="dialog"], .session-summary, [data-testid="session-complete"]').first();
      if (await summary.isVisible({ timeout: 2000 }).catch(() => false)) {
        await helper.capture(page, {
          step: 3,
          title: 'Session Summary',
          description: 'Final game results and winner declaration',
          annotations: [
            { selector: '[role="dialog"], .session-summary', label: 'Results', color: ANNOTATION_COLORS.success },
          ],
          previousAction: 'End session',
          nextAction: 'Close summary',
        });
      }
    }

    helper.setTotalSteps(3);
    console.log(`\n✅ End session captured: ${helper.getCapturedSteps().length} screenshots`);
  });
});

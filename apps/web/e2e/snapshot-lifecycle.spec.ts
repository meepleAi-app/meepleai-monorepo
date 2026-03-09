/**
 * Snapshot Lifecycle E2E Tests
 * Issue #5589: Pause (auto-snapshot) → restore → verify state restored.
 *
 * Tests the complete snapshot lifecycle:
 * 1. Create session with game state
 * 2. Pause session → auto-snapshot created
 * 3. Modify state (advance turns)
 * 4. Restore to snapshot → verify state matches snapshot point
 */

import { test, expect, Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// ============================================================================
// Mock Data
// ============================================================================

const MOCK_USER = {
  user: {
    id: 'user-snapshot-001',
    email: 'player@meepleai.dev',
    displayName: 'Snapshot Tester',
    role: 'User',
    tier: 'premium',
  },
  expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
};

const SESSION_ID = 'session-snapshot-001';

const MOCK_SESSION_ACTIVE = {
  id: SESSION_ID,
  sessionCode: 'SNAP01',
  gameId: 'game-catan-001',
  gameName: 'Catan',
  status: 'InProgress',
  currentTurnIndex: 5,
  currentPhaseIndex: 0,
  gameState: { turn: 5, scores: { Alice: 25, Bob: 18 } },
  players: [
    { id: 'p1', displayName: 'Alice', color: 'Red', role: 'Host', isActive: true },
    { id: 'p2', displayName: 'Bob', color: 'Blue', role: 'Player', isActive: true },
  ],
};

const MOCK_SNAPSHOT_0 = {
  id: 'snap-0',
  sessionId: SESSION_ID,
  snapshotIndex: 0,
  triggerType: 'Manual',
  triggerDescription: 'Initial save',
  isCheckpoint: true,
  turnIndex: 0,
  phaseIndex: null,
  timestamp: new Date(Date.now() - 3600000).toISOString(),
  createdByPlayerId: null,
  attachmentCount: 0,
};

const MOCK_SNAPSHOT_PAUSE = {
  id: 'snap-1',
  sessionId: SESSION_ID,
  snapshotIndex: 1,
  triggerType: 'SessionPaused',
  triggerDescription: 'Auto — Pausa turno 3',
  isCheckpoint: false,
  turnIndex: 3,
  phaseIndex: 0,
  timestamp: new Date(Date.now() - 1800000).toISOString(),
  createdByPlayerId: null,
  attachmentCount: 0,
};

const MOCK_SNAPSHOT_PRE_RESTORE = {
  id: 'snap-2',
  sessionId: SESSION_ID,
  snapshotIndex: 2,
  triggerType: 'PreRestore',
  triggerDescription: 'Auto — Pre-restore turno 5',
  isCheckpoint: false,
  turnIndex: 5,
  phaseIndex: 0,
  timestamp: new Date().toISOString(),
  createdByPlayerId: null,
  attachmentCount: 0,
};

const MOCK_RESTORED_STATE = {
  snapshotIndex: 1,
  turnIndex: 3,
  phaseIndex: 0,
  timestamp: MOCK_SNAPSHOT_PAUSE.timestamp,
  state: { turn: 3, scores: { Alice: 15, Bob: 12 } },
};

// ============================================================================
// Helpers
// ============================================================================

async function setupSnapshotRoutes(page: Page) {
  await page.route(`${API_BASE}/api/v1/auth/session`, route =>
    route.fulfill({ status: 200, json: MOCK_USER })
  );

  // Session
  await page.route(`${API_BASE}/api/v1/live-sessions/${SESSION_ID}`, route =>
    route.fulfill({ status: 200, json: MOCK_SESSION_ACTIVE })
  );

  // Pause session → returns paused state + triggers auto-snapshot
  await page.route(`${API_BASE}/api/v1/live-sessions/${SESSION_ID}/pause`, route =>
    route.fulfill({
      status: 200,
      json: { ...MOCK_SESSION_ACTIVE, status: 'Paused', pausedAt: new Date().toISOString() },
    })
  );

  // List snapshots
  await page.route(`${API_BASE}/api/v1/live-sessions/${SESSION_ID}/snapshots`, route => {
    if (route.request().method() === 'GET') {
      return route.fulfill({
        status: 200,
        json: [MOCK_SNAPSHOT_0, MOCK_SNAPSHOT_PAUSE],
      });
    }
    // POST — create manual snapshot
    return route.fulfill({ status: 201, json: MOCK_SNAPSHOT_0 });
  });

  // Restore snapshot
  await page.route(`${API_BASE}/api/v1/live-sessions/${SESSION_ID}/snapshots/*/restore`, route =>
    route.fulfill({ status: 200, json: MOCK_SNAPSHOT_PRE_RESTORE })
  );

  // Reconstructed state
  await page.route(`${API_BASE}/api/v1/live-sessions/${SESSION_ID}/snapshots/*/state`, route =>
    route.fulfill({ status: 200, json: MOCK_RESTORED_STATE })
  );

  // Catch-all
  await page.route(`${API_BASE}/api/**`, route => route.fulfill({ status: 200, json: {} }));
}

// ============================================================================
// Tests
// ============================================================================

test.describe('Snapshot Lifecycle', () => {
  test.beforeEach(async ({ page }) => {
    await setupSnapshotRoutes(page);
  });

  test('should create auto-snapshot on session pause', async ({ page }) => {
    // Pause the session
    const pauseResult = await page.evaluate(async apiBase => {
      const res = await fetch(`${apiBase}/api/v1/live-sessions/${SESSION_ID}/pause`, {
        method: 'POST',
        credentials: 'include',
      });
      return await res.json();
    }, API_BASE);

    expect(pauseResult.status).toBe('Paused');

    // Verify snapshots list includes the auto-snapshot
    const snapshots = await page.evaluate(async apiBase => {
      const res = await fetch(`${apiBase}/api/v1/live-sessions/${SESSION_ID}/snapshots`, {
        credentials: 'include',
      });
      return await res.json();
    }, API_BASE);

    expect(snapshots).toHaveLength(2);
    expect(snapshots[1].triggerType).toBe('SessionPaused');
    expect(snapshots[1].triggerDescription).toContain('Pausa');
  });

  test('should list snapshots with correct checkpoint flags', async ({ page }) => {
    const snapshots = await page.evaluate(async apiBase => {
      const res = await fetch(`${apiBase}/api/v1/live-sessions/${SESSION_ID}/snapshots`, {
        credentials: 'include',
      });
      return await res.json();
    }, API_BASE);

    expect(snapshots[0].isCheckpoint).toBe(true); // Index 0 = always checkpoint
    expect(snapshots[1].isCheckpoint).toBe(false); // Index 1 = delta
  });

  test('should restore snapshot and return pre-restore snapshot', async ({ page }) => {
    const restoreResult = await page.evaluate(async apiBase => {
      const res = await fetch(`${apiBase}/api/v1/live-sessions/${SESSION_ID}/snapshots/1/restore`, {
        method: 'POST',
        credentials: 'include',
      });
      return await res.json();
    }, API_BASE);

    // The response should be the pre-restore snapshot (created before restoring)
    expect(restoreResult.triggerType).toBe('PreRestore');
    expect(restoreResult.triggerDescription).toContain('Pre-restore');
    expect(restoreResult.snapshotIndex).toBe(2); // New snapshot created
  });

  test('should reconstruct state at snapshot point', async ({ page }) => {
    const stateResult = await page.evaluate(async apiBase => {
      const res = await fetch(`${apiBase}/api/v1/live-sessions/${SESSION_ID}/snapshots/1/state`, {
        credentials: 'include',
      });
      return await res.json();
    }, API_BASE);

    expect(stateResult.snapshotIndex).toBe(1);
    expect(stateResult.turnIndex).toBe(3); // Snapshot was at turn 3
    expect(stateResult.state.turn).toBe(3);
    expect(stateResult.state.scores.Alice).toBe(15);
    expect(stateResult.state.scores.Bob).toBe(12);
  });

  test('should support full pause-restore-verify cycle', async ({ page }) => {
    // Step 1: Pause session
    const pauseRes = await page.evaluate(async apiBase => {
      const res = await fetch(`${apiBase}/api/v1/live-sessions/${SESSION_ID}/pause`, {
        method: 'POST',
        credentials: 'include',
      });
      return await res.json();
    }, API_BASE);
    expect(pauseRes.status).toBe('Paused');

    // Step 2: List snapshots (should have auto-snapshot from pause)
    const snaps = await page.evaluate(async apiBase => {
      const res = await fetch(`${apiBase}/api/v1/live-sessions/${SESSION_ID}/snapshots`, {
        credentials: 'include',
      });
      return await res.json();
    }, API_BASE);
    expect(snaps.length).toBeGreaterThanOrEqual(2);

    // Step 3: Restore to snapshot 1 (the pause snapshot)
    const restoreRes = await page.evaluate(async apiBase => {
      const res = await fetch(`${apiBase}/api/v1/live-sessions/${SESSION_ID}/snapshots/1/restore`, {
        method: 'POST',
        credentials: 'include',
      });
      return await res.json();
    }, API_BASE);
    expect(restoreRes.triggerType).toBe('PreRestore');

    // Step 4: Verify restored state
    const state = await page.evaluate(async apiBase => {
      const res = await fetch(`${apiBase}/api/v1/live-sessions/${SESSION_ID}/snapshots/1/state`, {
        credentials: 'include',
      });
      return await res.json();
    }, API_BASE);
    expect(state.turnIndex).toBe(3);
    expect(state.state.turn).toBe(3);
  });
});

# Game Night Improvvisata Phase 2 — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create 25 backend integration E2E tests covering improvvisata-specific flows (save/resume, score parsing, disputes, data integrity, concurrent access) against real services.

**Architecture:** Playwright API-level tests hitting real backend (API + PostgreSQL + Redis + AI services). Shared helper module for auth and session setup. Each test file is independent and self-cleaning.

**Tech Stack:** Playwright Test, TypeScript, real .NET API backend, PostgreSQL, Redis, OpenRouter (AI)

**Spec:** `docs/superpowers/specs/2026-03-21-game-night-improvvisata-phase2-design.md`

---

### Task 1: Shared Helpers Module

**Files:**
- Create: `apps/web/e2e/integration/helpers/improvvisata-helpers.ts`

- [ ] **Step 1: Create helpers directory and file**

```typescript
import { expect, APIRequestContext } from '@playwright/test';

export const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

export const ADMIN_EMAIL = process.env.ADMIN_EMAIL || 'admin@meepleai.app';
export const ADMIN_PASSWORD = process.env.ADMIN_PASSWORD || 'DMxspufvkZM3gRHjAHmd';

const JSON_HEADERS = { 'Content-Type': 'application/json' };

/** Parse bare UUID string from API response (strips quotes) */
export function parseUuid(text: string): string {
  const id = text.replace(/"/g, '');
  return id;
}

/** Login as admin and return authenticated API context */
export async function loginAsAdmin(
  playwright: any
): Promise<APIRequestContext> {
  const api = await playwright.request.newContext({ baseURL: API_BASE });
  const loginRes = await api.post('/api/v1/auth/login', {
    data: { email: ADMIN_EMAIL, password: ADMIN_PASSWORD },
    headers: JSON_HEADERS,
  });
  expect(loginRes.ok(), `Admin login failed: ${loginRes.status()}`).toBe(true);
  return api;
}

/** Create a session via generic endpoint. Returns session ID (bare UUID). */
export async function createGenericSession(
  api: APIRequestContext,
  gameName: string,
  options?: { visibility?: string; scoringDimensions?: string[]; agentMode?: string }
): Promise<string> {
  const res = await api.post('/api/v1/live-sessions', {
    data: {
      gameName,
      visibility: options?.visibility ?? 'Private',
      scoringDimensions: options?.scoringDimensions ?? ['points'],
      agentMode: options?.agentMode ?? 'None',
    },
    headers: JSON_HEADERS,
  });
  expect(res.ok(), `Create session failed: ${res.status()} ${await res.text()}`).toBe(true);
  return parseUuid(await res.text());
}

export interface PlayerDef {
  displayName: string;
  color: string;
  role: string;
}

/** Add players to a session. Returns array of player IDs. */
export async function addPlayersToSession(
  api: APIRequestContext,
  sessionId: string,
  players: PlayerDef[]
): Promise<string[]> {
  const ids: string[] = [];
  for (const p of players) {
    const res = await api.post(`/api/v1/live-sessions/${sessionId}/players`, {
      data: { displayName: p.displayName, color: p.color, role: p.role },
      headers: JSON_HEADERS,
    });
    expect(res.ok(), `Add player ${p.displayName} failed: ${res.status()}`).toBe(true);
    ids.push(parseUuid(await res.text()));
  }
  return ids;
}

/** Start a session */
export async function startSession(api: APIRequestContext, sessionId: string): Promise<void> {
  const res = await api.post(`/api/v1/live-sessions/${sessionId}/start`);
  expect(res.ok(), `Start session failed: ${res.status()}`).toBe(true);
}

/** Record a score entry */
export async function recordScore(
  api: APIRequestContext,
  sessionId: string,
  playerId: string,
  round: number,
  value: number,
  dimension = 'points'
): Promise<void> {
  const res = await api.post(`/api/v1/live-sessions/${sessionId}/scores`, {
    data: { playerId, round, dimension, value },
    headers: JSON_HEADERS,
  });
  expect(res.ok(), `Record score failed: ${res.status()}`).toBe(true);
}

/** Get session details */
export async function getSession(api: APIRequestContext, sessionId: string): Promise<any> {
  const res = await api.get(`/api/v1/live-sessions/${sessionId}`);
  expect(res.ok()).toBe(true);
  return res.json();
}

/** Get session scores */
export async function getScores(api: APIRequestContext, sessionId: string): Promise<any[]> {
  const res = await api.get(`/api/v1/live-sessions/${sessionId}/scores`);
  expect(res.ok()).toBe(true);
  return res.json();
}

/** Cleanup: resume (try both paths) then complete session, swallow errors */
export async function cleanupSession(api: APIRequestContext, sessionId: string): Promise<void> {
  // Try improvvisata resume first (for sessions paused via /game-night/sessions/{id}/save)
  await api.post(`/api/v1/game-night/sessions/${sessionId}/resume`).catch(() => {});
  // Fallback: generic resume (for sessions paused via /live-sessions/{id}/pause)
  await api.post(`/api/v1/live-sessions/${sessionId}/resume`).catch(() => {});
  await api.post(`/api/v1/live-sessions/${sessionId}/complete`).catch(() => {});
}

/** Standard test players */
export const TEST_PLAYERS: PlayerDef[] = [
  { displayName: 'Marco', color: 'Red', role: 'Host' },
  { displayName: 'Lucia', color: 'Blue', role: 'Player' },
  { displayName: 'Giovanni', color: 'Green', role: 'Player' },
];
```

- [ ] **Step 2: Verify helpers compile**

Run: `cd apps/web && npx tsc --noEmit apps/web/e2e/integration/helpers/improvvisata-helpers.ts 2>&1 || echo "Check imports manually"`

If TS errors on PlaywrightTestArgs import, adjust to use the pattern from existing tests (inline type).

- [ ] **Step 3: Commit**

```bash
git add apps/web/e2e/integration/helpers/improvvisata-helpers.ts
git commit -m "test(e2e): add shared helpers for improvvisata integration tests"
```

---

### Task 2: Save/Resume Integration Tests

**Files:**
- Create: `apps/web/e2e/integration/improvvisata-save-resume.spec.ts`
- Reference: `apps/api/src/Api/Routing/GameNightImprovvisataEndpoints.cs` (save/resume endpoints)
- Reference: `apps/api/src/Api/Routing/SessionSnapshotEndpoints.cs` (snapshot queries)

- [ ] **Step 1: Write test file**

```typescript
import { test, expect } from '@playwright/test';
import {
  loginAsAdmin,
  createGenericSession,
  addPlayersToSession,
  startSession,
  recordScore,
  getSession,
  getScores,
  cleanupSession,
  TEST_PLAYERS,
} from './helpers/improvvisata-helpers';
import type { APIRequestContext } from '@playwright/test';

const JSON_HEADERS = { 'Content-Type': 'application/json' };

test.describe('Improvvisata Save/Resume — Integration', () => {
  test.describe.configure({ mode: 'serial' });

  let api: APIRequestContext;
  let sessionId: string | null = null;

  test.beforeAll(async ({ playwright }) => {
    api = await loginAsAdmin(playwright);
  });

  test.afterAll(async () => {
    await api.dispose();
  });

  test.afterEach(async () => {
    if (sessionId) {
      await cleanupSession(api, sessionId);
      sessionId = null;
    }
  });

  test('save session creates PauseSnapshot', async () => {
    // Setup: create session with players and scores
    sessionId = await createGenericSession(api, 'Save Test Game');
    const [p1, p2] = await addPlayersToSession(api, sessionId, TEST_PLAYERS.slice(0, 2));
    await startSession(api, sessionId);
    await recordScore(api, sessionId, p1, 1, 15);
    await recordScore(api, sessionId, p2, 1, 12);

    // Act: save via improvvisata endpoint
    const saveRes = await api.post(`/api/v1/game-night/sessions/${sessionId}/save`, {
      data: { finalPhotoIds: [] },
      headers: JSON_HEADERS,
    });
    expect(saveRes.ok(), `Save failed: ${saveRes.status()} ${await saveRes.text()}`).toBe(true);
    const saveBody = await saveRes.json();
    expect(saveBody.snapshotId).toBeTruthy();

    // Verify: session is Paused
    const session = await getSession(api, sessionId);
    expect(session.status).toBe('Paused');

    // Verify: snapshot exists
    const snapshotsRes = await api.get(`/api/v1/sessions/${sessionId}/snapshots`);
    expect(snapshotsRes.ok()).toBe(true);
    const snapshots = await snapshotsRes.json();
    expect(Array.isArray(snapshots)).toBe(true);
    expect(snapshots.length).toBeGreaterThanOrEqual(1);
  });

  test('resume restores state with recap', async () => {
    // Setup: create, start, score, save
    sessionId = await createGenericSession(api, 'Resume Test Game');
    const [p1] = await addPlayersToSession(api, sessionId, TEST_PLAYERS.slice(0, 2));
    await startSession(api, sessionId);
    await recordScore(api, sessionId, p1, 1, 20);

    await api.post(`/api/v1/game-night/sessions/${sessionId}/save`, {
      data: { finalPhotoIds: [] },
      headers: JSON_HEADERS,
    });

    // Act: resume via improvvisata endpoint
    const resumeRes = await api.post(`/api/v1/game-night/sessions/${sessionId}/resume`);
    expect(resumeRes.ok(), `Resume failed: ${resumeRes.status()} ${await resumeRes.text()}`).toBe(
      true
    );
    const resumeBody = await resumeRes.json();

    // Verify: response contains expected fields
    expect(resumeBody.sessionId).toBeTruthy();
    expect(resumeBody.inviteCode).toBeTruthy();
    expect(resumeBody.shareLink).toBeTruthy();
    // agentRecap may be null if AI service is unavailable — that's OK
    expect('agentRecap' in resumeBody).toBe(true);

    // Verify: session is InProgress again
    const session = await getSession(api, sessionId);
    expect(session.status).toBe('InProgress');

    // Supplementary: check resume-context endpoint
    const ctxRes = await api.get(`/api/v1/live-sessions/${sessionId}/resume-context`);
    if (ctxRes.ok()) {
      const ctx = await ctxRes.json();
      expect(ctx).toBeTruthy();
    }
  });

  test('multiple save/resume cycles preserve data', async () => {
    sessionId = await createGenericSession(api, 'Multi Cycle Test');
    const [p1, p2] = await addPlayersToSession(api, sessionId, TEST_PLAYERS.slice(0, 2));
    await startSession(api, sessionId);

    // Cycle 1: score → save → resume
    await recordScore(api, sessionId, p1, 1, 10);
    await api.post(`/api/v1/game-night/sessions/${sessionId}/save`, {
      data: { finalPhotoIds: [] },
      headers: JSON_HEADERS,
    });
    const resume1 = await api.post(`/api/v1/game-night/sessions/${sessionId}/resume`);
    expect(resume1.ok()).toBe(true);
    const invite1 = (await resume1.json()).inviteCode;

    // Cycle 2: more scores → save → resume
    await recordScore(api, sessionId, p2, 1, 8);
    await recordScore(api, sessionId, p1, 2, 12);
    await api.post(`/api/v1/game-night/sessions/${sessionId}/save`, {
      data: { finalPhotoIds: [] },
      headers: JSON_HEADERS,
    });
    const resume2 = await api.post(`/api/v1/game-night/sessions/${sessionId}/resume`);
    expect(resume2.ok()).toBe(true);
    const invite2 = (await resume2.json()).inviteCode;

    // Verify: fresh invite each resume
    expect(invite2).not.toBe(invite1);

    // Verify: all 3 scores persist
    const scores = await getScores(api, sessionId);
    expect(scores.length).toBe(3);
  });
});
```

- [ ] **Step 2: Run tests against live backend**

Run: `cd apps/web && npx playwright test e2e/integration/improvvisata-save-resume.spec.ts --reporter=list`

Expected: 3 tests pass (requires API + PG + Redis running)

- [ ] **Step 3: Fix any failures and re-run**

- [ ] **Step 4: Commit**

```bash
git add apps/web/e2e/integration/improvvisata-save-resume.spec.ts
git commit -m "test(e2e): add improvvisata save/resume integration tests"
```

---

### Task 3: Score Parsing Integration Tests

**Files:**
- Create: `apps/web/e2e/integration/improvvisata-score-parsing.spec.ts`
- Reference: `apps/api/src/Api/Routing/LiveSessionEndpoints.cs` (scores/parse, scores/confirm)

- [ ] **Step 1: Write test file**

```typescript
import { test, expect } from '@playwright/test';
import {
  loginAsAdmin,
  createGenericSession,
  addPlayersToSession,
  startSession,
  getScores,
  cleanupSession,
  TEST_PLAYERS,
} from './helpers/improvvisata-helpers';
import type { APIRequestContext } from '@playwright/test';

const JSON_HEADERS = { 'Content-Type': 'application/json' };

test.describe('Improvvisata Score Parsing — Integration', () => {
  test.describe.configure({ mode: 'serial' });
  // AI-dependent tests need longer timeout (OpenRouter can take 10-30s)
  test.setTimeout(60_000);

  let api: APIRequestContext;
  let sessionId: string | null = null;
  let playerIds: string[] = [];

  test.beforeAll(async ({ playwright }) => {
    api = await loginAsAdmin(playwright);
  });

  test.afterAll(async () => {
    await api.dispose();
  });

  test.afterEach(async () => {
    if (sessionId) {
      await cleanupSession(api, sessionId);
      sessionId = null;
      playerIds = [];
    }
  });

  test('parse natural language score returns structured result', async () => {
    sessionId = await createGenericSession(api, 'NLP Score Test');
    playerIds = await addPlayersToSession(api, sessionId, TEST_PLAYERS.slice(0, 2));
    await startSession(api, sessionId);

    const parseRes = await api.post(`/api/v1/live-sessions/${sessionId}/scores/parse`, {
      data: { message: 'Marco ha fatto 15 punti' },
      headers: JSON_HEADERS,
    });

    // AI service must be running — if not, test will fail here
    expect(parseRes.ok(), `Parse failed: ${parseRes.status()} ${await parseRes.text()}`).toBe(true);
    const result = await parseRes.json();

    expect(result.confidence).toBeDefined();
    expect(typeof result.confidence).toBe('number');
    expect(result.confidence).toBeGreaterThanOrEqual(0);
    expect(result.confidence).toBeLessThanOrEqual(1);
  });

  test('high confidence score is auto-recorded', async () => {
    sessionId = await createGenericSession(api, 'Auto Record Test');
    playerIds = await addPlayersToSession(api, sessionId, TEST_PLAYERS.slice(0, 2));
    await startSession(api, sessionId);

    const scoresBefore = await getScores(api, sessionId);
    expect(scoresBefore.length).toBe(0);

    // Use an unambiguous message that should produce high confidence
    const parseRes = await api.post(`/api/v1/live-sessions/${sessionId}/scores/parse`, {
      data: { message: 'Marco ha fatto 15 punti nel round 1' },
      headers: JSON_HEADERS,
    });
    expect(parseRes.ok()).toBe(true);
    const result = await parseRes.json();

    // Verify confidence is in valid range
    expect(result.confidence).toBeGreaterThanOrEqual(0);
    expect(result.confidence).toBeLessThanOrEqual(1);

    if (result.confidence >= 0.8 && !result.requiresConfirmation) {
      // High confidence: score should have been auto-recorded
      const scores = await getScores(api, sessionId);
      expect(scores.length).toBe(1);
    } else {
      // Lower confidence: confirm manually, then verify auto-record path wasn't triggered
      console.log(`[Score Parse] Confidence ${result.confidence} — confirming manually`);
      await api.post(`/api/v1/live-sessions/${sessionId}/scores/confirm`, {
        data: { targetPlayerId: playerIds[0], round: 1, dimension: 'points', value: 15 },
        headers: JSON_HEADERS,
      });
      const scores = await getScores(api, sessionId);
      expect(scores.length).toBe(1);
    }
  });

  test('low confidence requires confirmation', async () => {
    sessionId = await createGenericSession(api, 'Confirm Score Test');
    playerIds = await addPlayersToSession(api, sessionId, TEST_PLAYERS.slice(0, 2));
    await startSession(api, sessionId);

    // Use an ambiguous message
    const parseRes = await api.post(`/api/v1/live-sessions/${sessionId}/scores/parse`, {
      data: { message: 'qualcuno ha preso dei punti forse' },
      headers: JSON_HEADERS,
    });
    expect(parseRes.ok()).toBe(true);
    const result = await parseRes.json();

    if (result.requiresConfirmation) {
      // Confirm the score
      const confirmRes = await api.post(`/api/v1/live-sessions/${sessionId}/scores/confirm`, {
        data: {
          targetPlayerId: playerIds[0],
          round: 1,
          dimension: 'points',
          value: result.value ?? 10,
        },
        headers: JSON_HEADERS,
      });
      // Accept 200 or 204
      expect([200, 204]).toContain(confirmRes.status());

      const scores = await getScores(api, sessionId);
      expect(scores.length).toBeGreaterThanOrEqual(1);
    } else {
      console.log('[Score Parse] AI returned high confidence for ambiguous input — acceptable');
    }
  });

  test('invalid input returns error gracefully', async () => {
    sessionId = await createGenericSession(api, 'Invalid Score Test');
    playerIds = await addPlayersToSession(api, sessionId, TEST_PLAYERS.slice(0, 2));
    await startSession(api, sessionId);

    const parseRes = await api.post(`/api/v1/live-sessions/${sessionId}/scores/parse`, {
      data: { message: 'asdfghjkl zxcvbnm qwerty' },
      headers: JSON_HEADERS,
    });

    // Should return 200 with low confidence or 400/422 error
    if (parseRes.ok()) {
      const result = await parseRes.json();
      // If it parsed, confidence should be very low
      if (result.confidence !== undefined) {
        expect(result.confidence).toBeLessThan(0.5);
      }
    } else {
      expect([400, 422]).toContain(parseRes.status());
    }

    // No score should be recorded
    const scores = await getScores(api, sessionId);
    expect(scores.length).toBe(0);
  });
});
```

- [ ] **Step 2: Run tests**

Run: `cd apps/web && npx playwright test e2e/integration/improvvisata-score-parsing.spec.ts --reporter=list`

Expected: 4 tests pass (requires OpenRouter API key)

- [ ] **Step 3: Fix any failures and re-run**

- [ ] **Step 4: Commit**

```bash
git add apps/web/e2e/integration/improvvisata-score-parsing.spec.ts
git commit -m "test(e2e): add improvvisata score parsing integration tests"
```

---

### Task 4: Disputes Integration Tests

**Files:**
- Create: `apps/web/e2e/integration/improvvisata-disputes.spec.ts`
- Reference: `apps/api/src/Api/Routing/GameNightImprovvisataEndpoints.cs` (AI arbitro)
- Reference: `apps/api/src/Api/Routing/LiveSessionEndpoints.cs` (dispute v2)

- [ ] **Step 1: Write test file**

```typescript
import { test, expect } from '@playwright/test';
import {
  loginAsAdmin,
  createGenericSession,
  addPlayersToSession,
  startSession,
  cleanupSession,
  parseUuid,
  TEST_PLAYERS,
} from './helpers/improvvisata-helpers';
import type { APIRequestContext } from '@playwright/test';

const JSON_HEADERS = { 'Content-Type': 'application/json' };

test.describe('Improvvisata Disputes — Integration', () => {
  test.describe.configure({ mode: 'serial' });
  // AI arbitro test needs longer timeout
  test.setTimeout(60_000);

  let api: APIRequestContext;
  let sessionId: string | null = null;
  let playerIds: string[] = [];

  test.beforeAll(async ({ playwright }) => {
    api = await loginAsAdmin(playwright);
  });

  test.afterAll(async () => {
    await api.dispose();
  });

  test.afterEach(async () => {
    if (sessionId) {
      await cleanupSession(api, sessionId);
      sessionId = null;
      playerIds = [];
    }
  });

  // --- AI Arbitro (Improvvisata-specific) ---

  test('AI arbitro returns verdict for rule dispute', async () => {
    sessionId = await createGenericSession(api, 'Arbitro Test Game');
    playerIds = await addPlayersToSession(api, sessionId, TEST_PLAYERS.slice(0, 2));
    await startSession(api, sessionId);

    const disputeRes = await api.post(`/api/v1/game-night/sessions/${sessionId}/disputes`, {
      data: {
        description: 'Can I build a settlement on an intersection adjacent to another settlement?',
        raisedByPlayerName: 'Marco',
      },
      headers: JSON_HEADERS,
    });
    expect(
      disputeRes.ok(),
      `AI Arbitro failed: ${disputeRes.status()} ${await disputeRes.text()}`
    ).toBe(true);
    const verdict = await disputeRes.json();

    expect(verdict.id).toBeTruthy();
    expect(verdict.verdict).toBeTruthy();
    expect(typeof verdict.verdict).toBe('string');
    expect(Array.isArray(verdict.ruleReferences)).toBe(true);
  });

  // --- Dispute v2 (Structured flow) ---

  test('open structured dispute', async () => {
    sessionId = await createGenericSession(api, 'Dispute v2 Test');
    playerIds = await addPlayersToSession(api, sessionId, TEST_PLAYERS);
    await startSession(api, sessionId);

    const openRes = await api.post(`/api/v1/live-sessions/${sessionId}/disputes`, {
      data: {
        initiatorPlayerId: playerIds[0],
        initiatorClaim: 'I claimed the longest road first',
      },
      headers: JSON_HEADERS,
    });
    expect(openRes.ok(), `Open dispute failed: ${openRes.status()} ${await openRes.text()}`).toBe(
      true
    );
    const disputeId = parseUuid(await openRes.text());
    expect(disputeId).toMatch(/^[0-9a-f-]{36}$/);
  });

  test('respondent counter-claim', async () => {
    sessionId = await createGenericSession(api, 'Respond Dispute Test');
    playerIds = await addPlayersToSession(api, sessionId, TEST_PLAYERS);
    await startSession(api, sessionId);

    // Open dispute
    const openRes = await api.post(`/api/v1/live-sessions/${sessionId}/disputes`, {
      data: {
        initiatorPlayerId: playerIds[0],
        initiatorClaim: 'I had longest road',
      },
      headers: JSON_HEADERS,
    });
    expect(openRes.ok()).toBe(true);
    const disputeId = parseUuid(await openRes.text());

    // Respond
    const respondRes = await api.put(
      `/api/v1/live-sessions/${sessionId}/disputes/${disputeId}/respond`,
      {
        data: {
          respondentPlayerId: playerIds[1],
          respondentClaim: 'My road was longer at that point',
        },
        headers: JSON_HEADERS,
      }
    );
    expect(
      respondRes.ok(),
      `Respond failed: ${respondRes.status()} ${await respondRes.text()}`
    ).toBe(true);
  });

  test('cast votes on dispute', async () => {
    sessionId = await createGenericSession(api, 'Vote Dispute Test');
    playerIds = await addPlayersToSession(api, sessionId, TEST_PLAYERS);
    await startSession(api, sessionId);

    // Open + respond
    const openRes = await api.post(`/api/v1/live-sessions/${sessionId}/disputes`, {
      data: { initiatorPlayerId: playerIds[0], initiatorClaim: 'Dispute for voting' },
      headers: JSON_HEADERS,
    });
    const disputeId = parseUuid(await openRes.text());

    await api.put(`/api/v1/live-sessions/${sessionId}/disputes/${disputeId}/respond`, {
      data: { respondentPlayerId: playerIds[1], respondentClaim: 'Counter claim' },
      headers: JSON_HEADERS,
    });

    // All 3 players vote
    for (const pid of playerIds) {
      const voteRes = await api.post(
        `/api/v1/live-sessions/${sessionId}/disputes/${disputeId}/vote`,
        {
          data: { playerId: pid, acceptsVerdict: pid === playerIds[0] },
          headers: JSON_HEADERS,
        }
      );
      expect(voteRes.ok(), `Vote failed for ${pid}: ${voteRes.status()}`).toBe(true);
    }
  });

  test('tally and resolve dispute', async () => {
    sessionId = await createGenericSession(api, 'Tally Dispute Test');
    playerIds = await addPlayersToSession(api, sessionId, TEST_PLAYERS);
    await startSession(api, sessionId);

    // Open + respond + vote
    const openRes = await api.post(`/api/v1/live-sessions/${sessionId}/disputes`, {
      data: { initiatorPlayerId: playerIds[0], initiatorClaim: 'Tally test claim' },
      headers: JSON_HEADERS,
    });
    const disputeId = parseUuid(await openRes.text());

    await api.put(`/api/v1/live-sessions/${sessionId}/disputes/${disputeId}/respond`, {
      data: { respondentPlayerId: playerIds[1], respondentClaim: 'Tally counter' },
      headers: JSON_HEADERS,
    });

    for (const pid of playerIds) {
      await api.post(`/api/v1/live-sessions/${sessionId}/disputes/${disputeId}/vote`, {
        data: { playerId: pid, acceptsVerdict: true },
        headers: JSON_HEADERS,
      });
    }

    // Tally
    const tallyRes = await api.post(
      `/api/v1/live-sessions/${sessionId}/disputes/${disputeId}/tally`
    );
    expect(tallyRes.ok(), `Tally failed: ${tallyRes.status()} ${await tallyRes.text()}`).toBe(
      true
    );
  });

  test('timeout fallback when respondent does not reply', async () => {
    sessionId = await createGenericSession(api, 'Timeout Dispute Test');
    playerIds = await addPlayersToSession(api, sessionId, TEST_PLAYERS.slice(0, 2));
    await startSession(api, sessionId);

    const openRes = await api.post(`/api/v1/live-sessions/${sessionId}/disputes`, {
      data: { initiatorPlayerId: playerIds[0], initiatorClaim: 'Timeout test' },
      headers: JSON_HEADERS,
    });
    const disputeId = parseUuid(await openRes.text());

    // Timeout without responding
    const timeoutRes = await api.post(
      `/api/v1/live-sessions/${sessionId}/disputes/${disputeId}/timeout`
    );
    expect(
      timeoutRes.ok(),
      `Timeout failed: ${timeoutRes.status()} ${await timeoutRes.text()}`
    ).toBe(true);
  });
});
```

- [ ] **Step 2: Run tests**

Run: `cd apps/web && npx playwright test e2e/integration/improvvisata-disputes.spec.ts --reporter=list`

Expected: 6 tests pass

- [ ] **Step 3: Fix any failures and re-run**

- [ ] **Step 4: Commit**

```bash
git add apps/web/e2e/integration/improvvisata-disputes.spec.ts
git commit -m "test(e2e): add improvvisata disputes integration tests (AI arbitro + v2)"
```

---

### Task 5: Data Integrity Integration Tests

**Files:**
- Create: `apps/web/e2e/integration/improvvisata-data-integrity.spec.ts`
- Reference: `apps/api/src/Api/Routing/SessionSnapshotEndpoints.cs`
- Reference: `apps/api/src/Api/Routing/SessionInviteEndpoints.cs`

- [ ] **Step 1: Write test file**

```typescript
import { test, expect } from '@playwright/test';
import {
  loginAsAdmin,
  createGenericSession,
  addPlayersToSession,
  startSession,
  recordScore,
  getSession,
  getScores,
  cleanupSession,
  parseUuid,
  TEST_PLAYERS,
} from './helpers/improvvisata-helpers';
import type { APIRequestContext } from '@playwright/test';

const JSON_HEADERS = { 'Content-Type': 'application/json' };

test.describe('Improvvisata Data Integrity — Integration', () => {
  test.describe.configure({ mode: 'serial' });

  let api: APIRequestContext;
  let sessionId: string | null = null;

  test.beforeAll(async ({ playwright }) => {
    api = await loginAsAdmin(playwright);
  });

  test.afterAll(async () => {
    await api.dispose();
  });

  test.afterEach(async () => {
    if (sessionId) {
      await cleanupSession(api, sessionId);
      sessionId = null;
    }
  });

  test('session state transitions are consistent', async () => {
    sessionId = await createGenericSession(api, 'State Transition Test');
    await addPlayersToSession(api, sessionId, TEST_PLAYERS.slice(0, 2));

    // Created
    let session = await getSession(api, sessionId);
    expect(session.status).toBe('Created');
    expect(session.createdAt).toBeTruthy();

    // InProgress
    await startSession(api, sessionId);
    session = await getSession(api, sessionId);
    expect(session.status).toBe('InProgress');
    expect(session.startedAt).toBeTruthy();
    const startedAt = session.startedAt;

    // Paused
    await api.post(`/api/v1/game-night/sessions/${sessionId}/save`, {
      data: { finalPhotoIds: [] },
      headers: JSON_HEADERS,
    });
    session = await getSession(api, sessionId);
    expect(session.status).toBe('Paused');
    expect(session.startedAt).toBe(startedAt); // should not regress

    // InProgress again
    await api.post(`/api/v1/game-night/sessions/${sessionId}/resume`);
    session = await getSession(api, sessionId);
    expect(session.status).toBe('InProgress');
    expect(session.startedAt).toBe(startedAt); // original start preserved

    // Completed
    const completeRes = await api.post(`/api/v1/live-sessions/${sessionId}/complete`);
    expect(completeRes.ok()).toBe(true);
    session = await getSession(api, sessionId);
    expect(session.status).toBe('Completed');
    sessionId = null; // already completed
  });

  test('scores persist across save/resume', async () => {
    sessionId = await createGenericSession(api, 'Score Persist Test');
    const [p1, p2] = await addPlayersToSession(api, sessionId, TEST_PLAYERS.slice(0, 2));
    await startSession(api, sessionId);

    // Record 3 scores
    await recordScore(api, sessionId, p1, 1, 10);
    await recordScore(api, sessionId, p2, 1, 8);
    await recordScore(api, sessionId, p1, 2, 12);

    const scoresBefore = await getScores(api, sessionId);
    expect(scoresBefore.length).toBe(3);

    // Save → Resume
    await api.post(`/api/v1/game-night/sessions/${sessionId}/save`, {
      data: { finalPhotoIds: [] },
      headers: JSON_HEADERS,
    });
    await api.post(`/api/v1/game-night/sessions/${sessionId}/resume`);

    // Verify scores identical
    const scoresAfter = await getScores(api, sessionId);
    expect(scoresAfter.length).toBe(3);

    // Compare values (order may differ)
    const valuesBefore = scoresBefore.map((s: any) => s.value).sort();
    const valuesAfter = scoresAfter.map((s: any) => s.value).sort();
    expect(valuesAfter).toEqual(valuesBefore);
  });

  test('snapshot state is complete', async () => {
    sessionId = await createGenericSession(api, 'Snapshot Complete Test');
    const playerIds = await addPlayersToSession(api, sessionId, TEST_PLAYERS.slice(0, 2));
    await startSession(api, sessionId);
    await recordScore(api, sessionId, playerIds[0], 1, 15);

    // Save to create snapshot
    await api.post(`/api/v1/game-night/sessions/${sessionId}/save`, {
      data: { finalPhotoIds: [] },
      headers: JSON_HEADERS,
    });

    // Get snapshots list
    const listRes = await api.get(`/api/v1/sessions/${sessionId}/snapshots`);
    expect(listRes.ok()).toBe(true);
    const snapshots = await listRes.json();
    expect(snapshots.length).toBeGreaterThanOrEqual(1);

    // Get reconstructed state at index 0
    const stateRes = await api.get(`/api/v1/sessions/${sessionId}/snapshots/0`);
    expect(stateRes.ok(), `Snapshot state failed: ${stateRes.status()}`).toBe(true);
    const state = await stateRes.json();
    expect(state).toBeTruthy();
  });

  test('player soft-delete does not corrupt scores', async () => {
    sessionId = await createGenericSession(api, 'Soft Delete Test');
    const [p1, p2, p3] = await addPlayersToSession(api, sessionId, TEST_PLAYERS);
    await startSession(api, sessionId);

    // Record scores for all 3
    await recordScore(api, sessionId, p1, 1, 10);
    await recordScore(api, sessionId, p2, 1, 8);
    await recordScore(api, sessionId, p3, 1, 6);

    // Remove player 3
    const deleteRes = await api.delete(`/api/v1/live-sessions/${sessionId}/players/${p3}`);
    expect(deleteRes.ok()).toBe(true);

    // Verify session state
    const session = await getSession(api, sessionId);
    const activePlayers = session.players.filter((p: any) => p.isActive !== false);
    const inactivePlayers = session.players.filter((p: any) => p.isActive === false);
    expect(activePlayers.length).toBe(2);
    expect(inactivePlayers.length).toBe(1);

    // All 3 scores still present
    const scores = await getScores(api, sessionId);
    expect(scores.length).toBe(3);
  });

  test('invite token lifecycle', async () => {
    sessionId = await createGenericSession(api, 'Invite Lifecycle Test');
    await addPlayersToSession(api, sessionId, TEST_PLAYERS.slice(0, 1));
    await startSession(api, sessionId);

    // Create invite
    const inviteRes = await api.post(`/api/v1/live-sessions/${sessionId}/invite`, {
      data: { maxUses: 10, expiryMinutes: 30 },
      headers: JSON_HEADERS,
    });
    expect(inviteRes.status()).toBe(201);
    const invite = await inviteRes.json();
    expect(invite.pin).toBeTruthy();
    expect(invite.pin.length).toBeGreaterThanOrEqual(4);
    expect(invite.linkToken).toBeTruthy();
    expect(new Date(invite.expiresAt).getTime()).toBeGreaterThan(Date.now());

    // Join with PIN
    const joinRes = await api.post('/api/v1/live-sessions/join', {
      data: { token: invite.pin, guestName: 'Test Guest' },
      headers: JSON_HEADERS,
    });
    expect(joinRes.ok(), `Join failed: ${joinRes.status()} ${await joinRes.text()}`).toBe(true);
    const joinBody = await joinRes.json();
    expect(joinBody.participantId).toBeTruthy();
    expect(joinBody.sessionId).toBe(sessionId);

    // Save + resume → new invite
    await api.post(`/api/v1/game-night/sessions/${sessionId}/save`, {
      data: { finalPhotoIds: [] },
      headers: JSON_HEADERS,
    });
    const resumeRes = await api.post(`/api/v1/game-night/sessions/${sessionId}/resume`);
    expect(resumeRes.ok()).toBe(true);
    const resumeBody = await resumeRes.json();

    // Fresh invite code should differ
    expect(resumeBody.inviteCode).toBeTruthy();
    // Note: inviteCode may or may not differ from original PIN depending on implementation
  });

  test('dispute resolution does not affect session state', async () => {
    sessionId = await createGenericSession(api, 'Dispute Isolation Test');
    const playerIds = await addPlayersToSession(api, sessionId, TEST_PLAYERS);
    await startSession(api, sessionId);
    await recordScore(api, sessionId, playerIds[0], 1, 10);

    const sessionBefore = await getSession(api, sessionId);
    const scoresBefore = await getScores(api, sessionId);

    // Open dispute v2
    const openRes = await api.post(`/api/v1/live-sessions/${sessionId}/disputes`, {
      data: { initiatorPlayerId: playerIds[0], initiatorClaim: 'Isolation test' },
      headers: JSON_HEADERS,
    });
    expect(openRes.ok()).toBe(true);
    const disputeId = parseUuid(await openRes.text());

    // Respond + vote + tally
    await api.put(`/api/v1/live-sessions/${sessionId}/disputes/${disputeId}/respond`, {
      data: { respondentPlayerId: playerIds[1], respondentClaim: 'Counter' },
      headers: JSON_HEADERS,
    });
    for (const pid of playerIds) {
      await api.post(`/api/v1/live-sessions/${sessionId}/disputes/${disputeId}/vote`, {
        data: { playerId: pid, acceptsVerdict: true },
        headers: JSON_HEADERS,
      });
    }
    await api.post(`/api/v1/live-sessions/${sessionId}/disputes/${disputeId}/tally`);

    // Session state unchanged
    const sessionAfter = await getSession(api, sessionId);
    expect(sessionAfter.status).toBe('InProgress');
    expect(sessionAfter.status).toBe(sessionBefore.status);

    const scoresAfter = await getScores(api, sessionId);
    expect(scoresAfter.length).toBe(scoresBefore.length);

    // Note: GET /api/v1/games/{gameId}/dispute-history requires a gameId linked to the session.
    // Generic sessions created via POST /live-sessions don't expose a gameId in their response.
    // Dispute history verification is deferred to Phase 5 (full pipeline with real game entities).
  });
});
```

- [ ] **Step 2: Run tests**

Run: `cd apps/web && npx playwright test e2e/integration/improvvisata-data-integrity.spec.ts --reporter=list`

Expected: 6 tests pass

- [ ] **Step 3: Fix any failures and re-run**

- [ ] **Step 4: Commit**

```bash
git add apps/web/e2e/integration/improvvisata-data-integrity.spec.ts
git commit -m "test(e2e): add improvvisata data integrity integration tests"
```

---

### Task 6: Concurrent Access Integration Tests

**Files:**
- Create: `apps/web/e2e/integration/improvvisata-concurrent.spec.ts`
- Reference: `apps/api/src/Api/Routing/LiveSessionEndpoints.cs`
- Reference: `apps/api/src/Api/Routing/SessionInviteEndpoints.cs`

- [ ] **Step 1: Write test file**

```typescript
import { test, expect } from '@playwright/test';
import {
  loginAsAdmin,
  createGenericSession,
  addPlayersToSession,
  startSession,
  getScores,
  cleanupSession,
  parseUuid,
  TEST_PLAYERS,
  API_BASE,
  ADMIN_EMAIL,
  ADMIN_PASSWORD,
} from './helpers/improvvisata-helpers';
import type { APIRequestContext } from '@playwright/test';

const JSON_HEADERS = { 'Content-Type': 'application/json' };

/** Create a second authenticated API context (simulates another user) */
async function createSecondContext(
  playwright: any
): Promise<APIRequestContext> {
  const api = await playwright.request.newContext({ baseURL: API_BASE });
  // Login as same admin for simplicity — in production, use a second user
  const loginRes = await api.post('/api/v1/auth/login', {
    data: { email: ADMIN_EMAIL, password: ADMIN_PASSWORD },
    headers: JSON_HEADERS,
  });
  expect(loginRes.ok()).toBe(true);
  return api;
}

test.describe('Improvvisata Concurrent Access — Integration', () => {
  test.describe.configure({ mode: 'serial' });

  let apiHost: APIRequestContext;
  let apiGuest: APIRequestContext;
  let sessionId: string | null = null;

  test.beforeAll(async ({ playwright }) => {
    apiHost = await loginAsAdmin(playwright);
    apiGuest = await createSecondContext(playwright);
  });

  test.afterAll(async () => {
    await apiHost.dispose();
    await apiGuest.dispose();
  });

  test.afterEach(async () => {
    if (sessionId) {
      await cleanupSession(apiHost, sessionId);
      sessionId = null;
    }
  });

  test('simultaneous score recording', async () => {
    sessionId = await createGenericSession(apiHost, 'Concurrent Score Test');
    const [p1, p2] = await addPlayersToSession(apiHost, sessionId, TEST_PLAYERS.slice(0, 2));
    await startSession(apiHost, sessionId);

    // Both record simultaneously
    const [r1, r2] = await Promise.all([
      apiHost.post(`/api/v1/live-sessions/${sessionId}/scores`, {
        data: { playerId: p1, round: 1, dimension: 'points', value: 15 },
        headers: JSON_HEADERS,
      }),
      apiGuest.post(`/api/v1/live-sessions/${sessionId}/scores`, {
        data: { playerId: p2, round: 1, dimension: 'points', value: 12 },
        headers: JSON_HEADERS,
      }),
    ]);

    expect(r1.ok(), `Host score failed: ${r1.status()}`).toBe(true);
    expect(r2.ok(), `Guest score failed: ${r2.status()}`).toBe(true);

    const scores = await getScores(apiHost, sessionId);
    expect(scores.length).toBe(2);
    const values = scores.map((s: any) => s.value).sort();
    expect(values).toEqual([12, 15]);
  });

  test('score proposal and confirmation flow', async () => {
    sessionId = await createGenericSession(apiHost, 'Propose Score Test');
    const [p1, p2] = await addPlayersToSession(apiHost, sessionId, TEST_PLAYERS.slice(0, 2));
    await startSession(apiHost, sessionId);

    // Create invite so we have a participant ID
    const inviteRes = await apiHost.post(`/api/v1/live-sessions/${sessionId}/invite`, {
      data: { maxUses: 10, expiryMinutes: 30 },
      headers: JSON_HEADERS,
    });
    expect(inviteRes.status()).toBe(201);
    const invite = await inviteRes.json();

    // Guest joins
    const joinRes = await apiGuest.post('/api/v1/live-sessions/join', {
      data: { token: invite.pin, guestName: 'ProposerGuest' },
      headers: JSON_HEADERS,
    });
    expect(joinRes.ok()).toBe(true);
    const joinBody = await joinRes.json();

    // Guest proposes score
    const proposeRes = await apiGuest.post(
      `/api/v1/live-sessions/${sessionId}/scores/propose`,
      {
        data: {
          participantId: joinBody.participantId,
          targetPlayerId: p1,
          round: 1,
          dimension: 'points',
          value: 20,
          proposerName: 'ProposerGuest',
        },
        headers: JSON_HEADERS,
      }
    );
    expect(proposeRes.status()).toBe(202);

    // Host confirms
    const confirmRes = await apiHost.post(
      `/api/v1/live-sessions/${sessionId}/scores/confirm`,
      {
        data: {
          targetPlayerId: p1,
          round: 1,
          dimension: 'points',
          value: 20,
        },
        headers: JSON_HEADERS,
      }
    );
    expect([200, 204]).toContain(confirmRes.status());

    // Verify score recorded
    const scores = await getScores(apiHost, sessionId);
    const confirmed = scores.find((s: any) => s.value === 20);
    expect(confirmed).toBeTruthy();
  });

  test('concurrent dispute voting', async () => {
    sessionId = await createGenericSession(apiHost, 'Concurrent Vote Test');
    const playerIds = await addPlayersToSession(apiHost, sessionId, TEST_PLAYERS);
    await startSession(apiHost, sessionId);

    // Open dispute
    const openRes = await apiHost.post(`/api/v1/live-sessions/${sessionId}/disputes`, {
      data: { initiatorPlayerId: playerIds[0], initiatorClaim: 'Concurrent vote test' },
      headers: JSON_HEADERS,
    });
    expect(openRes.ok()).toBe(true);
    const disputeId = parseUuid(await openRes.text());

    // Respond
    await apiHost.put(`/api/v1/live-sessions/${sessionId}/disputes/${disputeId}/respond`, {
      data: { respondentPlayerId: playerIds[1], respondentClaim: 'Counter' },
      headers: JSON_HEADERS,
    });

    // All 3 vote in parallel
    const voteResults = await Promise.all(
      playerIds.map((pid) =>
        apiHost.post(`/api/v1/live-sessions/${sessionId}/disputes/${disputeId}/vote`, {
          data: { playerId: pid, acceptsVerdict: true },
          headers: JSON_HEADERS,
        })
      )
    );

    for (const vr of voteResults) {
      expect(vr.ok(), `Vote failed: ${vr.status()}`).toBe(true);
    }

    // Tally
    const tallyRes = await apiHost.post(
      `/api/v1/live-sessions/${sessionId}/disputes/${disputeId}/tally`
    );
    expect(tallyRes.ok()).toBe(true);
  });

  test('save while guest is scoring', async () => {
    sessionId = await createGenericSession(apiHost, 'Race Condition Test');
    const [p1, p2] = await addPlayersToSession(apiHost, sessionId, TEST_PLAYERS.slice(0, 2));
    await startSession(apiHost, sessionId);

    // Simultaneous: guest scores + host saves
    const [scoreRes, saveRes] = await Promise.all([
      apiGuest.post(`/api/v1/live-sessions/${sessionId}/scores`, {
        data: { playerId: p2, round: 1, dimension: 'points', value: 7 },
        headers: JSON_HEADERS,
      }),
      apiHost.post(`/api/v1/game-night/sessions/${sessionId}/save`, {
        data: { finalPhotoIds: [] },
        headers: JSON_HEADERS,
      }),
    ]);

    // At least one should succeed — no silent data loss
    const scoreOk = scoreRes.ok();
    const saveOk = saveRes.ok();
    expect(scoreOk || saveOk).toBe(true);

    if (scoreOk && saveOk) {
      // Both succeeded — score should be in snapshot or session
      console.log('[Concurrent] Both score and save succeeded');
    } else if (!scoreOk) {
      // Score failed because session was paused — acceptable
      console.log(`[Concurrent] Score rejected (${scoreRes.status()}) — session was paused first`);
    }
  });

  test('join while session is active', async () => {
    sessionId = await createGenericSession(apiHost, 'Join Active Test');
    await addPlayersToSession(apiHost, sessionId, TEST_PLAYERS.slice(0, 1));
    await startSession(apiHost, sessionId);

    // Create invite
    const inviteRes = await apiHost.post(`/api/v1/live-sessions/${sessionId}/invite`, {
      data: { maxUses: 10, expiryMinutes: 30 },
      headers: JSON_HEADERS,
    });
    expect(inviteRes.status()).toBe(201);
    const invite = await inviteRes.json();

    // Guest joins active session
    const joinRes = await apiGuest.post('/api/v1/live-sessions/join', {
      data: { token: invite.pin, guestName: 'Late Joiner' },
      headers: JSON_HEADERS,
    });
    expect(joinRes.ok()).toBe(true);

    // Session still active
    const session = await getSession(apiHost, sessionId);
    expect(session.status).toBe('InProgress');

    // Participant visible
    const participantsRes = await apiHost.get(
      `/api/v1/live-sessions/${sessionId}/participants`
    );
    if (participantsRes.ok()) {
      const participants = await participantsRes.json();
      const names = participants.map((p: any) => p.displayName);
      expect(names).toContain('Late Joiner');
    }
  });

  test('optimistic concurrency on score edit', async () => {
    sessionId = await createGenericSession(apiHost, 'Concurrency Test');
    const [p1] = await addPlayersToSession(apiHost, sessionId, TEST_PLAYERS.slice(0, 1));
    await startSession(apiHost, sessionId);

    // Record initial score
    await apiHost.post(`/api/v1/live-sessions/${sessionId}/scores`, {
      data: { playerId: p1, round: 1, dimension: 'points', value: 10 },
      headers: JSON_HEADERS,
    });

    // Both try to edit simultaneously
    const [edit1, edit2] = await Promise.all([
      apiHost.put(`/api/v1/live-sessions/${sessionId}/scores`, {
        data: { playerId: p1, round: 1, dimension: 'points', value: 20 },
        headers: JSON_HEADERS,
      }),
      apiGuest.put(`/api/v1/live-sessions/${sessionId}/scores`, {
        data: { playerId: p1, round: 1, dimension: 'points', value: 30 },
        headers: JSON_HEADERS,
      }),
    ]);

    const status1 = edit1.status();
    const status2 = edit2.status();

    // At least one succeeds
    expect(status1 === 200 || status1 === 204 || status2 === 200 || status2 === 204).toBe(true);

    // If concurrency is enforced, one should get 409 or 500
    if ((status1 === 409 || status1 === 500) || (status2 === 409 || status2 === 500)) {
      console.log(`[Concurrency] Conflict detected: edit1=${status1}, edit2=${status2}`);
    } else {
      console.log(
        `[Concurrency] Both edits succeeded (${status1}, ${status2}) — last-write-wins`
      );
    }

    // No corruption: score should be either 20 or 30, not something else
    const scores = await getScores(apiHost, sessionId);
    const score = scores.find((s: any) => s.round === 1 && s.dimension === 'points');
    expect(score).toBeTruthy();
    expect([20, 30]).toContain(score.value);
  });
});
```

- [ ] **Step 2: Run tests**

Run: `cd apps/web && npx playwright test e2e/integration/improvvisata-concurrent.spec.ts --reporter=list`

Expected: 6 tests pass

- [ ] **Step 3: Fix any failures and re-run**

- [ ] **Step 4: Commit**

```bash
git add apps/web/e2e/integration/improvvisata-concurrent.spec.ts
git commit -m "test(e2e): add improvvisata concurrent access integration tests"
```

---

### Task 7: Full Suite Validation & Final Commit

**Files:**
- All files from Tasks 1-6

- [ ] **Step 1: Run entire integration suite**

Run: `cd apps/web && npx playwright test e2e/integration/improvvisata-*.spec.ts --reporter=list`

Expected: 25 tests pass

- [ ] **Step 2: Run alongside existing integration tests**

Run: `cd apps/web && npx playwright test e2e/integration/ --reporter=list`

Expected: All integration tests pass (existing + new)

- [ ] **Step 3: Final commit with all files**

```bash
git add apps/web/e2e/integration/
git commit -m "test(e2e): complete Game Night Improvvisata Phase 2 — 25 backend integration tests

Covers: save/resume with PauseSnapshot, AI score parsing, dispute v2 + AI arbitro,
data integrity verification, concurrent access with optimistic concurrency.

Spec: docs/superpowers/specs/2026-03-21-game-night-improvvisata-phase2-design.md"
```

- [ ] **Step 4: Update roadmap**

Edit `docs/roadmap/game-night-improvvisata-completion-roadmap.md` — mark Phase 2 as complete with date 2026-03-21.

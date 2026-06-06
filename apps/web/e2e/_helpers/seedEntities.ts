/**
 * Issue #1928 Task B (DEC-B-2 + DEC-B-5 + DEC-B-8) — TypeScript factory wrapper
 * for E2E entity seeding via admin endpoint POST /api/v1/admin/test/seed/*.
 *
 * **Contract**:
 *   - Caller pre-seeds admin session via `seedAuthSession(page, { role: 'admin' })`
 *   - All factory calls require `testRunId` (forced via API, DEC-B-5)
 *   - `cleanupTestEntities` MUST be called in `test.afterEach` (DEC-B-3)
 *
 * **Triple gate enforced backend-side** (DEC-B-4):
 *   - env `E2E_SEEDING_ENABLED=true`
 *   - `ASPNETCORE_ENVIRONMENT != Production` (startup fail-fast)
 *   - `RequireAdminSessionFilter`
 *
 * Backend ref: `apps/api/src/Api/Routing/Admin/AdminTestSeedEndpoints.cs`
 */
import type { Page } from '@playwright/test';

const SEED_BASE = '/api/v1/admin/test/seed';

export type GameNightStatus = 'Draft' | 'Published' | 'InProgress' | 'Completed';
export type ScoringType = 'Points' | 'BinaryWin' | 'Objectives' | 'Ranking';
export type PlayerRole = 'host' | 'player' | 'guest';

export interface SeedGameNightResponse {
  gameNightId: string;
  ownerId: string;
  testRunId: string;
}

export interface SeedSessionResponse {
  sessionId: string;
  gameNightId: string;
  isLive: boolean;
  testRunId: string;
}

export interface SeedPlayerResponse {
  playerId: string;
  gameNightId: string;
  role: PlayerRole;
  isGuest: boolean;
  testRunId: string;
}

export interface SeedLibraryGameResponse {
  gameId: string;
  libraryEntryId: string;
  ownerId: string;
  testRunId: string;
}

export interface CleanupResponse {
  testRunId: string;
  deletedGameNights: number;
  deletedSessions: number;
  deletedInvitations: number;
  deletedRsvps: number;
  deletedUsers: number;
  deletedLibraryEntries: number;
  deletedSharedGames: number;
  durationMs: number;
}

/**
 * Generates canonical testRunId format: `e2e-{testId}-{epochMs}`.
 * Pass `test.info().testId` from Playwright fixture.
 */
export function newTestRunId(testId: string): string {
  const cleanId = testId
    .replace(/[^a-zA-Z0-9]/g, '')
    .slice(0, 32)
    .padEnd(8, '0');
  return `e2e-${cleanId}-${Date.now()}`;
}

export async function seedGameNight(
  page: Page,
  opts: {
    testRunId: string;
    status: GameNightStatus;
    ownerEmail: string;
    scoringType?: ScoringType;
    rosterCount?: number;
  }
): Promise<SeedGameNightResponse> {
  const response = await page.request.post(`${SEED_BASE}/game-night`, {
    data: opts,
  });
  if (!response.ok()) {
    const body = await response.text();
    throw new Error(`seedGameNight failed (${response.status()}): ${body}`);
  }
  return (await response.json()) as SeedGameNightResponse;
}

export async function seedSession(
  page: Page,
  opts: {
    testRunId: string;
    gameNightId: string;
    isLive: boolean;
    scoreType?: ScoringType;
  }
): Promise<SeedSessionResponse> {
  const response = await page.request.post(`${SEED_BASE}/session`, {
    data: opts,
  });
  if (!response.ok()) {
    const body = await response.text();
    throw new Error(`seedSession failed (${response.status()}): ${body}`);
  }
  return (await response.json()) as SeedSessionResponse;
}

export async function seedPlayer(
  page: Page,
  opts: {
    testRunId: string;
    gameNightId: string;
    role: PlayerRole;
    userId?: string;
    displayName?: string;
  }
): Promise<SeedPlayerResponse> {
  const response = await page.request.post(`${SEED_BASE}/player`, {
    data: opts,
  });
  if (!response.ok()) {
    const body = await response.text();
    throw new Error(`seedPlayer failed (${response.status()}): ${body}`);
  }
  return (await response.json()) as SeedPlayerResponse;
}

/**
 * Issue #1929 Task C Macro 3a (DEC-C-8) — Seeds a SharedGame catalog entity +
 * a UserLibraryEntry owned by `ownerEmail`. Used by Journey #2/#3 to set up the
 * "user has a game in library" precondition. Cascade-cleaned by
 * `cleanupTestEntities` via DEC-B-8 TestRunId scope.
 */
export async function seedLibraryGame(
  page: Page,
  opts: {
    testRunId: string;
    ownerEmail: string;
    title?: string;
    publisher?: string;
    minPlayers?: number;
    maxPlayers?: number;
  }
): Promise<SeedLibraryGameResponse> {
  const response = await page.request.post(`${SEED_BASE}/library-game`, {
    data: opts,
  });
  if (!response.ok()) {
    const body = await response.text();
    throw new Error(`seedLibraryGame failed (${response.status()}): ${body}`);
  }
  return (await response.json()) as SeedLibraryGameResponse;
}

export async function cleanupTestEntities(
  page: Page,
  opts: { testRunId: string }
): Promise<CleanupResponse> {
  const response = await page.request.post(`${SEED_BASE}/cleanup`, {
    data: opts,
  });
  if (!response.ok()) {
    const body = await response.text();
    throw new Error(`cleanupTestEntities failed (${response.status()}): ${body}`);
  }
  return (await response.json()) as CleanupResponse;
}

/**
 * Visual-regression test fixture for `/sessions/[id]` summary (Wave D.3).
 *
 * Mirror Wave D.1 / Wave D.2 sentinel pattern: workflow
 * `visual-regression-migrated.yml` runs only Next.js prod (no backend API at
 * `:8080`). The summary page uses 4 hooks (`useSessionDetail`,
 * `useSessionDiaryQuery`, `useSessionVisionSnapshots`,
 * `useSessionAchievements`) that all need data to render the default cell.
 * In CI this fixture short-circuits the four hooks via the orchestrator.
 *
 * Production safety: production builds do NOT set
 * `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED`. The `STATE_OVERRIDE_ENABLED`
 * gate evaluates to its `process.env.NODE_ENV !== 'production'` branch only,
 * which is `false` in prod, allowing the bundler to dead-code-eliminate the
 * `?fixture=` URL hatch. The fixture data itself is also dead code in prod
 * because the orchestrator only reads it when the hatch is enabled.
 *
 * Schema reality v1 carryover (Gate B):
 *   - `useSessionDetail` returns `GameSessionDto` (from `games.schemas.ts`),
 *     NOT `SessionDetailsDto` (from `session-tracking.schemas.ts`). The
 *     contract uses `SessionDetailsDto` field names (`status`, `participants`,
 *     `finalizedAt`). The orchestrator (Task 3) is responsible for adapting
 *     `GameSessionDto` → `SessionDetailsDto`-shaped value for downstream
 *     consumers, OR composing both. This fixture exposes the contract shape
 *     (`SessionDetailsDto`) so the FSM and components consume the canonical
 *     summary domain types.
 *
 * State coverage (6 fixture kinds):
 *   - `default`             → 4 distinct scores, rich data
 *   - `tied`                → 2-way tie at 1st place
 *   - `abandoned`           → status='Abandoned', fewer events
 *   - `solo`                → 1 participant
 *   - `empty-achievements`  → full default but achievements=[]
 *   - `empty-photos`        → full default but snapshots=[]
 *
 * The `error` cell is NOT covered by fixtures — TanStack Query `isError`
 * is non-deterministic via URL override (covered by integration tests).
 *
 * Used by:
 *   - `apps/web/e2e/visual-migrated/sp4-session-summary.spec.ts` (Task 4)
 *   - `apps/web/e2e/v2-states/session-summary.spec.ts` (Task 4)
 */

import type { SessionSnapshotDto } from '@/lib/api/clients/sessionSnapshotsClient';
import type { ParticipantDto, SessionDetailsDto } from '@/lib/api/schemas/session-tracking.schemas';
import type { DiaryEntryDto } from '@/lib/api/session-flow/types';

import { sessionSummaryFixtureKindSchema } from './schemas';

import type { AchievementDto, SessionSummaryFixtureKind } from './schemas';

/**
 * Sentinel value for the visual-test fixture session id. UUIDv4-shaped so it
 * passes `z.string().uuid()` validation; encodes Wave D.3 issue #756 in the
 * trailing nibbles for human debuggability (mirrors Wave D.1 #735 sentinel).
 */
export const SESSION_SUMMARY_VISUAL_TEST_SENTINEL = '00000000-0000-4000-8000-000000000756' as const;

/**
 * True only when the build was produced by the visual-regression CI workflow
 * (which sets `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1` before `pnpm build`).
 */
export const IS_VISUAL_TEST_BUILD = process.env.NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED === '1';

/**
 * Build-time gating for the `?fixture=` URL override hatch.
 *
 * Allowing fixture overrides in production would expose a UI manipulation
 * surface. The hatch is enabled only when IS_VISUAL_TEST_BUILD=true OR
 * NODE_ENV !== 'production' (development/test environments).
 */
export const STATE_OVERRIDE_ENABLED = IS_VISUAL_TEST_BUILD || process.env.NODE_ENV !== 'production';

/**
 * Composed fixture record. Exposed shape mirrors what the 4 hooks would
 * collectively return on a happy-path render.
 */
export interface SessionSummaryFixture {
  readonly session: SessionDetailsDto;
  readonly diary: readonly DiaryEntryDto[];
  readonly snapshots: readonly SessionSnapshotDto[];
  readonly achievements: readonly AchievementDto[];
}

// ---------------------------------------------------------------------------
// Static building blocks (shared across fixtures)
// ---------------------------------------------------------------------------

const ISO_FINALIZED = '2026-05-04T20:30:00.000Z';
const ISO_SESSION_DATE = '2026-05-04T19:00:00.000Z';

const PARTICIPANTS_DEFAULT: readonly ParticipantDto[] = [
  {
    id: '00000000-0000-4000-8000-000000000a01',
    userId: '00000000-0000-4000-8000-000000000a01',
    displayName: 'Marco Rossi',
    isOwner: true,
    joinOrder: 1,
    finalRank: 1,
    totalScore: 42,
  },
  {
    id: '00000000-0000-4000-8000-000000000a02',
    userId: '00000000-0000-4000-8000-000000000a02',
    displayName: 'Anna Bianchi',
    isOwner: false,
    joinOrder: 2,
    finalRank: 2,
    totalScore: 36,
  },
  {
    id: '00000000-0000-4000-8000-000000000a03',
    userId: '00000000-0000-4000-8000-000000000a03',
    displayName: 'Luigi Verdi',
    isOwner: false,
    joinOrder: 3,
    finalRank: 3,
    totalScore: 28,
  },
  {
    id: '00000000-0000-4000-8000-000000000a04',
    userId: '00000000-0000-4000-8000-000000000a04',
    displayName: 'Sofia Russo',
    isOwner: false,
    joinOrder: 4,
    finalRank: 4,
    totalScore: 19,
  },
];

const PARTICIPANTS_TIED: readonly ParticipantDto[] = [
  {
    ...PARTICIPANTS_DEFAULT[0],
    totalScore: 42,
    finalRank: 1,
  },
  {
    ...PARTICIPANTS_DEFAULT[1],
    totalScore: 42,
    finalRank: 1,
  },
  {
    ...PARTICIPANTS_DEFAULT[2],
    totalScore: 28,
    finalRank: 3,
  },
  {
    ...PARTICIPANTS_DEFAULT[3],
    totalScore: 19,
    finalRank: 4,
  },
];

const PARTICIPANTS_SOLO: readonly ParticipantDto[] = [
  {
    ...PARTICIPANTS_DEFAULT[0],
    totalScore: 38,
    finalRank: 1,
  },
];

function buildScoresFor(participants: readonly ParticipantDto[]) {
  return participants.map((p, idx) => ({
    id: `00000000-0000-4000-8000-0000000${(0xb01 + idx).toString(16).padStart(5, '0')}`,
    participantId: p.id,
    roundNumber: null,
    category: null,
    scoreValue: p.totalScore,
    timestamp: ISO_FINALIZED,
  }));
}

function buildSession(
  participants: readonly ParticipantDto[],
  status: 'Completed' | 'Abandoned'
): SessionDetailsDto {
  return {
    id: SESSION_SUMMARY_VISUAL_TEST_SENTINEL,
    userId: PARTICIPANTS_DEFAULT[0].userId ?? PARTICIPANTS_DEFAULT[0].id,
    gameId: '00000000-0000-4000-8000-00000000c001',
    sessionCode: 'D3-VISUAL',
    sessionType: 'Standard',
    status,
    sessionDate: ISO_SESSION_DATE,
    location: 'Sala giochi',
    finalizedAt: ISO_FINALIZED,
    participants: [...participants],
    scores: buildScoresFor(participants),
    notes: [],
  };
}

function buildDiaryEntries(sessionId: string, count: number): readonly DiaryEntryDto[] {
  const types = ['score_updated', 'turn_advanced', 'chat_message', 'photo_added', 'tool_used'];
  const out: DiaryEntryDto[] = [];
  for (let i = 0; i < count; i += 1) {
    out.push({
      id: `00000000-0000-4000-8000-0000000d${(0x1000 + i).toString(16)}`,
      sessionId,
      gameNightId: null,
      eventType: types[i % types.length],
      timestamp: new Date(Date.parse(ISO_SESSION_DATE) + i * 60_000).toISOString(),
      payload: JSON.stringify({ index: i, kind: types[i % types.length] }),
      createdBy: PARTICIPANTS_DEFAULT[i % PARTICIPANTS_DEFAULT.length].userId,
      source: 'system',
    });
  }
  return out;
}

function buildSnapshots(sessionId: string, count: number): readonly SessionSnapshotDto[] {
  const out: SessionSnapshotDto[] = [];
  for (let i = 0; i < count; i += 1) {
    out.push({
      id: `00000000-0000-4000-8000-0000000e${(0x2000 + i).toString(16)}`,
      sessionId,
      turnNumber: i + 1,
      caption: i === 0 ? 'Setup iniziale' : `Snapshot turno ${i + 1}`,
      hasGameState: i === 0,
      createdAt: new Date(Date.parse(ISO_SESSION_DATE) + i * 120_000).toISOString(),
      images: [
        {
          id: `00000000-0000-4000-8000-0000000f${(0x3000 + i).toString(16)}`,
          downloadUrl: null,
          mediaType: 'image/jpeg',
          width: 800,
          height: 600,
          orderIndex: 0,
        },
      ],
    });
  }
  return out;
}

function buildAchievements(unlockedCount: number): readonly AchievementDto[] {
  const codes = ['firstWin', 'comeback', 'perfectScore', 'fastestTurn', 'mvp', 'streak'] as const;
  const emojis = ['🏆', '🔥', '⭐', '⚡', '👑', '🎯'];
  const out: AchievementDto[] = [];
  for (let i = 0; i < codes.length; i += 1) {
    const isUnlocked = i < unlockedCount;
    out.push({
      id: `00000000-0000-4000-8000-0000000ach${(0x100 + i).toString(16)}`,
      code: codes[i],
      titleKey: `pages.sessionSummary.achievements.${codes[i]}.title`,
      descriptionKey: `pages.sessionSummary.achievements.${codes[i]}.description`,
      iconEmoji: emojis[i],
      unlockedAt: isUnlocked
        ? new Date(Date.parse(ISO_FINALIZED) - i * 30_000).toISOString()
        : null,
    });
  }
  return out;
}

// ---------------------------------------------------------------------------
// 6 fixture variants
// ---------------------------------------------------------------------------

const FIXTURE_DEFAULT: SessionSummaryFixture = {
  session: buildSession(PARTICIPANTS_DEFAULT, 'Completed'),
  diary: buildDiaryEntries(SESSION_SUMMARY_VISUAL_TEST_SENTINEL, 8),
  snapshots: buildSnapshots(SESSION_SUMMARY_VISUAL_TEST_SENTINEL, 4),
  achievements: buildAchievements(4),
};

const FIXTURE_TIED: SessionSummaryFixture = {
  session: buildSession(PARTICIPANTS_TIED, 'Completed'),
  diary: buildDiaryEntries(SESSION_SUMMARY_VISUAL_TEST_SENTINEL, 7),
  snapshots: buildSnapshots(SESSION_SUMMARY_VISUAL_TEST_SENTINEL, 4),
  achievements: buildAchievements(3),
};

const FIXTURE_ABANDONED: SessionSummaryFixture = {
  session: buildSession(PARTICIPANTS_DEFAULT, 'Abandoned'),
  diary: buildDiaryEntries(SESSION_SUMMARY_VISUAL_TEST_SENTINEL, 5),
  snapshots: buildSnapshots(SESSION_SUMMARY_VISUAL_TEST_SENTINEL, 2),
  achievements: buildAchievements(2),
};

const FIXTURE_SOLO: SessionSummaryFixture = {
  session: buildSession(PARTICIPANTS_SOLO, 'Completed'),
  diary: buildDiaryEntries(SESSION_SUMMARY_VISUAL_TEST_SENTINEL, 6),
  snapshots: buildSnapshots(SESSION_SUMMARY_VISUAL_TEST_SENTINEL, 4),
  achievements: buildAchievements(3),
};

const FIXTURE_EMPTY_ACHIEVEMENTS: SessionSummaryFixture = {
  session: FIXTURE_DEFAULT.session,
  diary: FIXTURE_DEFAULT.diary,
  snapshots: FIXTURE_DEFAULT.snapshots,
  achievements: [],
};

const FIXTURE_EMPTY_PHOTOS: SessionSummaryFixture = {
  session: FIXTURE_DEFAULT.session,
  diary: FIXTURE_DEFAULT.diary,
  snapshots: [],
  achievements: FIXTURE_DEFAULT.achievements,
};

/**
 * Single source of truth for the 6 visual fixture variants. Keyed by
 * `SessionSummaryFixtureKind` — the discriminant is also used by the
 * `?fixture=` URL override.
 */
export const sessionSummaryFixtures: Readonly<
  Record<SessionSummaryFixtureKind, SessionSummaryFixture>
> = {
  default: FIXTURE_DEFAULT,
  tied: FIXTURE_TIED,
  abandoned: FIXTURE_ABANDONED,
  solo: FIXTURE_SOLO,
  'empty-achievements': FIXTURE_EMPTY_ACHIEVEMENTS,
  'empty-photos': FIXTURE_EMPTY_PHOTOS,
};

// ---------------------------------------------------------------------------
// URL override parsing
// ---------------------------------------------------------------------------

/**
 * Parses the `?fixture=` URL search param into a `SessionSummaryFixtureKind`.
 *
 * Returns `null` when:
 *   - `STATE_OVERRIDE_ENABLED` is false (production builds without the env var)
 *   - The param is missing, empty, or not a recognized fixture kind
 *
 * Accepts either a `URLSearchParams`, a plain string (the param value), or
 * `null`/`undefined` for ergonomic call sites.
 */
export function parseStateOverride(
  searchParam: string | URLSearchParams | null | undefined
): SessionSummaryFixtureKind | null {
  if (!STATE_OVERRIDE_ENABLED) return null;
  let raw: string | null;
  if (searchParam == null) {
    raw = null;
  } else if (typeof searchParam === 'string') {
    raw = searchParam;
  } else {
    raw = searchParam.get('fixture');
  }
  if (!raw) return null;
  const parsed = sessionSummaryFixtureKindSchema.safeParse(raw);
  return parsed.success ? parsed.data : null;
}

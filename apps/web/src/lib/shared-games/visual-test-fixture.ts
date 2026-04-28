/**
 * Visual-regression test fixture for `/shared-games/[id]` (Wave A.4, Issue #603).
 *
 * **Purpose**: bootstrap workflow `visual-regression-migrated.yml` runs only
 * Next.js prod (no backend API at `:8080`). The detail route's SSR fetcher
 * (`getSharedGameDetail`) cannot reach the backend and returns null →
 * `page.tsx` calls `notFound()` → no screenshot is rendered.
 *
 * **Contract**: when env var `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED === '1'`
 * is baked into the build, navigating to `/shared-games/<VISUAL_TEST_FIXTURE_ID>`
 * short-circuits the SSR fetch and returns a deterministic, statically-defined
 * `SharedGameDetailV2` shape that exercises the v2 surface (hero + 5 tabs +
 * non-empty toolkit/agent/kb lists + community strip).
 *
 * **Production safety**: Production builds do NOT set the env var. The constant
 * `IS_VISUAL_TEST_BUILD` evaluates to `false` and the fixture short-circuit
 * is dead code, eliminated by the bundler. The fixture UUID is meaningless
 * to a production deployment — it returns 404 like any unknown id.
 *
 * Used by:
 *   - `apps/web/e2e/visual-migrated/sp3-shared-game-detail.spec.ts`
 *   - `apps/web/e2e/v2-states/shared-game-detail.spec.ts`
 */

import type { SharedGameDetailV2, TopContributor } from '@/lib/api/shared-games';

/**
 * Deterministic UUIDv4-shaped sentinel encoding issue #603 in the last group
 * for human-debuggability. Recognized by `loadInitialData` only when the
 * fixture flag is enabled at build time.
 */
export const VISUAL_TEST_FIXTURE_ID = '00000000-0000-4000-8000-000000000603' as const;

/**
 * True only when the build was produced by the visual-regression CI workflow
 * (sets `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1` before `pnpm build`).
 *
 * `NEXT_PUBLIC_*` env vars are inlined at build time → in production deploys
 * this is the literal `false`, allowing the bundler to dead-code-eliminate
 * the fixture and its short-circuit branches.
 */
export const IS_VISUAL_TEST_BUILD = process.env.NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED === '1';

const FIXTURE_DETAIL: SharedGameDetailV2 = {
  id: VISUAL_TEST_FIXTURE_ID,
  bggId: 174430,
  title: 'Gloomhaven',
  yearPublished: 2017,
  description:
    'Gloomhaven is a game of Euro-inspired tactical combat in a persistent world of shifting motives. Players take on the role of wandering adventurers with their own special set of skills and their own reasons for traveling to this dark corner of the world.',
  minPlayers: 1,
  maxPlayers: 4,
  playingTimeMinutes: 120,
  minAge: 14,
  complexityRating: 3.85,
  averageRating: 8.7,
  imageUrl: '',
  thumbnailUrl: '',
  status: 'Approved',
  createdAt: '2026-01-15T10:00:00.000Z',
  modifiedAt: '2026-04-20T10:00:00.000Z',
  toolkits: [
    {
      id: '11111111-1111-4000-8000-000000000001',
      name: 'Combat tracker companion',
      ownerId: '22222222-2222-4000-8000-000000000001',
      ownerName: 'Marco Rossi',
      lastUpdatedAt: '2026-04-22T14:30:00.000Z',
    },
    {
      id: '11111111-1111-4000-8000-000000000002',
      name: 'Scenario unlock helper',
      ownerId: '22222222-2222-4000-8000-000000000002',
      ownerName: 'Sofia Bianchi',
      lastUpdatedAt: '2026-04-18T09:15:00.000Z',
    },
    {
      id: '11111111-1111-4000-8000-000000000003',
      name: 'Character sheet builder',
      ownerId: '22222222-2222-4000-8000-000000000003',
      ownerName: 'Luca Verdi',
      lastUpdatedAt: '2026-04-10T18:45:00.000Z',
    },
  ],
  agents: [
    {
      id: '33333333-3333-4000-8000-000000000001',
      name: 'Rules clarifier',
      invocationCount: 1284,
      lastUpdatedAt: '2026-04-25T11:00:00.000Z',
    },
    {
      id: '33333333-3333-4000-8000-000000000002',
      name: 'Strategy advisor',
      invocationCount: 547,
      lastUpdatedAt: '2026-04-12T16:20:00.000Z',
    },
  ],
  kbs: [
    {
      id: '44444444-4444-4000-8000-000000000001',
      language: 'en',
      totalChunks: 412,
      indexedAt: '2026-03-10T08:00:00.000Z',
    },
    {
      id: '44444444-4444-4000-8000-000000000002',
      language: 'it',
      totalChunks: 398,
      indexedAt: '2026-03-15T08:00:00.000Z',
    },
  ],
  toolkitsCount: 3,
  agentsCount: 2,
  kbsCount: 2,
  contributorsCount: 12,
  hasKnowledgeBase: true,
  isTopRated: true,
  isNew: false,
};

const FIXTURE_CONTRIBUTORS: readonly TopContributor[] = [
  {
    userId: '55555555-5555-4000-8000-000000000001',
    displayName: 'Marco Rossi',
    avatarUrl: null,
    totalSessions: 42,
    totalWins: 18,
    score: 78,
  },
  {
    userId: '55555555-5555-4000-8000-000000000002',
    displayName: 'Sofia Bianchi',
    avatarUrl: null,
    totalSessions: 35,
    totalWins: 14,
    score: 63,
  },
  {
    userId: '55555555-5555-4000-8000-000000000003',
    displayName: 'Luca Verdi',
    avatarUrl: null,
    totalSessions: 28,
    totalWins: 11,
    score: 50,
  },
  {
    userId: '55555555-5555-4000-8000-000000000004',
    displayName: 'Giulia Neri',
    avatarUrl: null,
    totalSessions: 21,
    totalWins: 8,
    score: 37,
  },
  {
    userId: '55555555-5555-4000-8000-000000000005',
    displayName: 'Andrea Costa',
    avatarUrl: null,
    totalSessions: 18,
    totalWins: 6,
    score: 30,
  },
];

/**
 * Returns the static fixture iff the build is a visual-test build AND the id
 * matches the sentinel. Returns `null` otherwise — caller must fall through
 * to the real backend fetch.
 */
export function tryLoadVisualTestFixture(id: string): {
  readonly detail: SharedGameDetailV2;
  readonly contributors: readonly TopContributor[];
} | null {
  if (!IS_VISUAL_TEST_BUILD) return null;
  if (id !== VISUAL_TEST_FIXTURE_ID) return null;
  return { detail: FIXTURE_DETAIL, contributors: FIXTURE_CONTRIBUTORS };
}

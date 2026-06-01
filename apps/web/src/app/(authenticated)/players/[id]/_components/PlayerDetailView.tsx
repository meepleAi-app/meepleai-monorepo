/**
 * PlayerDetailView — Wave 3 (Issue #683) orchestrator for `/players/[id]`.
 *
 * Tier M route — single hook + linear 4-state FSM.
 * Mirrors Wave C.1 GameDetailView pattern adapted for the player detail surface.
 *
 *   - `usePlayerStatistics()` TanStack hook (single call — current user only;
 *     v1 schema carryover: playerId from URL slug is decorative, decoded as
 *     displayName. True other-player API is a followup issue post-merge.)
 *   - `derivePlayerDetailUiState` pure helper for FSM derivation
 *   - `mapStatsToProfile` pure helper: derives displayName from URL slug,
 *     winRate from totals, favoriteGameName from max-count gamePlayCounts entry;
 *     achievementCount/leaderboardRank/favoriteAgentName defaulted to 0/null/null
 *     (TBD backend schema extensions)
 *   - `?state=...` URL override gated by
 *     `NODE_ENV !== 'production' || IS_VISUAL_TEST_BUILD`
 *   - Visual-test fixture short-circuit (`IS_VISUAL_TEST_BUILD`) for CI
 *     Next.js prod-only builds without a backend API at `:8080`
 *   - View-all CTAs link to existing subroutes (preserved per Wave C.1 lesson):
 *       /players/{id}/achievements, /players/{id}/games, /players/{id}/sessions
 *
 * Shells:
 *   - loading  — inline skeleton
 *   - error    — error message + retry CTA
 *   - not-found — descriptive message + CTA back to /players
 *   - default   — PlayerHero + PlayerStatsGrid + PlayerLeaderboardCard +
 *                 FavoriteAgentCard + AchievementBadgeGrid
 *
 * Subroutes /players/[id]/{achievements,games,sessions,stats} UNCHANGED.
 *
 * Refs Wave 3 /players/[id] (Issue #683).
 */

'use client';

import { useMemo, type ReactElement } from 'react';

import Link from 'next/link';
import { useRouter, useSearchParams } from 'next/navigation';

import {
  AchievementBadgeGrid,
  PlayerHero,
  type AchievementBadgeGridLabels,
  type FavoriteAgentCardLabels,
  type PlayerHeroLabels,
  type PlayerLeaderboardCardLabels,
  type PlayerStatsGridLabels,
  type PlayerTopGamesCardLabels,
} from '@/components/features/player-detail';
import { DetailPageLayout } from '@/components/ui/detail-layout';
import { useAchievements } from '@/hooks/queries/useAchievements';
import { usePlayerStatistics } from '@/hooks/queries/usePlayersFromRecords';
import { useTranslation } from '@/hooks/useTranslation';
import type { PlayerStatistics } from '@/lib/api/schemas/play-records.schemas';
import { derivePlayerDetailUiState } from '@/lib/player-detail/player-detail-state';
import {
  IS_VISUAL_TEST_BUILD,
  tryLoadVisualTestFixture,
  type PlayerProfileFixture,
  type TopGameItem,
} from '@/lib/player-detail/player-detail-visual-test-fixture';

import { GamesTabPanel, type GamesTabPanelLabels } from './GamesTabPanel';
import { PlayerConnectionBar, type PlayerConnectionBarLabels } from './PlayerConnectionBar';
import { PlayerOverviewRegion, type PlayerOverviewRegionLabels } from './PlayerOverviewRegion';
import {
  PLAYER_TAB_KEYS,
  PlayerTabs,
  type PlayerTabKey,
  type PlayerTabsLabels,
} from './PlayerTabs';
import { SessionsTabPanel, type SessionsTabPanelLabels } from './SessionsTabPanel';
import { ToolkitsTabPanel, type ToolkitsTabPanelLabels } from './ToolkitsTabPanel';

// ─── State override hatch (dev / visual-test only) ─────────────────────────

const VALID_OVERRIDES = ['loading', 'error', 'not-found'] as const;
type StateOverride = (typeof VALID_OVERRIDES)[number];

const STATE_OVERRIDE_ENABLED = process.env.NODE_ENV !== 'production' || IS_VISUAL_TEST_BUILD;

function parseStateOverride(raw: string | null): StateOverride | null {
  if (!STATE_OVERRIDE_ENABLED) return null;
  if (raw == null) return null;
  return (VALID_OVERRIDES as readonly string[]).includes(raw) ? (raw as StateOverride) : null;
}

// ─── Tab URL state ────────────────────────────────────────────────────────────

const DEFAULT_TAB: PlayerTabKey = 'sessions';

function parseTabFromUrl(raw: string | null): PlayerTabKey {
  if (raw == null) return DEFAULT_TAB;
  return (PLAYER_TAB_KEYS as readonly string[]).includes(raw) ? (raw as PlayerTabKey) : DEFAULT_TAB;
}

// ─── Props ─────────────────────────────────────────────────────────────────

export interface PlayerDetailViewProps {
  /**
   * Normalized playerId from URL params.
   * MUST be string | null — NEVER undefined or 'undefined'.
   * null → FSM immediately maps to 'not-found'.
   */
  readonly playerId: string | null;
}

// ─── Schema mapping ─────────────────────────────────────────────────────────

/**
 * Derives the ranked top-N games list for `PlayerTopGamesCard`.
 *
 * Source priority (#1663 Phase 2 evolution — optional during BE rollout):
 *   1. `stats.mostPlayedGames` if present (BE pre-sorted, has gameId).
 *   2. Fallback: derive from `stats.gamePlayCounts` (always present).
 *
 * `winCount` is joined from `stats.winByGame` (optional) by gameId when both
 * sides have one, otherwise by gameName. Returns `null` when no join hits —
 * the card then gracefully hides the win-rate badge (never shows "0%").
 */
function deriveTopGames(stats: PlayerStatistics, maxItems = 5): ReadonlyArray<TopGameItem> {
  // Build a winByGame lookup: prefer gameId, fall back to gameName.
  const winsByGameId = new Map<string, number>();
  const winsByGameName = new Map<string, number>();
  for (const entry of stats.winByGame ?? []) {
    if (entry.gameId != null) winsByGameId.set(entry.gameId, entry.won);
    winsByGameName.set(entry.gameName, entry.won);
  }

  const resolveWins = (gameId: string | null, gameName: string): number | null => {
    if (gameId != null && winsByGameId.has(gameId)) return winsByGameId.get(gameId) ?? null;
    if (winsByGameName.has(gameName)) return winsByGameName.get(gameName) ?? null;
    return null;
  };

  // Priority 1: mostPlayedGames (BE-sorted, structured).
  if (stats.mostPlayedGames && stats.mostPlayedGames.length > 0) {
    return stats.mostPlayedGames.slice(0, maxItems).map(g => ({
      gameId: g.gameId,
      gameName: g.gameName,
      playCount: g.plays,
      winCount: resolveWins(g.gameId, g.gameName),
    }));
  }

  // Fallback: derive from gamePlayCounts (Record<gameName, count>).
  return Object.entries(stats.gamePlayCounts)
    .map<TopGameItem>(([gameName, playCount]) => ({
      gameId: null,
      gameName,
      playCount,
      winCount: resolveWins(null, gameName),
    }))
    .sort((a, b) => b.playCount - a.playCount)
    .slice(0, maxItems);
}

/**
 * Maps usePlayerStatistics data + URL slug → PlayerProfileFixture-like shape.
 *
 * Schema reality (v1 carryover):
 *   - displayName derived from URL slug (decodeURIComponent + replace dashes)
 *   - winRate = totalWins / totalSessions (0 when totalSessions === 0)
 *   - favoriteGameName = max-count entry from gamePlayCounts (null when empty)
 *   - topGames = top-N derived from mostPlayedGames (or gamePlayCounts fallback)
 *     joined with winByGame for per-game wins
 *   - achievementCount / leaderboardRank / favoriteAgentName defaulted to
 *     0 / null / null (TBD backend schema extensions)
 */
function mapStatsToProfile(playerId: string, stats: PlayerStatistics): PlayerProfileFixture {
  const { totalSessions, totalWins, gamePlayCounts } = stats;
  const winRate = totalSessions > 0 ? totalWins / totalSessions : 0;

  const gamePlayCountsEntries = Object.entries(gamePlayCounts);
  const favoriteGameName =
    gamePlayCountsEntries.length > 0
      ? gamePlayCountsEntries.reduce<[string, number]>(
          (max, [name, count]) => (count > max[1] ? [name, count] : max),
          ['', 0]
        )[0]
      : null;

  const displayName = decodeURIComponent(playerId).replace(/-/g, ' ');

  return {
    playerId,
    displayName,
    totalSessions,
    totalWins,
    winRate,
    favoriteGameName: favoriteGameName && favoriteGameName.length > 0 ? favoriteGameName : null,
    favoriteAgentName: null, // TBD: no favorite agent data in current schema
    achievementCount: 0, // TBD: no achievement data in current schema
    leaderboardRank: null, // TBD: no leaderboard data in current schema
    topGames: deriveTopGames(stats),
  };
}

// ─── Loading skeleton ────────────────────────────────────────────────────────

function PlayerDetailLoadingSkeleton(): ReactElement {
  return (
    <div
      data-slot="player-detail-loading"
      aria-busy="true"
      aria-live="polite"
      className="flex flex-col gap-4 p-4 sm:p-8"
    >
      {/* Hero skeleton */}
      <div className="h-[200px] w-full animate-pulse rounded-2xl bg-muted" />
      {/* Stats grid skeleton */}
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        {Array.from({ length: 4 }).map((_, i) => (
          <div key={i} className="h-28 animate-pulse rounded-2xl bg-muted" />
        ))}
      </div>
      {/* Cards row skeleton */}
      <div className="grid gap-3 sm:grid-cols-2">
        <div className="h-24 animate-pulse rounded-2xl bg-muted" />
        <div className="h-24 animate-pulse rounded-2xl bg-muted" />
      </div>
      {/* Achievement grid skeleton */}
      <div className="h-32 animate-pulse rounded-2xl bg-muted" />
    </div>
  );
}

// ─── Error shell ────────────────────────────────────────────────────────────

function ErrorShell({
  title,
  subtitle,
  cta,
  onRetry,
}: {
  title: string;
  subtitle: string;
  cta: string;
  onRetry: () => void;
}): ReactElement {
  return (
    <div
      data-slot="player-detail-error"
      role="status"
      aria-live="polite"
      className="flex min-h-[60vh] flex-col items-center justify-center gap-4 p-4 text-center sm:p-8"
    >
      <span aria-hidden="true" className="text-4xl">
        ⚠️
      </span>
      <h2 className="font-display text-[20px] font-extrabold text-foreground">{title}</h2>
      <p className="max-w-sm text-[14px] text-muted-foreground">{subtitle}</p>
      <button
        type="button"
        onClick={onRetry}
        data-slot="player-detail-error-retry"
        className="rounded-md border border-border bg-transparent px-4 py-2 font-display text-[13px] font-bold text-foreground transition-colors hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
      >
        {cta}
      </button>
    </div>
  );
}

// ─── Not-found shell ─────────────────────────────────────────────────────────

function NotFoundShell({
  title,
  subtitle,
  cta,
}: {
  title: string;
  subtitle: string;
  cta: string;
}): ReactElement {
  return (
    <div
      data-slot="player-detail-not-found"
      role="status"
      aria-live="polite"
      className="flex min-h-[60vh] flex-col items-center justify-center gap-4 p-4 text-center sm:p-8"
    >
      <span aria-hidden="true" className="text-5xl">
        👤
      </span>
      <h2 className="font-display text-[20px] font-extrabold text-foreground">{title}</h2>
      <p className="max-w-sm text-[14px] text-muted-foreground">{subtitle}</p>
      <Link
        href="/players"
        data-slot="player-detail-not-found-cta"
        className="inline-flex items-center gap-1.5 rounded-md border-none bg-violet-700 px-4 py-2.5 font-display text-[13px] font-extrabold text-white shadow-md transition-shadow hover:shadow-lg focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
      >
        {cta}
      </Link>
    </div>
  );
}

// ─── Main orchestrator ──────────────────────────────────────────────────────

/**
 * PlayerDetailView — orchestrator for /players/[id] v2 surface.
 *
 * Accepts `playerId: string | null` (normalized at page boundary by page.tsx).
 */
export function PlayerDetailView({ playerId }: PlayerDetailViewProps): ReactElement {
  const { t } = useTranslation();
  const router = useRouter();
  const searchParams = useSearchParams();

  // ── URL override hatch ───────────────────────────────────────────────────
  const stateOverride = parseStateOverride(searchParams.get('state'));

  // ── Visual fixture short-circuit (CI prod build) ─────────────────────────
  // Fixture loads when IS_VISUAL_TEST_BUILD=true and override is not 'not-found'.
  // Dead-code-eliminated in production builds.
  const fixture = useMemo<PlayerProfileFixture | null>(() => {
    if (!IS_VISUAL_TEST_BUILD) return null;
    if (stateOverride === 'not-found') return null;
    return tryLoadVisualTestFixture('default');
  }, [stateOverride]);

  // ── Data hooks (current user only — schema reality v1 carryover) ─────────
  const statsQuery = usePlayerStatistics();
  // #1542: pull achievements list to derive `achievementCount` from the real
  // unlock count instead of the legacy hardcoded `0`. Graceful degrade: if the
  // achievements query is still loading or errored, achievementCount stays 0.
  const achievementsQuery = useAchievements();

  // ── Derived profile (fixture takes priority over real data) ─────────────
  const profile = useMemo<PlayerProfileFixture | null>(() => {
    if (fixture != null) return fixture;
    if (playerId == null) return null;
    if (statsQuery.data == null) return null;
    const base = mapStatsToProfile(playerId, statsQuery.data);
    const unlockedCount = achievementsQuery.data?.filter(a => a.isUnlocked).length ?? 0;
    return { ...base, achievementCount: unlockedCount };
  }, [fixture, playerId, statsQuery.data, achievementsQuery.data]);

  // ── FSM state derivation ─────────────────────────────────────────────────
  const realKind = useMemo(() => {
    if (fixture != null) return 'default' as const;
    return derivePlayerDetailUiState({
      playerId,
      isLoading: statsQuery.isLoading,
      isError: statsQuery.isError,
      hasData: statsQuery.data != null,
    });
  }, [fixture, playerId, statsQuery.isLoading, statsQuery.isError, statsQuery.data]);

  const effectiveKind = stateOverride ?? realKind;

  // ── i18n labels ───────────────────────────────────────────────────────────

  const heroLabels = useMemo<PlayerHeroLabels>(
    () => ({
      back: t('pages.playerDetail.hero.back'),
      backAriaLabel: t('pages.playerDetail.hero.backAriaLabel'),
      totalSessions: t('pages.playerDetail.hero.totalSessions'),
      totalWins: t('pages.playerDetail.hero.totalWins'),
      winRate: t('pages.playerDetail.hero.winRate'),
    }),
    [t]
  );

  const statsLabels = useMemo<PlayerStatsGridLabels>(
    () => ({
      sessions: t('pages.playerDetail.sections.stats.sessions'),
      wins: t('pages.playerDetail.sections.stats.wins'),
      winRate: t('pages.playerDetail.sections.stats.winRate'),
      achievements: t('pages.playerDetail.sections.stats.achievements'),
    }),
    [t]
  );

  const leaderboardLabels = useMemo<PlayerLeaderboardCardLabels>(
    () => ({
      title: t('pages.playerDetail.sections.leaderboard.title'),
      rank: t('pages.playerDetail.sections.leaderboard.rank'),
      rankAriaLabel: t('pages.playerDetail.sections.leaderboard.ariaLabel'),
      noRank: t('pages.playerDetail.sections.leaderboard.noRank'),
    }),
    [t]
  );

  const favoriteAgentLabels = useMemo<FavoriteAgentCardLabels>(
    () => ({
      title: t('pages.playerDetail.sections.favoriteAgent.title'),
      none: t('pages.playerDetail.sections.favoriteAgent.none'),
      ariaLabel: t('pages.playerDetail.sections.favoriteAgent.ariaLabel'),
    }),
    [t]
  );

  const topGamesLabels = useMemo<PlayerTopGamesCardLabels>(
    () => ({
      title: t('pages.playerDetail.sections.topGames.title'),
      playsLabel: t('pages.playerDetail.sections.topGames.playsLabel'),
      playsLabelWithWins: t('pages.playerDetail.sections.topGames.playsLabelWithWins'),
      winRateLabel: t('pages.playerDetail.sections.topGames.winRateLabel'),
      rankAriaLabel: t('pages.playerDetail.sections.topGames.rankAriaLabel'),
      empty: t('pages.playerDetail.sections.topGames.empty'),
    }),
    [t]
  );

  const achievementLabels = useMemo<AchievementBadgeGridLabels>(
    () => ({
      title: t('pages.playerDetail.sections.achievements.title'),
      viewAll: t('pages.playerDetail.sections.achievements.viewAll'),
      viewAllAriaLabel: t('pages.playerDetail.sections.achievements.viewAllAriaLabel'),
      empty: t('pages.playerDetail.sections.achievements.empty'),
    }),
    [t]
  );

  // ── Shell renders ────────────────────────────────────────────────────────

  if (effectiveKind === 'loading') {
    return <PlayerDetailLoadingSkeleton />;
  }

  if (effectiveKind === 'error') {
    return (
      <ErrorShell
        title={t('pages.playerDetail.states.error.title')}
        subtitle={t('pages.playerDetail.states.error.subtitle')}
        cta={t('pages.playerDetail.states.error.cta')}
        onRetry={() => statsQuery.refetch?.()}
      />
    );
  }

  if (effectiveKind === 'not-found') {
    return (
      <NotFoundShell
        title={t('pages.playerDetail.states.notFound.title')}
        subtitle={t('pages.playerDetail.states.notFound.subtitle')}
        cta={t('pages.playerDetail.states.notFound.cta')}
      />
    );
  }

  // ── Default render — profile guaranteed non-null (FSM=default) ───────────
  // TypeScript: effectiveKind === 'default' → real FSM ensures profile != null.
  // The fixture branch also guarantees profile != null. The non-null assertion
  // is intentional and safe: derivePlayerDetailUiState has validated profile.
  // eslint-disable-next-line @typescript-eslint/no-non-null-assertion -- FSM invariant
  const safeProfile = profile!;
  const safePlayerId = playerId ?? safeProfile.playerId;

  const tab = parseTabFromUrl(searchParams.get('tab'));

  const onTabChange = (next: PlayerTabKey): void => {
    const params = new URLSearchParams(searchParams.toString());
    if (next === DEFAULT_TAB) {
      params.delete('tab');
    } else {
      params.set('tab', next);
    }
    const qs = params.toString();
    router.replace(`/players/${safePlayerId}${qs ? `?${qs}` : ''}`, { scroll: false });
  };

  const gamePlayCounts = statsQuery.data?.gamePlayCounts ?? {};
  const gameCount = Object.keys(gamePlayCounts).length;

  const tabCounts: Record<PlayerTabKey, number> = {
    sessions: safeProfile.totalSessions,
    games: gameCount,
    toolkits: 0,
    achievements: safeProfile.achievementCount,
  };

  const tabLabels: PlayerTabsLabels = {
    tablistAriaLabel: t('pages.playerDetail.tabs.ariaLabel'),
    sessions: t('pages.playerDetail.tabs.sessions.label'),
    games: t('pages.playerDetail.tabs.games.label'),
    toolkits: t('pages.playerDetail.tabs.toolkits.label'),
    achievements: t('pages.playerDetail.tabs.achievements.label'),
  };

  const connectionLabels: PlayerConnectionBarLabels = {
    topGames: t('pages.playerDetail.connections.topGames'),
    sessions: t('pages.playerDetail.connections.sessions'),
    gameNights: t('pages.playerDetail.connections.gameNights'),
    agents: t('pages.playerDetail.connections.agents'),
    toolkits: t('pages.playerDetail.connections.toolkits'),
    chats: t('pages.playerDetail.connections.chats'),
  };

  const overviewLabels: PlayerOverviewRegionLabels = {
    stats: statsLabels,
    leaderboard: leaderboardLabels,
    favoriteAgent: favoriteAgentLabels,
    topGames: topGamesLabels,
  };

  const sessionsLabels: SessionsTabPanelLabels = {
    title: t('pages.playerDetail.tabs.sessions.title'),
    viewAll: t('pages.playerDetail.tabs.sessions.viewAll'),
    empty: t('pages.playerDetail.tabs.sessions.empty'),
    totalLabel: t('pages.playerDetail.tabs.sessions.totalLabel'),
  };

  const gamesLabels: GamesTabPanelLabels = {
    title: t('pages.playerDetail.tabs.games.title'),
    viewAll: t('pages.playerDetail.tabs.games.viewAll'),
    empty: t('pages.playerDetail.tabs.games.empty'),
    playsSuffix: t('pages.playerDetail.tabs.games.playsSuffix'),
  };

  const toolkitsLabels: ToolkitsTabPanelLabels = {
    title: t('pages.playerDetail.tabs.toolkits.title'),
    comingSoon: t('pages.playerDetail.tabs.toolkits.comingSoon'),
  };

  // #1094 ARIA fix: wrap tabPanel in role="tabpanel" + matching id so that
  // PlayerTabs' aria-controls="tabpanel-player-detail-{key}" resolves to a
  // real element (was firing axe aria-valid-attr-value otherwise).
  let tabPanelInner: ReactElement;
  switch (tab) {
    case 'games':
      tabPanelInner = (
        <GamesTabPanel
          playerId={safePlayerId}
          gamePlayCounts={gamePlayCounts}
          labels={gamesLabels}
        />
      );
      break;
    case 'toolkits':
      tabPanelInner = <ToolkitsTabPanel labels={toolkitsLabels} />;
      break;
    case 'achievements':
      tabPanelInner = (
        <AchievementBadgeGrid
          count={safeProfile.achievementCount}
          viewAllHref={`/players/${safePlayerId}/achievements`}
          labels={achievementLabels}
        />
      );
      break;
    case 'sessions':
    default:
      tabPanelInner = <SessionsTabPanel stats={safeProfile} labels={sessionsLabels} />;
      break;
  }
  const tabPanel: ReactElement = (
    <div
      role="tabpanel"
      id={`tabpanel-player-detail-${tab}`}
      aria-labelledby={`tab-player-detail-${tab}`}
      tabIndex={0}
    >
      {tabPanelInner}
    </div>
  );

  return (
    <div data-slot="player-detail-view">
      <DetailPageLayout
        hero={
          <>
            <PlayerHero
              displayName={safeProfile.displayName}
              totalSessions={safeProfile.totalSessions}
              totalWins={safeProfile.totalWins}
              winRate={safeProfile.winRate}
              onBack={() => router.push('/players')}
              labels={heroLabels}
            />
            <PlayerOverviewRegion
              stats={safeProfile}
              labels={overviewLabels}
              onFavoriteAgentClick={
                safeProfile.favoriteAgentName != null ? () => router.push('/agents') : undefined
              }
            />
          </>
        }
        connections={
          <PlayerConnectionBar
            stats={safeProfile}
            gameCount={gameCount}
            labels={connectionLabels}
          />
        }
        tabs={
          <PlayerTabs
            activeTab={tab}
            onChange={onTabChange}
            counts={tabCounts}
            labels={tabLabels}
          />
        }
      >
        {tabPanel}
      </DetailPageLayout>
    </div>
  );
}

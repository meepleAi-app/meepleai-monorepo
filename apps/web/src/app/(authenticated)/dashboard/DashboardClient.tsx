/**
 * DashboardClient — Asse C priority-driven orchestrator for `/dashboard` (Issue #1898).
 *
 * REFACTOR replacement of Stage 3 cluster (5 entity sections Games/Players/Agents/
 * Sessions/Events) with 4 priority slots in fixed order:
 *
 *   1. ProssimiSection      → upcoming GameNights (Published + InProgress, ASC by date)
 *   2. RecentiSection       → completed GameNights (DESC by date)
 *   3. SuggestedSection     → "Potresti giocare" game suggestions (MVP fixture)
 *   4. FriendsActivitySection → recent friend activities (verbs: completed/created/joined)
 *
 * DEC-1 (locked plan v2): in-place refactor of /dashboard, no alternative route.
 * Hero block (DashboardHero) is preserved as the entry surface; KPI grid still
 * exposes games / sessions / hoursPlayed / winRate (the latter two still
 * unexposed by the backend, displayed as "—").
 *
 * State derivation per section:
 *   loading → query.isLoading
 *   error   → query.isError
 *   empty   → derived (length === 0)
 *   default → otherwise
 *
 * Pixel-faithful to admin-mockups/design_files/sp4-dashboard.jsx.
 */

'use client';

import { useMemo, type ReactElement } from 'react';

import { useAuth } from '@/components/auth/AuthProvider';
import { CascadeDrawerHost } from '@/components/dashboard/CascadeDrawerHost';
import { useActiveSessions } from '@/hooks/queries/useActiveSessions';
import { useUpcomingGameNights } from '@/hooks/queries/useGameNights';
import { useGames } from '@/hooks/queries/useGames';
import { useLibraryStats } from '@/hooks/queries/useLibrary';
import { useFriendsActivity } from '@/hooks/use-friends-activity';
import { useTranslation } from '@/hooks/useTranslation';

import { DashboardHero, type DashboardHeroKpi } from './_components/DashboardHero';
import {
  FriendsActivitySection,
  type FriendsActivitySectionState,
  ProssimiSection,
  type ProssimiGameNightCard,
  type ProssimiSectionState,
  type ProssimiStatus,
  RecentiSection,
  type RecentiGameNightCard,
  type RecentiSectionState,
  SuggestedSection,
  type SuggestedGameCard,
  type SuggestedSectionState,
} from './_components/sections';

/**
 * Derive a 4-state lifecycle (loading | error | empty | default) from
 * a TanStack Query result + a derived item count.
 */
function deriveSectionState(
  isLoading: boolean,
  isError: boolean,
  itemCount: number
): 'loading' | 'error' | 'empty' | 'default' {
  if (isLoading) return 'loading';
  if (isError) return 'error';
  if (itemCount === 0) return 'empty';
  return 'default';
}

export function DashboardClient(): ReactElement {
  const { t } = useTranslation();
  const { user } = useAuth();

  // ── Data hooks (re-used from Stage 3 + asse-C additions) ─────────────────
  const upcomingGNQuery = useUpcomingGameNights();
  const sessionsQuery = useActiveSessions(10);
  const gamesQuery = useGames(undefined, undefined, 1, 20);
  const statsQuery = useLibraryStats();
  const friendsActivityQuery = useFriendsActivity();

  // ── Slot #1: Prossimi (upcoming GameNights, Published + InProgress) ──────
  const prossimiCards = useMemo<ReadonlyArray<ProssimiGameNightCard>>(() => {
    const data = upcomingGNQuery.data ?? [];
    return (
      data
        .filter(gn => gn.status === 'Published' || gn.status === 'Draft')
        // Treat "Draft" as future "Published" for the upcoming surface? No — only
        // Published / InProgress should appear. The backend `getUpcoming()` already
        // filters server-side, but we re-assert here so a relaxed BE doesn't leak
        // stale Cancelled/Completed rows.
        // (InProgress isn't a status emitted by the upcoming endpoint today, but
        // when asse-A WP1 + #15 lands and the BE starts emitting it on a started
        // session, the UX is ready.)
        .filter(gn => gn.status === 'Published')
        .slice(0, 3)
        .map<ProssimiGameNightCard>(gn => ({
          id: gn.id,
          title: gn.title,
          date: gn.scheduledAt,
          status: 'Published' as ProssimiStatus,
          rsvpConfirmedCount: gn.acceptedCount,
          rsvpPendingCount: gn.pendingCount,
          rsvpTotalCount: gn.totalInvited,
        }))
    );
  }, [upcomingGNQuery.data]);

  const prossimiState: ProssimiSectionState = deriveSectionState(
    upcomingGNQuery.isLoading,
    upcomingGNQuery.isError,
    prossimiCards.length
  );

  // ── Slot #2: Recenti (completed GameNights) ──────────────────────────────
  // BE endpoint for completed GameNights is not yet wired (out of scope T1).
  // For now we surface an empty section, which RecentiSection renders as `null`
  // per spec MAJ-6 (silent fallback). When the BE endpoint lands, swap the
  // empty array for the query result and adapt the projection.
  const recentiCards = useMemo<ReadonlyArray<RecentiGameNightCard>>(() => {
    return [];
  }, []);

  const recentiState: RecentiSectionState = deriveSectionState(
    /* isLoading */ false,
    /* isError */ false,
    recentiCards.length
  );

  // ── Slot #3: Suggested ("Potresti giocare") ──────────────────────────────
  // MVP algorithm: surface up to 6 owned games. Future BE endpoint
  // `GET /dashboard/suggestions` will refine to "owned NOT played last 30d
  // sorted by play count DESC" + collaborative filtering (plan §"MIN-2").
  const suggestedCards = useMemo<ReadonlyArray<SuggestedGameCard>>(() => {
    const games = gamesQuery.data?.games ?? [];
    return games.slice(0, 6).map<SuggestedGameCard>(g => {
      const min = g.minPlayers ?? 0;
      const max = g.maxPlayers ?? 0;
      const playerCount =
        min > 0 && max > 0 && min !== max
          ? `${min}-${max}`
          : min > 0
            ? `${min}`
            : max > 0
              ? `${max}`
              : '—';
      const durationMin = g.maxPlayTimeMinutes ?? g.minPlayTimeMinutes ?? 60;
      return {
        id: g.id,
        title: g.title,
        coverImageUrl: g.imageUrl ?? undefined,
        playerCount,
        durationMin,
      };
    });
  }, [gamesQuery.data]);

  const suggestedState: SuggestedSectionState = deriveSectionState(
    gamesQuery.isLoading,
    gamesQuery.isError,
    suggestedCards.length
  );

  // ── Slot #4: Friends Activity ────────────────────────────────────────────
  const friendsActivities = friendsActivityQuery.data ?? [];
  const friendsState: FriendsActivitySectionState = deriveSectionState(
    friendsActivityQuery.isLoading,
    friendsActivityQuery.isError,
    friendsActivities.length
  );

  // ── KPI assembly (preserved from Stage 3 — hero KPIs are independent of
  //    the 4 priority slots and remain meaningful as a snapshot at the top) ──
  const kpi = useMemo<DashboardHeroKpi>(
    () => ({
      games: statsQuery.data?.totalGames ?? 0,
      sessions: sessionsQuery.data?.total ?? undefined,
      hoursPlayed: undefined, // not yet exposed by backend
      winRate: undefined, // not yet exposed by backend
    }),
    [statsQuery.data, sessionsQuery.data]
  );

  // ── Hero labels (i18n-mediated) ──────────────────────────────────────────
  const heroLabels = {
    greetingMorning: t('pages.dashboard.hero.greetingMorning'),
    greetingAfternoon: t('pages.dashboard.hero.greetingAfternoon'),
    greetingEvening: t('pages.dashboard.hero.greetingEvening'),
    subtitle: t('pages.dashboard.hero.subtitle'),
    kpiGames: t('pages.dashboard.hero.kpiGames'),
    kpiSessions: t('pages.dashboard.hero.kpiSessions'),
    kpiHours: t('pages.dashboard.hero.kpiHours'),
    kpiWinRate: t('pages.dashboard.hero.kpiWinRate'),
  };

  return (
    <main data-slot="dashboard-client" className="flex w-full flex-col">
      <DashboardHero
        userName={user?.displayName ?? user?.email ?? t('pages.dashboard.hero.guestName')}
        kpi={kpi}
        labels={heroLabels}
      />

      <div
        data-slot="dashboard-priority-sections"
        className="container mx-auto flex flex-col gap-8 px-4 py-8 pb-16 sm:px-8"
      >
        <ProssimiSection
          state={prossimiState}
          gameNights={prossimiCards}
          onRetry={() => {
            void upcomingGNQuery.refetch();
          }}
        />

        <RecentiSection
          state={recentiState}
          gameNights={recentiCards}
          onRetry={() => {
            // Placeholder until BE completed-GN endpoint lands; we refetch the
            // upcoming source so the user gets *some* fresh data on retry.
            void upcomingGNQuery.refetch();
          }}
        />

        <SuggestedSection
          state={suggestedState}
          games={suggestedCards}
          onRetry={() => {
            void gamesQuery.refetch();
          }}
        />

        <FriendsActivitySection state={friendsState} activities={friendsActivities} />
      </div>

      {/* #1929 WP5: cascade-store driven drawer renderer for dashboard card clicks */}
      <CascadeDrawerHost />
    </main>
  );
}

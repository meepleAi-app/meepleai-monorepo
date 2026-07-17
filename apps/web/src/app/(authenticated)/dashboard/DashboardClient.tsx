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
 * Note on mockup divergence (#2114): `admin-mockups/design_files/sp4-dashboard.{html,jsx}`
 * is the legacy Pre-Stage-3 design with 5 entity sections — it is intentionally
 * NOT the design target for this component. The mockup is classified
 * `forward-refactor-obsolete` in its fidelity.json companion; the codebase
 * supersedes it via the Asse C refactor. There is no current pixel-faithful
 * mockup for the priority-driven layout; the spec lives in the plan archived
 * under `docs/superpowers/plans/` (2026-06-05 Asse C session).
 */

'use client';

import { useEffect, useMemo, type ReactElement } from 'react';

import Link from 'next/link';

import { useAuth } from '@/components/auth/AuthProvider';
import { CascadeDrawerHost } from '@/components/dashboard/CascadeDrawerHost';
import { HubPageContainer } from '@/components/layout/PageContainer';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { useActiveSessions } from '@/hooks/queries/useActiveSessions';
import {
  useCompletedGameNights,
  useRsvpGameNight,
  useUpcomingGameNights,
} from '@/hooks/queries/useGameNights';
import { useLibrary, useLibraryStats } from '@/hooks/queries/useLibrary';
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
  // #2978 (invariante #17): inline RSVP for pending-invitee cards on the dashboard.
  const rsvpMutation = useRsvpGameNight();
  // F20 #1974: dashboard "Recenti" slot — recently completed game nights.
  const completedGNQuery = useCompletedGameNights({ limit: 5 });
  const sessionsQuery = useActiveSessions(10);
  // Issue #2176: SuggestedSection renders "Potresti giocare" cards from the
  // user's OWN library (UserLibrary), NOT from the shared catalog (SharedGames).
  // The previous useGames(undefined,…,20) call read SharedGames system-wide,
  // which created a silent inconsistency: KPI "GIOCHI N" (useLibraryStats →
  // UserLibrary) and the suggested cards (useGames → SharedGames) drew from
  // different sources, so the section could render community catalog items
  // even when the user owned 0 games, or hide when the user owned N games
  // but the catalog query was throttled.
  const libraryQuery = useLibrary({ page: 1, pageSize: 20 });
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
          viewerRsvpStatus: gn.viewerRsvpStatus ?? null,
        }))
    );
  }, [upcomingGNQuery.data]);

  const prossimiState: ProssimiSectionState = deriveSectionState(
    upcomingGNQuery.isLoading,
    upcomingGNQuery.isError,
    prossimiCards.length
  );

  // ── Slot #2: Recenti (completed GameNights) ──────────────────────────────
  // F20 #1974 (audit 2026-06-07): wires the BE `/game-nights/completed`
  // endpoint. Projection mirrors `prossimiCards`: map the BE `GameNightDto` to
  // the `RecentiGameNightCard` contract that `RecentiSection` expects.
  // `sessionCount` now comes from the BE DTO (#3084 — a completed night has >= 1
  // session, so the card no longer shows a misleading "0 partite"). Mini cover
  // thumbnails are not yet exposed server-side so we pass an empty array (the
  // card primitive already hides the cover stack in that case).
  const recentiCards = useMemo<ReadonlyArray<RecentiGameNightCard>>(() => {
    const data = completedGNQuery.data ?? [];
    return data.slice(0, 3).map<RecentiGameNightCard>(gn => ({
      id: gn.id,
      title: gn.title,
      date: gn.scheduledAt,
      sessionCount: gn.sessionCount,
      gamePreviewThumbnails: [],
    }));
  }, [completedGNQuery.data]);

  const recentiState: RecentiSectionState = deriveSectionState(
    completedGNQuery.isLoading,
    completedGNQuery.isError,
    recentiCards.length
  );

  // ── Slot #3: Suggested ("Potresti giocare") ──────────────────────────────
  // MVP algorithm: surface up to 6 OWNED games from the user's UserLibrary.
  // Future BE endpoint `GET /dashboard/suggestions` will refine to
  // "owned NOT played last 30d sorted by play count DESC" + collaborative
  // filtering (plan §"MIN-2"). Source is UserLibrary (not SharedGames) —
  // see #2176 for why this matters.
  const suggestedCards = useMemo<ReadonlyArray<SuggestedGameCard>>(() => {
    const entries = libraryQuery.data?.items ?? [];
    return entries
      .filter(entry => entry.currentState === 'Owned' || entry.currentState === 'Nuovo')
      .slice(0, 6)
      .map<SuggestedGameCard>(entry => {
        const min = entry.minPlayers ?? 0;
        const max = entry.maxPlayers ?? 0;
        const playerCount =
          min > 0 && max > 0 && min !== max
            ? `${min}-${max}`
            : min > 0
              ? `${min}`
              : max > 0
                ? `${max}`
                : '—';
        const durationMin = entry.playingTimeMinutes ?? 60;
        return {
          id: entry.gameId,
          title: entry.gameTitle,
          coverImageUrl: entry.coverUrl ?? entry.gameImageUrl ?? undefined,
          playerCount,
          durationMin,
        };
      });
  }, [libraryQuery.data]);

  const suggestedState: SuggestedSectionState = deriveSectionState(
    libraryQuery.isLoading,
    libraryQuery.isError,
    suggestedCards.length
  );

  // Issue #2176 observability: detect cross-endpoint drift when stats reports
  // owned games but the library list returned 0 mappable entries. Fires once
  // per state transition (React StrictMode double-mount produces 2 warns in
  // dev, expected). Logged via console.warn in non-production builds —
  // production emits this as a Prometheus counter from the backend audit
  // layer instead.
  useEffect(() => {
    if (process.env.NODE_ENV === 'production') return;
    if (!statsQuery.data || libraryQuery.isLoading || libraryQuery.isError) return;
    if (statsQuery.data.totalGames > 0 && suggestedCards.length === 0) {
      console.warn(
        '[Dashboard] cross-endpoint drift: useLibraryStats.totalGames > 0 but SuggestedSection rendered 0 cards. ' +
          `stats=${statsQuery.data.totalGames}, libraryItems=${libraryQuery.data?.items.length ?? 0}, ` +
          'check filter (Owned/Nuovo) or useLibrary pagination.'
      );
    }
  }, [
    statsQuery.data,
    libraryQuery.isLoading,
    libraryQuery.isError,
    libraryQuery.data,
    suggestedCards.length,
  ]);

  // ── Block C (#2289 / #2247 follow-up): Agenti pronti ─────────────────────
  // Surfaces the user's library entries that already have an indexed KB
  // (`hasKb=true`, mapped from `UserLibraryEntry`). Mirrors the at-a-glance
  // chip wired on `/library` so the dashboard's headline answer to "which of
  // my games already have a chat-ready agent?" is one fold above the
  // suggested cards. The section is hidden when the list is empty so first-
  // login users with no KB-ready games still see a clean dashboard.
  const agentiProntiEntries = useMemo(
    () => (libraryQuery.data?.items ?? []).filter(entry => entry.hasKb).slice(0, 6),
    [libraryQuery.data]
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

      <HubPageContainer data-slot="dashboard-priority-sections" className="gap-8 py-8 pb-16">
        <ProssimiSection
          state={prossimiState}
          gameNights={prossimiCards}
          onRsvp={(id, response) => rsvpMutation.mutate({ id, response })}
          onRetry={() => {
            void upcomingGNQuery.refetch();
          }}
        />

        <RecentiSection
          state={recentiState}
          gameNights={recentiCards}
          onRetry={() => {
            // F20 #1974: wired to the completed-GN endpoint.
            void completedGNQuery.refetch();
          }}
        />

        <SuggestedSection
          state={suggestedState}
          games={suggestedCards}
          onRetry={() => {
            void libraryQuery.refetch();
          }}
        />

        {/* Block C (#2289 / #2247): inline section because the dashboard
            already adopts the "filter → render-if-non-empty → MeepleCard grid"
            pattern (see SuggestedSection internals) and a dedicated section
            primitive here would add a `Section/Empty/Error/Loading` family
            that's outside the scope of this follow-up — `hasKb` data piggy-
            backs on `libraryQuery`, so loading/error states are already
            covered upstream by SuggestedSection rendering. */}
        {agentiProntiEntries.length > 0 ? (
          <section
            data-slot="dashboard-agenti-pronti"
            aria-label="Giochi con agente pronto"
            className="flex flex-col gap-4"
          >
            <header className="flex items-baseline justify-between gap-3">
              <h2 className="font-display text-[18px] font-extrabold text-foreground">
                Giochi con agente pronto
              </h2>
              <Link
                href="/library?hasKb=true"
                className="font-display text-[12px] font-bold text-muted-foreground transition-colors hover:text-foreground"
              >
                Vedi tutti →
              </Link>
            </header>
            <ul
              role="list"
              className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-6"
            >
              {agentiProntiEntries.map(entry => (
                <li key={entry.id} role="listitem">
                  <Link
                    href={`/library/${entry.gameId}`}
                    className="block rounded-2xl focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                  >
                    <MeepleCard
                      entity="game"
                      variant="compact"
                      id={entry.id}
                      title={entry.gameTitle}
                      subtitle={entry.gamePublisher ?? undefined}
                      imageUrl={entry.coverUrl ?? entry.gameImageUrl ?? undefined}
                      rating={entry.averageRating ?? undefined}
                      ratingMax={10}
                      metadata={[
                        {
                          label:
                            entry.kbCardCount > 1
                              ? `📄 ${entry.kbIndexedCount}/${entry.kbCardCount} KB`
                              : '📄 KB',
                        },
                      ]}
                      headingLevel={3}
                    />
                  </Link>
                </li>
              ))}
            </ul>
          </section>
        ) : null}

        <FriendsActivitySection state={friendsState} activities={friendsActivities} />
      </HubPageContainer>

      {/* #1929 WP5: cascade-store driven drawer renderer for dashboard card clicks */}
      <CascadeDrawerHost />
    </main>
  );
}

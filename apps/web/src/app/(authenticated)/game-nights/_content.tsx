/**
 * GameNightsContent — v2 orchestrator for /game-nights index (Issue #1170 commit 3).
 *
 * Composes the v2 primitives from `components/features/game-nights/` (calendar,
 * list, header, drawer) driven by `useUpcomingGameNights` + `useMyGameNights`
 * + `useCurrentUser`. URL is the SSOT for `?view=calendar|list` and
 * `?filter=all|organizing|invited|completed`.
 *
 * Backend gaps (out of scope #1170, tracked):
 *   - per-event RSVP roster → `players={[]}` (avatar stack hidden).
 *   - per-event game title  → `gameTitle={undefined}` (chip hidden).
 *
 * Replaces the legacy `?tab=` upcoming/mine flat grid.
 */

'use client';

import { useEffect, useMemo, useState } from 'react';

import { useRouter, useSearchParams } from 'next/navigation';

import {
  CalendarMonthGrid,
  DayDetailDrawer,
  GameNightListCard,
  GameNightsHeader,
  type CalendarDayCellLabels,
  type CalendarMonthGridLabels,
  type DayDetailDrawerLabels,
  type FilterPillBarLabels,
  type GameNightListCardLabels,
  type GameNightsHeaderLabels,
  type GameNightsView,
  type StatusPillLabels,
} from '@/components/features/game-nights';
import { useCurrentUser } from '@/hooks/queries/useCurrentUser';
import { useMyGameNights, useUpcomingGameNights } from '@/hooks/queries/useGameNights';
import { useTranslation, type TranslationFunction } from '@/hooks/useTranslation';
import type { MonthCell } from '@/lib/game-nights/calendar-grid';
import { filterEvents, isFilterKey, type FilterKey } from '@/lib/game-nights/event-filter';
import { groupByMonth } from '@/lib/game-nights/event-grouping';
import { toGameNightVM, type GameNightVM } from '@/lib/game-nights/view-model';
import { useRecentsStore } from '@/stores/use-recents';

function monthAbbrev(year: number, month: number): string {
  return new Date(year, month, 1)
    .toLocaleDateString(undefined, { month: 'short' })
    .replace(/\.$/, '');
}

function monthLong(year: number, month: number): string {
  return new Date(year, month, 1).toLocaleDateString(undefined, {
    month: 'long',
    year: 'numeric',
  });
}

const K = 'gameNightsIndex';

function makeCardLabels(
  t: TranslationFunction,
  statusLabels: StatusPillLabels,
  vm: GameNightVM
): GameNightListCardLabels {
  return {
    status: statusLabels,
    organizingBadge: t(`${K}.list.organizingBadge`),
    participants: (count: number) => t(`${K}.list.participants`, { count }),
    cta: {
      edit: t(`${K}.list.cta.edit`),
      viewSummary: t(`${K}.list.cta.viewSummary`),
      reschedule: t(`${K}.list.cta.reschedule`),
      accept: t(`${K}.list.cta.accept`),
      maybe: t(`${K}.list.cta.maybe`),
    },
    monthAbbrev: monthAbbrev(vm.year, vm.month),
  };
}

export function GameNightsContent(): React.JSX.Element {
  const { t } = useTranslation();
  const router = useRouter();
  const sp = useSearchParams();

  // Register in recents (parity with legacy).
  useEffect(() => {
    useRecentsStore.getState().push({
      id: 'section-game-nights',
      entity: 'event',
      title: 'Game Nights',
      href: '/game-nights',
    });
  }, []);

  // URL state SSOT.
  const rawView = sp.get('view');
  const view: GameNightsView = rawView === 'list' ? 'list' : 'calendar';
  const rawFilter = sp.get('filter');
  const filter: FilterKey = isFilterKey(rawFilter) ? rawFilter : 'all';

  function setParam(key: string, value: string): void {
    const next = new URLSearchParams(sp.toString());
    next.set(key, value);
    router.replace(`/game-nights?${next.toString()}`, { scroll: false });
  }

  // Data hooks.
  const { data: viewer } = useCurrentUser();
  const upcoming = useUpcomingGameNights();
  const mine = useMyGameNights();

  // Merge + dedup by id; map to VMs.
  const allVms = useMemo<readonly GameNightVM[]>(() => {
    const byId = new Map<string, GameNightVM>();
    for (const dto of upcoming.data ?? []) {
      byId.set(dto.id, toGameNightVM(dto, viewer?.id ?? null));
    }
    for (const dto of mine.data ?? []) {
      if (byId.has(dto.id)) continue;
      byId.set(dto.id, toGameNightVM(dto, viewer?.id ?? null));
    }
    return Array.from(byId.values());
  }, [upcoming.data, mine.data, viewer?.id]);

  // Today / "this month" header counters (always against the unfiltered set so
  // the header reflects the underlying month volume, not the active filter).
  const today = useMemo(() => new Date(), []);
  const year = today.getFullYear();
  const month = today.getMonth();
  const thisMonthVms = useMemo(
    () => allVms.filter(v => v.year === year && v.month === month),
    [allVms, year, month]
  );
  const totalThisMonth = thisMonthVms.length;
  const confirmedThisMonth = thisMonthVms.filter(v => v.statusKey === 'confirmed').length;

  // Filtered set drives both calendar + list.
  const filtered = useMemo(() => filterEvents(allVms, filter), [allVms, filter]);
  const cancelledInFiltered = useMemo(
    () => filtered.filter(v => v.statusKey === 'cancelled').length,
    [filtered]
  );

  // Day-drawer state.
  const [drawerCell, setDrawerCell] = useState<MonthCell | null>(null);
  const dayEvents = useMemo<readonly GameNightVM[]>(() => {
    if (!drawerCell) return [];
    return filtered.filter(v => v.year === year && v.month === month && v.day === drawerCell.day);
  }, [drawerCell, filtered, year, month]);

  // ── Label dictionaries (memoised on translator + dependent counts). ──
  const statusLabels: StatusPillLabels = useMemo(
    () => ({
      confirmed: t(`${K}.status.confirmed`),
      planned: t(`${K}.status.planned`),
      cancelled: t(`${K}.status.cancelled`),
      completed: t(`${K}.status.completed`),
    }),
    [t]
  );

  const filterLabels: FilterPillBarLabels = useMemo(
    () => ({
      ariaLabel: t(`${K}.filter.ariaLabel`),
      all: t(`${K}.filter.all`),
      organizing: t(`${K}.filter.organizing`),
      invited: t(`${K}.filter.invited`),
      completed: t(`${K}.filter.completed`),
    }),
    [t]
  );

  const headerLabels: GameNightsHeaderLabels = useMemo(
    () => ({
      kicker: t(`${K}.header.kicker`),
      title: t(`${K}.header.title`),
      countLine: t(`${K}.header.countLine`, {
        total: totalThisMonth,
        confirmed: confirmedThisMonth,
      }),
      ctaNew: t(`${K}.header.ctaNew`),
      viewTablistAriaLabel: t(`${K}.view.tablistAriaLabel`),
      viewCalendar: t(`${K}.view.calendar`),
      viewList: t(`${K}.view.list`),
      filter: filterLabels,
    }),
    [t, totalThisMonth, confirmedThisMonth, filterLabels]
  );

  const cellLabels: CalendarDayCellLabels = useMemo(
    () => ({
      todayBadge: t(`${K}.calendar.todayBadge`),
      dayAriaLabel: (day: number, events: number) =>
        t(`${K}.calendar.dayAriaLabel`, { day, events }),
      overflow: (count: number) => t(`${K}.calendar.overflow`, { count }),
    }),
    [t]
  );

  const monthGridLabels: CalendarMonthGridLabels = useMemo(
    () => ({
      prevMonth: t(`${K}.calendar.prevMonth`),
      nextMonth: t(`${K}.calendar.nextMonth`),
      today: t(`${K}.calendar.today`),
      monthHeading: monthLong(year, month),
      dayLabels: t(`${K}.calendar.dayLabels`).split(','),
      footerCount: t(`${K}.calendar.footerCount`, {
        total: filtered.length,
        cancelled: cancelledInFiltered,
      }),
      legendEvent: t(`${K}.calendar.legend.event`),
      legendCancelled: t(`${K}.calendar.legend.cancelled`),
      legendToday: t(`${K}.calendar.legend.today`),
      cell: cellLabels,
    }),
    [t, year, month, filtered.length, cancelledInFiltered, cellLabels]
  );

  // ── State branches ────────────────────────────────────────────────
  // Loading: only show skeleton on first load (both queries pending).
  const isInitialLoading = upcoming.isLoading && mine.isLoading && allVms.length === 0;
  const hasError = (upcoming.error || mine.error) && allVms.length === 0;

  if (hasError) {
    return (
      <div
        data-testid="game-nights-error"
        className="flex flex-col items-center gap-3 px-4 py-12 text-center"
      >
        <h2 className="font-display text-lg font-extrabold text-foreground">
          {t(`${K}.states.error.title`)}
        </h2>
        <p className="font-mono text-sm text-muted-foreground">{t(`${K}.states.error.body`)}</p>
        <button
          type="button"
          onClick={() => {
            void upcoming.refetch();
            void mine.refetch();
          }}
          className="rounded-md bg-entity-event px-4 py-2 font-display text-sm font-extrabold text-white"
        >
          {t(`${K}.states.error.retry`)}
        </button>
      </div>
    );
  }

  if (isInitialLoading) {
    return <GameNightsLoadingSkeleton />;
  }

  // ── Header always renders (with zeroed counts in true-empty state). ──
  const headerEl = (
    <GameNightsHeader
      view={view}
      onViewChange={(v: GameNightsView) => setParam('view', v)}
      filter={filter}
      onFilterChange={(k: FilterKey) => setParam('filter', k)}
      onCreate={() => router.push('/game-nights/new')}
      labels={headerLabels}
    />
  );

  // True empty: no events at all.
  if (allVms.length === 0) {
    return (
      <>
        {headerEl}
        <EmptyState
          title={t(`${K}.states.empty.title`)}
          body={t(`${K}.states.empty.body`)}
          cta={t(`${K}.states.empty.cta`)}
          onCta={() => router.push('/game-nights/new')}
        />
      </>
    );
  }

  // Drawer labels (only built when drawerCell present).
  const drawerLabels: DayDetailDrawerLabels | null = drawerCell
    ? {
        title: t(`${K}.drawer.title`, {
          day: drawerCell.day,
          month: new Date(year, month, 1).toLocaleString(undefined, { month: 'long' }),
          year,
        }),
        subtitle: t(`${K}.drawer.subtitle`, { count: dayEvents.length }),
        close: t(`${K}.drawer.close`),
        addOnDay: t(`${K}.drawer.addOnDay`),
        card: {
          status: statusLabels,
          organizingBadge: t(`${K}.list.organizingBadge`),
          participants: (count: number) => t(`${K}.list.participants`, { count }),
          cta: {
            edit: t(`${K}.list.cta.edit`),
            viewSummary: t(`${K}.list.cta.viewSummary`),
            reschedule: t(`${K}.list.cta.reschedule`),
            accept: t(`${K}.list.cta.accept`),
            maybe: t(`${K}.list.cta.maybe`),
          },
          monthAbbrev: monthAbbrev(year, month),
        },
        monthAbbrev: monthAbbrev(year, month),
      }
    : null;

  return (
    <>
      {headerEl}
      {view === 'calendar' ? (
        <CalendarMonthGrid
          year={year}
          month={month}
          events={filtered}
          today={today}
          labels={monthGridLabels}
          onDayClick={cell => setDrawerCell(cell)}
        />
      ) : (
        <ListView
          events={filtered}
          now={today}
          t={t}
          statusLabels={statusLabels}
          emptyTitle={t(`${K}.states.filteredEmpty.title`)}
          emptyBody={t(`${K}.states.filteredEmpty.body`)}
        />
      )}
      {drawerCell && drawerLabels && (
        <DayDetailDrawer
          open
          day={drawerCell.day}
          events={dayEvents}
          labels={drawerLabels}
          onClose={() => setDrawerCell(null)}
          onAddOnDay={() => router.push('/game-nights/new')}
        />
      )}
    </>
  );
}

// ─── ListView ──────────────────────────────────────────────────────────────

interface ListViewProps {
  readonly events: readonly GameNightVM[];
  readonly now: Date;
  readonly t: TranslationFunction;
  readonly statusLabels: StatusPillLabels;
  readonly emptyTitle: string;
  readonly emptyBody: string;
}

function ListView({
  events,
  now,
  t,
  statusLabels,
  emptyTitle,
  emptyBody,
}: ListViewProps): React.JSX.Element {
  const groups = useMemo(() => groupByMonth(events, now), [events, now]);

  if (groups.length === 0) {
    return (
      <div
        data-testid="game-nights-list-empty"
        className="flex flex-col items-center gap-2 px-4 py-12 text-center"
      >
        <h2 className="font-display text-base font-extrabold text-foreground">{emptyTitle}</h2>
        <p className="font-mono text-sm text-muted-foreground">{emptyBody}</p>
      </div>
    );
  }

  return (
    <section
      data-testid="game-nights-list"
      className="flex flex-col gap-4 px-4 pb-6 pt-3 md:px-8 md:pt-5 md:pb-8"
    >
      {groups.map(group => {
        const heading = monthLong(group.year, group.month);
        return (
          <div key={`${group.year}-${group.month}`} className="flex flex-col gap-2.5">
            <header className="sticky top-0 z-[5] flex items-baseline justify-between bg-background py-2">
              <h2 className="font-display text-sm font-extrabold uppercase tracking-wider text-foreground">
                {heading}
              </h2>
              <span className="font-mono text-xs font-bold text-muted-foreground">
                {t(`${K}.list.groupCount`, { count: group.items.length })}
              </span>
            </header>
            <div className="flex flex-col gap-2.5">
              {group.items.map(vm => (
                <GameNightListCard
                  key={vm.id}
                  vm={vm}
                  labels={makeCardLabels(t, statusLabels, vm)}
                />
              ))}
            </div>
          </div>
        );
      })}
    </section>
  );
}

// ─── Empty state ───────────────────────────────────────────────────────────

interface EmptyStateProps {
  readonly title: string;
  readonly body: string;
  readonly cta: string;
  readonly onCta: () => void;
}

function EmptyState({ title, body, cta, onCta }: EmptyStateProps): React.JSX.Element {
  return (
    <div
      data-testid="game-nights-empty"
      className="flex flex-col items-center gap-3 px-4 py-12 text-center"
    >
      <h2 className="font-display text-lg font-extrabold text-foreground">{title}</h2>
      <p className="max-w-md font-mono text-sm text-muted-foreground">{body}</p>
      <button
        type="button"
        onClick={onCta}
        className="rounded-md bg-gradient-to-br from-entity-event to-entity-session px-4 py-2.5 font-display text-sm font-extrabold text-white shadow-md shadow-entity-event/30"
      >
        {cta}
      </button>
    </div>
  );
}

// ─── Loading skeleton (preserved for Suspense fallback) ────────────────────

export function GameNightsLoadingSkeleton(): React.JSX.Element {
  return (
    <div data-testid="game-nights-loading" className="flex flex-col gap-3 px-4 py-6 md:px-8">
      {Array.from({ length: 4 }).map((_, i) => (
        <div key={i} className="h-28 animate-pulse rounded-lg bg-muted" />
      ))}
    </div>
  );
}

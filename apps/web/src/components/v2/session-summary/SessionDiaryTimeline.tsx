/**
 * SessionDiaryTimeline — Wave D.3 v2 component (Issue #756).
 *
 * Filterable diary timeline grouped by turn. Filter pills sit at the top
 * (rovingtab via `useTablistKeyboardNav`); the timeline area renders one
 * `<details>`-style turn group per turn, expandable independently.
 *
 * Mockup mapping:
 *   - admin-mockups/design_files/sp4-session-summary.jsx (Diary)
 *
 * Contract reference: docs/frontend/contracts/sessions-id-summary-hooks.md §5.7.
 *
 * MeepleCard divergence (Gate C): a vertical event timeline with grouped
 * turn collapsibles + filter pills. MeepleCard cannot host this composition
 * (multi-row turn groups, internal expand/collapse buttons). DIVERGE.
 *
 * A11y:
 *   - Filter pills use `useTablistKeyboardNav` (Wave A.6 reusable hook). The
 *     active filter has `tabIndex=0` + `aria-pressed=true`; siblings
 *     `tabIndex=-1` and `aria-pressed=false`.
 *   - Each turn collapsible: `<button aria-expanded={open} aria-controls={id}>`
 *     + sibling `<div id={id}>` (when open).
 *
 * Pure component: orchestrator owns `filter` URL state + `expandedTurns` set.
 */

'use client';

import type { ReactElement } from 'react';
import { useId } from 'react';

import clsx from 'clsx';

import { useTablistKeyboardNav } from '@/hooks/useTablistKeyboardNav';
import type { DiaryEntryDto } from '@/lib/api/session-flow/types';

export type DiaryFilter = 'all' | 'score' | 'event' | 'chat' | 'photo';

export interface SessionDiaryTimelineLabels {
  readonly title: string;
  readonly filterAll: string;
  readonly filterScore: string;
  readonly filterEvent: string;
  readonly filterChat: string;
  readonly filterPhoto: string;
  readonly empty: string;
  /** Pre-resolved aria-label per (turn, isExpanded) — orchestrator handles ICU. */
  readonly toggleAriaLabel: (turn: number, isExpanded: boolean) => string;
  readonly turnLabel: (turn: number) => string;
  /** Resolved event count text (handles ICU plural) per turn-events count. */
  readonly turnEventsCount: (count: number) => string;
}

/**
 * Visual diary turn group — orchestrator pre-groups DiaryEntryDto[] by turn
 * (since DiaryEntryDto carries no built-in grouping field; orchestrator
 * derives turn from payload JSON or eventType).
 */
export interface DiaryTurnGroup {
  readonly turn: number;
  readonly events: readonly DiaryEntryDto[];
}

export interface SessionDiaryTimelineProps {
  readonly turns: readonly DiaryTurnGroup[];
  readonly activeFilter: DiaryFilter;
  readonly onFilterChange: (next: DiaryFilter) => void;
  readonly expandedTurns: ReadonlySet<number>;
  readonly onToggleTurn: (turn: number) => void;
  readonly labels: SessionDiaryTimelineLabels;
  readonly className?: string;
}

const FILTER_ORDER: ReadonlyArray<DiaryFilter> = ['all', 'score', 'event', 'chat', 'photo'];

/**
 * Map each diary `eventType` payload string to a coarse filter category.
 * The mapping mirrors the mockup `eventColor`: score events keep `score`,
 * event-types like `turn_advanced/session_started` map to `event`, etc.
 */
function classifyEvent(eventType: string): DiaryFilter {
  if (eventType.includes('score')) return 'score';
  if (eventType.includes('chat')) return 'chat';
  if (eventType.includes('photo') || eventType.includes('snapshot')) return 'photo';
  return 'event';
}

export function SessionDiaryTimeline({
  turns,
  activeFilter,
  onFilterChange,
  expandedTurns,
  onToggleTurn,
  labels,
  className,
}: SessionDiaryTimelineProps): ReactElement {
  const baseId = useId();

  const filterLabels: Record<DiaryFilter, string> = {
    all: labels.filterAll,
    score: labels.filterScore,
    event: labels.filterEvent,
    chat: labels.filterChat,
    photo: labels.filterPhoto,
  };

  const { tabRefs, handleKeyDown } = useTablistKeyboardNav<DiaryFilter>({
    orderedKeys: FILTER_ORDER,
    onChange: onFilterChange,
  });

  // Filter turns: keep only events matching the active filter, then drop
  // turn groups that become empty post-filter.
  const visibleTurns = turns
    .map(t => ({
      ...t,
      events:
        activeFilter === 'all'
          ? t.events
          : t.events.filter(e => classifyEvent(e.eventType) === activeFilter),
    }))
    .filter(t => t.events.length > 0);

  return (
    <section
      data-slot="session-diary-timeline"
      className={clsx('flex flex-col gap-2.5', className)}
    >
      <div className="flex items-baseline justify-between">
        <h3 className="font-display text-base font-extrabold text-foreground">
          <span aria-hidden="true" className="mr-1.5">
            📜
          </span>
          {labels.title}
        </h3>
      </div>
      <div role="tablist" className="flex flex-wrap gap-1">
        {FILTER_ORDER.map(filter => {
          const active = filter === activeFilter;
          return (
            <button
              key={filter}
              type="button"
              role="tab"
              ref={node => {
                if (node) tabRefs.current.set(filter, node);
                else tabRefs.current.delete(filter);
              }}
              tabIndex={active ? 0 : -1}
              aria-pressed={active}
              aria-selected={active}
              onKeyDown={e => handleKeyDown(e, filter)}
              onClick={() => onFilterChange(filter)}
              data-slot="diary-filter-pill"
              data-filter={filter}
              data-active={active || undefined}
              className={clsx(
                'rounded-full border px-3 py-1 font-display text-[11px] font-bold transition-colors',
                'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
                active
                  ? 'border-[hsla(240,60%,55%,0.4)] bg-[hsla(240,60%,55%,0.14)] text-[hsl(240,60%,45%)]'
                  : 'border-border bg-card text-muted-foreground hover:bg-muted/40'
              )}
            >
              {filterLabels[filter]}
            </button>
          );
        })}
      </div>
      {visibleTurns.length === 0 ? (
        <div
          data-slot="diary-empty"
          className="rounded-lg border border-dashed border-border bg-card p-6 text-center"
        >
          <p className="text-sm text-muted-foreground">{labels.empty}</p>
        </div>
      ) : (
        <ol
          data-slot="diary-turn-list"
          className="overflow-hidden rounded-lg border border-border bg-card"
        >
          {visibleTurns.map((t, idx) => {
            const open = expandedTurns.has(t.turn);
            const panelId = `${baseId}-turn-${t.turn}`;
            return (
              <li
                key={t.turn}
                data-slot="diary-turn-item"
                data-turn={t.turn}
                data-open={open || undefined}
                className={clsx(idx < visibleTurns.length - 1 && 'border-b border-border/60')}
              >
                <button
                  type="button"
                  onClick={() => onToggleTurn(t.turn)}
                  aria-expanded={open}
                  aria-controls={panelId}
                  aria-label={labels.toggleAriaLabel(t.turn, open)}
                  className="flex w-full items-center gap-2.5 px-3 py-2.5 text-left text-foreground hover:bg-muted/30"
                  data-slot="diary-turn-toggle"
                >
                  <span className="rounded-full bg-[hsla(240,60%,55%,0.12)] px-2 py-0.5 font-mono text-[10px] font-extrabold uppercase tracking-wide text-[hsl(240,60%,45%)]">
                    {labels.turnLabel(t.turn)}
                  </span>
                  <span className="font-mono text-[11px] font-bold text-muted-foreground">
                    {labels.turnEventsCount(t.events.length)}
                  </span>
                  <span className="flex-1" />
                  <span aria-hidden="true" className="text-xs text-muted-foreground">
                    {open ? '▴' : '▾'}
                  </span>
                </button>
                {open && (
                  <ul
                    id={panelId}
                    role="list"
                    data-slot="diary-events"
                    className="flex flex-col gap-1.5 px-3 pb-3 pt-0.5"
                  >
                    {t.events.map(e => (
                      <li
                        key={e.id}
                        data-slot="diary-event"
                        data-event-type={e.eventType}
                        className="flex items-start gap-2 pl-3"
                      >
                        <span
                          aria-hidden="true"
                          className="mt-0.5 inline-flex h-5 w-5 shrink-0 items-center justify-center rounded-full bg-[hsla(240,60%,55%,0.14)] text-[10px]"
                        >
                          •
                        </span>
                        <div className="flex-1 text-xs text-muted-foreground">
                          <span className="mr-2 font-mono text-[10px] font-extrabold uppercase tracking-wide text-foreground/70">
                            {new Date(e.timestamp).toLocaleTimeString(undefined, {
                              hour: '2-digit',
                              minute: '2-digit',
                            })}
                          </span>
                          {e.eventType}
                        </div>
                      </li>
                    ))}
                  </ul>
                )}
              </li>
            );
          })}
        </ol>
      )}
    </section>
  );
}

'use client';

import Link from 'next/link';

import { DashboardSection } from './DashboardSection';

export interface EventListItem {
  readonly id: string;
  readonly title: string;
  /** ISO datetime. */
  readonly startsAt: string;
  readonly location: string | null;
  readonly confirmedCount: number;
  readonly pendingCount: number;
}

export interface EventsListLabels {
  readonly title: string;
  readonly viewAllLabel: string;
  readonly viewAllHref: string;
  readonly participantsTemplate: string;
  readonly emptyTitle: string;
  readonly emptyCta: string;
  readonly emptyCtaHref: string;
}

export interface EventsListProps {
  readonly events: ReadonlyArray<EventListItem>;
  readonly labels: EventsListLabels;
  readonly onViewAllClick?: (sectionId: string, viewAllHref: string) => void;
  readonly onEmptyCtaClick?: (sectionId: string, ctaHref: string) => void;
}

function formatDateBadge(iso: string): { day: string; month: string } {
  try {
    const date = new Date(iso);
    const day = date.getDate().toString().padStart(2, '0');
    const month = date.toLocaleString(undefined, { month: 'short' }).toUpperCase().replace('.', '');
    return { day, month };
  } catch {
    return { day: '—', month: '—' };
  }
}

export function EventsList({ events, labels, onViewAllClick, onEmptyCtaClick }: EventsListProps) {
  const top = events.slice(0, 3);

  return (
    <DashboardSection
      sectionId="events"
      icon="📅"
      title={labels.title}
      count={events.length}
      viewAllLabel={labels.viewAllLabel}
      viewAllHref={labels.viewAllHref}
      onViewAllClick={onViewAllClick}
      fullWidth
    >
      {top.length === 0 ? (
        <div className="flex flex-col items-center gap-2 rounded-xl border border-dashed border-border bg-muted/30 px-3 py-6 text-center">
          <span aria-hidden="true" className="text-3xl">
            📅
          </span>
          <p className="text-sm text-muted-foreground">{labels.emptyTitle}</p>
          <Link
            href={labels.emptyCtaHref}
            onClick={() => onEmptyCtaClick?.('events', labels.emptyCtaHref)}
            className="mt-1 inline-flex items-center rounded-lg bg-foreground px-3 py-1.5 font-bold font-[Quicksand] text-xs text-background"
          >
            {labels.emptyCta}
          </Link>
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-2 sm:grid-cols-3">
          {top.map(e => {
            const badge = formatDateBadge(e.startsAt);
            return (
              <Link
                key={e.id}
                href={`/game-nights/${e.id}`}
                data-slot="dashboard-event-card"
                className="flex items-center gap-3 rounded-lg border border-border bg-card p-3 hover:border-border-strong"
              >
                <div
                  aria-hidden="true"
                  className="flex h-12 w-12 shrink-0 flex-col items-center justify-center rounded-md bg-[hsl(var(--c-event)/0.12)] text-[hsl(var(--c-event))]"
                >
                  <span className="font-mono text-[9px] font-bold uppercase tracking-wider">
                    {badge.month}
                  </span>
                  <span className="font-bold font-[Quicksand] text-base leading-tight tabular-nums">
                    {badge.day}
                  </span>
                </div>
                <div className="min-w-0 flex-1">
                  <div className="line-clamp-1 font-bold font-[Quicksand] text-sm text-foreground">
                    {e.title}
                  </div>
                  <div className="line-clamp-1 font-mono text-[10px] text-muted-foreground">
                    {e.location ?? '—'} ·{' '}
                    {labels.participantsTemplate
                      .replace('{confirmed}', String(e.confirmedCount))
                      .replace('{total}', String(e.confirmedCount + e.pendingCount))}
                  </div>
                </div>
              </Link>
            );
          })}
        </div>
      )}
    </DashboardSection>
  );
}

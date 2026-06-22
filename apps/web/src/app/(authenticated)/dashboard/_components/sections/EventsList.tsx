'use client';

import Link from 'next/link';

import { DashboardSection } from './DashboardSection';
import { EmptySection } from './EmptySection';

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

function formatTime(iso: string): string {
  try {
    const date = new Date(iso);
    return date.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
  } catch {
    return '';
  }
}

export function EventsList({ events, labels, onViewAllClick, onEmptyCtaClick }: EventsListProps) {
  const top = events.slice(0, 3);

  return (
    <DashboardSection
      sectionId="events"
      entity="event"
      icon="📅"
      title={labels.title}
      count={events.length}
      viewAllLabel={labels.viewAllLabel}
      viewAllHref={labels.viewAllHref}
      onViewAllClick={onViewAllClick}
      fullWidth
    >
      {top.length === 0 ? (
        <EmptySection
          entity="event"
          icon="📅"
          message={labels.emptyTitle}
          cta={labels.emptyCta}
          ctaHref={labels.emptyCtaHref}
          onCtaClick={() => onEmptyCtaClick?.('events', labels.emptyCtaHref)}
        />
      ) : (
        <div className="grid grid-cols-1 gap-2.5 sm:grid-cols-3">
          {top.map(e => {
            const badge = formatDateBadge(e.startsAt);
            const time = formatTime(e.startsAt);
            const locationLabel = e.location ? `📍 ${e.location}` : '';
            const subtitle = [time, locationLabel].filter(Boolean).join(' · ');
            const total = e.confirmedCount + e.pendingCount;

            return (
              <Link
                key={e.id}
                href={`/game-nights/${e.id}`}
                data-slot="dashboard-event-card"
                className="flex items-center gap-3 rounded-[10px] border border-border bg-background p-3 transition-colors hover:border-border-strong"
              >
                <div
                  aria-hidden="true"
                  className="flex h-[52px] w-[52px] shrink-0 flex-col items-center justify-center rounded-[10px] border"
                  style={{
                    background: 'hsl(var(--c-event) / 0.1)',
                    color: 'hsl(var(--c-event))',
                    borderColor: 'hsl(var(--c-event) / 0.25)',
                  }}
                >
                  <span className="font-mono text-[9px] font-extrabold uppercase leading-none">
                    {badge.month}
                  </span>
                  <span className="font-quicksand text-[20px] font-extrabold leading-none tabular-nums">
                    {badge.day}
                  </span>
                </div>
                <div className="min-w-0 flex-1">
                  <div className="mb-0.5 line-clamp-1 font-quicksand text-[13px] font-extrabold text-foreground">
                    {e.title}
                  </div>
                  <div className="mb-1 line-clamp-1 font-mono text-[10px] font-semibold text-muted-foreground">
                    {subtitle || '—'}
                  </div>
                  <div
                    className="inline-flex items-center gap-1 rounded-full px-1.5 py-0.5 font-mono text-[9px] font-bold"
                    style={{
                      background: 'hsl(var(--c-player) / 0.12)',
                      color: 'hsl(var(--c-player))',
                    }}
                  >
                    <span aria-hidden="true">👤</span>
                    {labels.participantsTemplate
                      .replace('{confirmed}', String(e.confirmedCount))
                      .replace('{total}', String(total))}
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

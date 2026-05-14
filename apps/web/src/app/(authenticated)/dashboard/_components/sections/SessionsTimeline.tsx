'use client';

import Link from 'next/link';

import { DashboardSection } from './DashboardSection';

export interface SessionTimelineItem {
  readonly id: string;
  readonly title: string;
  readonly status: 'live' | 'completed' | 'paused' | 'setup' | 'abandoned';
  readonly playerCount: number;
  readonly durationMinutes: number | null;
}

export interface SessionsTimelineLabels {
  readonly title: string;
  readonly liveBadge: string;
  readonly viewAllLabel: string;
  readonly viewAllHref: string;
  readonly playerCountTemplate: string;
  readonly minutesTemplate: string;
  readonly emptyTitle: string;
  readonly emptyCta: string;
  readonly emptyCtaHref: string;
  readonly statusLabels: Readonly<{
    live: string;
    completed: string;
    paused: string;
    setup: string;
    abandoned: string;
  }>;
}

export interface SessionsTimelineProps {
  readonly sessions: ReadonlyArray<SessionTimelineItem>;
  readonly totalCount: number;
  readonly labels: SessionsTimelineLabels;
  readonly onViewAllClick?: (sectionId: string, viewAllHref: string) => void;
  readonly onEmptyCtaClick?: (sectionId: string, ctaHref: string) => void;
}

export function SessionsTimeline({
  sessions,
  totalCount,
  labels,
  onViewAllClick,
  onEmptyCtaClick,
}: SessionsTimelineProps) {
  const top = sessions.slice(0, 3);
  const hasLive = top.some(s => s.status === 'live');

  return (
    <DashboardSection
      sectionId="sessions"
      icon={hasLive ? '🔴' : '🎯'}
      title={hasLive ? `${labels.title} · ` : labels.title}
      count={totalCount}
      viewAllLabel={labels.viewAllLabel}
      viewAllHref={labels.viewAllHref}
      onViewAllClick={onViewAllClick}
    >
      {hasLive && (
        <div
          data-slot="dashboard-sessions-live-badge"
          aria-live="polite"
          className="-mt-1 mb-2 inline-flex items-center gap-1.5 self-start rounded-full bg-[hsl(var(--c-session)/0.15)] px-2 py-0.5 font-mono text-[10px] font-extrabold uppercase tracking-wider text-[hsl(var(--c-session))]"
        >
          <span aria-hidden="true" className="relative flex h-2 w-2">
            <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-[hsl(var(--c-session))] opacity-75" />
            <span className="relative inline-flex h-2 w-2 rounded-full bg-[hsl(var(--c-session))]" />
          </span>
          {labels.liveBadge}
        </div>
      )}
      {top.length === 0 ? (
        <div className="flex flex-col items-center gap-2 rounded-xl border border-dashed border-border bg-muted/30 px-3 py-6 text-center">
          <span aria-hidden="true" className="text-3xl">
            🎯
          </span>
          <p className="text-sm text-muted-foreground">{labels.emptyTitle}</p>
          <Link
            href={labels.emptyCtaHref}
            onClick={() => onEmptyCtaClick?.('sessions', labels.emptyCtaHref)}
            className="mt-1 inline-flex items-center rounded-lg bg-foreground px-3 py-1.5 font-bold font-[Quicksand] text-xs text-background"
          >
            {labels.emptyCta}
          </Link>
        </div>
      ) : (
        <div className="flex flex-col gap-2">
          {top.map(s => (
            <Link
              key={s.id}
              href={`/sessions/${s.id}`}
              data-slot="dashboard-session-row"
              data-status={s.status}
              className="flex items-center gap-3 rounded-lg border border-border bg-card p-2 hover:border-border-strong"
            >
              <div
                aria-hidden="true"
                className="flex h-8 w-8 shrink-0 items-center justify-center rounded bg-[hsl(var(--c-session)/0.12)] text-base text-[hsl(var(--c-session))]"
              >
                {s.status === 'live' ? '▶️' : '🎯'}
              </div>
              <div className="min-w-0 flex-1">
                <div className="line-clamp-1 font-bold font-[Quicksand] text-xs text-foreground">
                  {s.title}
                </div>
                <div className="line-clamp-1 font-mono text-[9px] text-muted-foreground">
                  {labels.statusLabels[s.status]} ·{' '}
                  {labels.playerCountTemplate.replace('{count}', String(s.playerCount))}
                  {s.durationMinutes != null
                    ? ` · ${labels.minutesTemplate.replace('{count}', String(s.durationMinutes))}`
                    : ''}
                </div>
              </div>
            </Link>
          ))}
        </div>
      )}
    </DashboardSection>
  );
}

'use client';

import Link from 'next/link';

import { DashboardSection } from './DashboardSection';
import { EmptySection } from './EmptySection';

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

/** Deterministic gradient from session id (matches mockup `grad(h,s)`). */
function sessionGradient(seed: string): string {
  let h = 0;
  for (let i = 0; i < seed.length; i++) h = (h * 31 + seed.charCodeAt(i)) % 360;
  const h2 = (h + 340) % 360;
  const h3 = (h + 30) % 360;
  return `linear-gradient(135deg, hsl(${h}, 60%, 55%), hsl(${h2}, 40%, 30%) 60%, hsl(${h3}, 50%, 40%))`;
}

export function SessionsTimeline({
  sessions,
  totalCount,
  labels,
  onViewAllClick,
  onEmptyCtaClick,
}: SessionsTimelineProps) {
  const top = sessions.slice(0, 3);

  return (
    <DashboardSection
      sectionId="sessions"
      entity="session"
      icon="🎯"
      title={labels.title}
      count={totalCount}
      viewAllLabel={labels.viewAllLabel}
      viewAllHref={labels.viewAllHref}
      onViewAllClick={onViewAllClick}
    >
      {top.length === 0 ? (
        <EmptySection
          entity="session"
          icon="🎯"
          message={labels.emptyTitle}
          cta={labels.emptyCta}
          ctaHref={labels.emptyCtaHref}
          onCtaClick={() => onEmptyCtaClick?.('sessions', labels.emptyCtaHref)}
        />
      ) : (
        <div className="flex flex-col gap-2">
          {top.map(s => {
            const isLive = s.status === 'live';
            const playerLabel = labels.playerCountTemplate.replace(
              '{count}',
              String(s.playerCount)
            );
            const minutesLabel =
              s.durationMinutes != null
                ? labels.minutesTemplate.replace('{count}', String(s.durationMinutes))
                : null;
            const statusLabel = labels.statusLabels[s.status];
            const subtitle = isLive
              ? [minutesLabel, playerLabel].filter(Boolean).join(' · ')
              : [statusLabel, minutesLabel, playerLabel].filter(Boolean).join(' · ');

            return (
              <Link
                key={s.id}
                href={`/sessions/${s.id}`}
                data-slot="dashboard-session-row"
                data-status={s.status}
                className="flex items-center gap-2.5 rounded-[10px] border bg-background px-2.5 py-2 transition-colors hover:border-border-strong"
                style={{
                  borderColor: isLive ? 'hsl(var(--c-session) / 0.4)' : 'var(--border)',
                  background: isLive ? 'hsl(var(--c-session) / 0.06)' : 'var(--bg)',
                }}
              >
                <div
                  aria-hidden="true"
                  className="flex h-8 w-8 shrink-0 items-center justify-center rounded-md text-base"
                  style={{ background: sessionGradient(s.id) }}
                >
                  <span style={{ filter: 'drop-shadow(0 1px 2px rgba(0,0,0,0.3))' }}>🎯</span>
                </div>
                <div className="min-w-0 flex-1">
                  <div className="flex items-center gap-1.5 font-quicksand text-xs font-extrabold text-foreground">
                    <span className="line-clamp-1">{s.title}</span>
                    {isLive && (
                      <span
                        data-slot="dashboard-sessions-live-badge"
                        aria-live="polite"
                        className="inline-flex shrink-0 items-center gap-[3px] rounded-full px-1.5 py-px font-mono text-[8px] font-extrabold uppercase tracking-[0.06em]"
                        style={{
                          background: 'hsl(var(--c-session) / 0.15)',
                          color: 'hsl(var(--c-session))',
                        }}
                      >
                        <span
                          aria-hidden="true"
                          className="h-[5px] w-[5px] animate-pulse rounded-full"
                          style={{ background: 'hsl(var(--c-session))' }}
                        />
                        {labels.liveBadge}
                      </span>
                    )}
                  </div>
                  <div className="line-clamp-1 font-mono text-[10px] font-semibold text-muted-foreground">
                    {subtitle}
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

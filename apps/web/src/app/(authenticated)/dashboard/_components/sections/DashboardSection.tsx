/**
 * DashboardSection — generic wrapper for Stage 3 dashboard sections.
 *
 * 5 instances in DashboardClient (Games / Players / Agents / Sessions / Events).
 * Renders header (entity-tinted icon + title + count pill + view-all link) +
 * body (passed via children). Empty-state rendering is delegated to each
 * section's content component, so the wrapper stays pure.
 *
 * Pixel-faithful to admin-mockups/design_files/sp4-dashboard.jsx
 * (entity-tinted accents, count pill, view-all colored with arrow).
 */

'use client';

import type { ReactNode } from 'react';

import clsx from 'clsx';
import Link from 'next/link';

export type DashboardSectionEntity =
  | 'game'
  | 'player'
  | 'agent'
  | 'session'
  | 'event'
  | 'toolkit';

const ENTITY_TINT: Record<
  DashboardSectionEntity,
  { iconBg: string; iconFg: string; link: string }
> = {
  game: {
    iconBg: 'bg-[hsl(var(--c-game)/0.12)]',
    iconFg: 'text-[hsl(var(--c-game))]',
    link: 'text-[hsl(var(--c-game))]',
  },
  player: {
    iconBg: 'bg-[hsl(var(--c-player)/0.12)]',
    iconFg: 'text-[hsl(var(--c-player))]',
    link: 'text-[hsl(var(--c-player))]',
  },
  agent: {
    iconBg: 'bg-[hsl(var(--c-agent)/0.12)]',
    iconFg: 'text-[hsl(var(--c-agent))]',
    link: 'text-[hsl(var(--c-agent))]',
  },
  session: {
    iconBg: 'bg-[hsl(var(--c-session)/0.12)]',
    iconFg: 'text-[hsl(var(--c-session))]',
    link: 'text-[hsl(var(--c-session))]',
  },
  event: {
    iconBg: 'bg-[hsl(var(--c-event)/0.12)]',
    iconFg: 'text-[hsl(var(--c-event))]',
    link: 'text-[hsl(var(--c-event))]',
  },
  toolkit: {
    iconBg: 'bg-[hsl(var(--c-toolkit)/0.12)]',
    iconFg: 'text-[hsl(var(--c-toolkit))]',
    link: 'text-[hsl(var(--c-toolkit))]',
  },
};

export interface DashboardSectionProps {
  /** Stable identifier for telemetry (e.g. "games", "players"). */
  readonly sectionId: string;
  /** Entity color theme for icon/link tinting. */
  readonly entity: DashboardSectionEntity;
  /** Localised title. */
  readonly title: string;
  /** Entity-tinted icon (emoji). */
  readonly icon: string;
  /** Optional count chip (e.g. total games in library). */
  readonly count?: number;
  /** Localised "view all" CTA label. */
  readonly viewAllLabel?: string;
  /** "View all" href. */
  readonly viewAllHref?: string;
  /** Called when "view all" is clicked (for telemetry). */
  readonly onViewAllClick?: (sectionId: string, viewAllHref: string) => void;
  /** Section body (content component or empty-state). */
  readonly children: ReactNode;
  /** When true, section spans full width (used for Events on desktop). */
  readonly fullWidth?: boolean;
  readonly className?: string;
  /** Optional inline node rendered between title and count (e.g. live indicator). */
  readonly headerExtra?: ReactNode;
}

export function DashboardSection({
  sectionId,
  entity,
  title,
  icon,
  count,
  viewAllLabel,
  viewAllHref,
  onViewAllClick,
  children,
  fullWidth,
  className,
  headerExtra,
}: DashboardSectionProps) {
  const tint = ENTITY_TINT[entity];

  return (
    <section
      data-slot="dashboard-section"
      data-section-id={sectionId}
      className={clsx(
        'flex min-h-[180px] flex-col gap-3 rounded-[18px] border border-border bg-card p-3.5 sm:gap-3 sm:p-[18px]',
        fullWidth && 'sm:col-span-2',
        className
      )}
    >
      <header className="flex items-center gap-2 sm:gap-2.5">
        <span
          aria-hidden="true"
          className={clsx(
            'flex h-[26px] w-[26px] shrink-0 items-center justify-center rounded-md text-sm sm:h-8 sm:w-8 sm:text-[17px]',
            tint.iconBg,
            tint.iconFg
          )}
        >
          {icon}
        </span>
        <h2 className="font-quicksand text-sm font-extrabold leading-tight text-foreground sm:text-[17px]">
          {title}
        </h2>
        {count !== undefined && count > 0 && (
          <span className="inline-flex items-center rounded-full bg-muted px-2 py-0.5 font-mono text-[10px] font-extrabold text-muted-foreground tabular-nums">
            {count}
          </span>
        )}
        {headerExtra}
        <div className="flex-1" />
        {viewAllHref && viewAllLabel && (
          <Link
            href={viewAllHref}
            onClick={() => onViewAllClick?.(sectionId, viewAllHref)}
            className={clsx(
              'inline-flex items-center gap-1 rounded-md px-2 py-1 font-quicksand text-[11px] font-bold sm:text-xs',
              tint.link
            )}
          >
            {viewAllLabel}
            <span aria-hidden="true">→</span>
          </Link>
        )}
      </header>
      <div className="flex-1">{children}</div>
    </section>
  );
}

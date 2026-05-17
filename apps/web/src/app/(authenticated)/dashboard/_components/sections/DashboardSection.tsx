/**
 * DashboardSection — generic wrapper for Stage 3 dashboard sections.
 *
 * 5 instances in DashboardClient (Games / Players / Agents / Sessions / Events).
 * Renders header (icon + title + count + view-all link) + body (passed via
 * children). Empty-state rendering is delegated to each section's content
 * component, so the wrapper stays pure.
 */

'use client';

import type { ReactNode } from 'react';

import clsx from 'clsx';
import Link from 'next/link';

export interface DashboardSectionProps {
  /** Stable identifier for telemetry (e.g. "games", "players"). */
  readonly sectionId: string;
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
}

export function DashboardSection({
  sectionId,
  title,
  icon,
  count,
  viewAllLabel,
  viewAllHref,
  onViewAllClick,
  children,
  fullWidth,
  className,
}: DashboardSectionProps) {
  return (
    <section
      data-slot="dashboard-section"
      data-section-id={sectionId}
      className={clsx(
        'flex flex-col gap-3 rounded-2xl border border-border bg-card p-4 sm:p-5',
        fullWidth && 'sm:col-span-2',
        className
      )}
    >
      <header className="flex items-center gap-3">
        <span
          aria-hidden="true"
          className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-muted text-lg"
        >
          {icon}
        </span>
        <h2 className="flex-1 font-bold font-[Quicksand] text-base text-foreground">{title}</h2>
        {count !== undefined && count > 0 && (
          <span className="tabular-nums font-mono text-xs font-semibold text-muted-foreground">
            {count}
          </span>
        )}
        {viewAllHref && viewAllLabel && (
          <Link
            href={viewAllHref}
            onClick={() => onViewAllClick?.(sectionId, viewAllHref)}
            className="text-xs font-bold font-[Quicksand] text-muted-foreground hover:text-foreground"
          >
            {viewAllLabel} →
          </Link>
        )}
      </header>
      <div className="flex-1">{children}</div>
    </section>
  );
}

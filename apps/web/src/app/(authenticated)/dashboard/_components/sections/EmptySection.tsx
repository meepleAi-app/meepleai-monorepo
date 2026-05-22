/**
 * EmptySection — entity-tinted empty state shared across the 5 dashboard
 * sections. Pixel-faithful to admin-mockups/design_files/sp4-dashboard.jsx
 * `EmptySection` component.
 *
 * Dashed border entity-tinted, circular icon, centered message, primary CTA.
 */

'use client';

import type { JSX } from 'react';

import clsx from 'clsx';
import Link from 'next/link';

import type { DashboardSectionEntity } from './DashboardSection';

const EMPTY_TINT: Record<
  DashboardSectionEntity,
  { border: string; iconBg: string; iconFg: string; ctaBg: string }
> = {
  game: {
    border: 'border-[hsl(var(--c-game)/0.3)]',
    iconBg: 'bg-[hsl(var(--c-game)/0.12)]',
    iconFg: 'text-[hsl(var(--c-game))]',
    ctaBg: 'bg-[hsl(var(--c-game))]',
  },
  player: {
    border: 'border-[hsl(var(--c-player)/0.3)]',
    iconBg: 'bg-[hsl(var(--c-player)/0.12)]',
    iconFg: 'text-[hsl(var(--c-player))]',
    ctaBg: 'bg-[hsl(var(--c-player))]',
  },
  agent: {
    border: 'border-[hsl(var(--c-agent)/0.3)]',
    iconBg: 'bg-[hsl(var(--c-agent)/0.12)]',
    iconFg: 'text-[hsl(var(--c-agent))]',
    ctaBg: 'bg-[hsl(var(--c-agent))]',
  },
  session: {
    border: 'border-[hsl(var(--c-session)/0.3)]',
    iconBg: 'bg-[hsl(var(--c-session)/0.12)]',
    iconFg: 'text-[hsl(var(--c-session))]',
    ctaBg: 'bg-[hsl(var(--c-session))]',
  },
  event: {
    border: 'border-[hsl(var(--c-event)/0.3)]',
    iconBg: 'bg-[hsl(var(--c-event)/0.12)]',
    iconFg: 'text-[hsl(var(--c-event))]',
    ctaBg: 'bg-[hsl(var(--c-event))]',
  },
  toolkit: {
    border: 'border-[hsl(var(--c-toolkit)/0.3)]',
    iconBg: 'bg-[hsl(var(--c-toolkit)/0.12)]',
    iconFg: 'text-[hsl(var(--c-toolkit))]',
    ctaBg: 'bg-[hsl(var(--c-toolkit))]',
  },
};

export interface EmptySectionProps {
  readonly entity: DashboardSectionEntity;
  readonly icon: string;
  readonly message: string;
  readonly cta?: string;
  readonly ctaHref?: string;
  readonly onCtaClick?: () => void;
}

export function EmptySection({
  entity,
  icon,
  message,
  cta,
  ctaHref,
  onCtaClick,
}: EmptySectionProps): JSX.Element {
  const tint = EMPTY_TINT[entity];

  return (
    <div
      className={clsx(
        'flex flex-1 flex-col items-center rounded-[10px] border border-dashed bg-background px-3 py-5 text-center sm:px-4 sm:py-7',
        tint.border
      )}
    >
      <div
        aria-hidden="true"
        className={clsx(
          'mb-2 flex h-12 w-12 items-center justify-center rounded-full text-[22px]',
          tint.iconBg,
          tint.iconFg
        )}
      >
        {icon}
      </div>
      <p className="mx-auto mb-2.5 max-w-[240px] text-xs leading-[1.4] text-muted-foreground">
        {message}
      </p>
      {cta && ctaHref && (
        <Link
          href={ctaHref}
          onClick={onCtaClick}
          className={clsx(
            'inline-flex items-center rounded-md px-3 py-1.5 font-quicksand text-[11px] font-extrabold text-[#fff]',
            tint.ctaBg
          )}
        >
          {cta}
        </Link>
      )}
    </div>
  );
}

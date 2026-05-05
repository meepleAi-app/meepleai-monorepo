/**
 * SessionsHero — Wave D.1 v2 component (Issue #735).
 *
 * Mapped from `admin-mockups/design_files/sp4-sessions-index.jsx` (SessionsHero).
 *
 * Pure component: all i18n strings injected via `labels` — no `useTranslation`.
 *
 * Layout:
 *   - Title: large heading, session entity-colored text (hsl(240,60%,45%) l45 AA)
 *   - Subtitle: muted text with {count} template substitution
 *   - CTA: right-aligned primary button (session-colored, white text WCAG AA)
 *
 * WCAG AA: session entity at l=45% yields ~6.8:1 contrast vs white — safe for
 * button background. We use the literal HSL value to avoid Tailwind purge issues.
 */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

export interface SessionsHeroLabels {
  readonly title: string;
  /** Template e.g. "{count} sessioni totali" — {count} is replaced at render time. */
  readonly subtitleTemplate: string;
  readonly ctaNew: string;
}

export interface SessionsHeroProps {
  readonly count: number;
  readonly onNewSession: () => void;
  readonly labels: SessionsHeroLabels;
  readonly className?: string;
}

export function SessionsHero({
  count,
  onNewSession,
  labels,
  className,
}: SessionsHeroProps): ReactElement {
  const subtitle = labels.subtitleTemplate.replace('{count}', String(count));

  return (
    <section
      data-slot="sessions-hero"
      className={clsx('mx-auto max-w-[1280px] px-4 py-6 sm:px-8 sm:py-8', className)}
    >
      <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between sm:gap-6">
        {/* Text block */}
        <div className="flex flex-col gap-1">
          <h1 className="text-2xl font-bold tracking-tight text-[hsl(240,60%,45%)] sm:text-3xl">
            {labels.title}
          </h1>
          <p className="text-sm text-slate-600">{subtitle}</p>
        </div>

        {/* CTA */}
        <button
          type="button"
          onClick={onNewSession}
          aria-label={labels.ctaNew}
          className={clsx(
            'inline-flex h-10 shrink-0 items-center justify-center gap-1.5 rounded-lg px-5 text-sm font-semibold text-white shadow-sm',
            'bg-[hsl(240,60%,38%)] transition-colors',
            'hover:bg-[hsl(240,60%,32%)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'
          )}
          data-slot="sessions-hero-cta"
        >
          <span aria-hidden="true">+</span>
          {labels.ctaNew}
        </button>
      </div>
    </section>
  );
}

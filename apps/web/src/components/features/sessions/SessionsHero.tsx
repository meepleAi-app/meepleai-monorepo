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
  /** Resolved subtitle string (ICU plural already formatted by orchestrator with count). */
  readonly subtitle: string;
  readonly ctaNew: string;
}

export interface SessionsHeroProps {
  readonly onNewSession: () => void;
  readonly labels: SessionsHeroLabels;
  readonly className?: string;
}

export function SessionsHero({ onNewSession, labels, className }: SessionsHeroProps): ReactElement {
  return (
    <section
      data-slot="sessions-hero"
      className={clsx('mx-auto max-w-[1280px] px-4 py-6 sm:px-8 sm:py-8', className)}
    >
      <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between sm:gap-6">
        {/* Text block */}
        <div className="flex flex-col gap-1">
          <h1 className="text-2xl font-bold tracking-tight text-entity-session sm:text-3xl">
            {labels.title}
          </h1>
          <p className="text-sm text-muted-foreground">{labels.subtitle}</p>
        </div>

        {/* CTA */}
        <button
          type="button"
          onClick={onNewSession}
          aria-label={labels.ctaNew}
          className={clsx(
            // eslint-disable-next-line local/no-hardcoded-color-utility -- text-white on bg-entity-session (entity-colored CTA); mockup .e-bg pattern; WCAG AA ~6.8:1
            'inline-flex h-10 shrink-0 items-center justify-center gap-1.5 rounded-lg px-5 text-sm font-semibold text-white shadow-sm',
            'bg-entity-session transition-colors',
            'hover:opacity-90 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'
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

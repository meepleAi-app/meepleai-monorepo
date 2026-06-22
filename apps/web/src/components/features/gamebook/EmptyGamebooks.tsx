/* eslint-disable local/no-hardcoded-color-utility -- text-white / button color on style-prop colored bg or entity-colored CTA; mockup .e-bg pattern. DS-12 will introduce primitives encoding bg via className. */
/**
 * EmptyGamebooks — SP6 Phase B Task 2 v2 component (Issue #788).
 *
 * Mapped from `admin-mockups/design_files/sp6-libro-game-index.jsx` (EmptyState).
 *
 * Pure component: all i18n strings injected via `labels` (Wave D.3 pattern).
 *
 * Layout:
 *   - Centered illustration (📖 + 🎲 emoji) on dashed-border card
 *   - Title (display font, l-700 contrast vs white card)
 *   - Description (slate-700 body)
 *   - Primary CTA — game entity orange (l39 = ~4.6:1 vs white WCAG AA)
 *
 * a11y:
 *   - role="status" announces empty state to SR.
 *   - Illustration is `aria-hidden="true"` (decorative).
 *
 * data-slot="empty-gamebooks" for E2E selectors.
 */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

export interface EmptyGamebooksLabels {
  /** Title (e.g. "Nessun manuale ancora"). */
  readonly title: string;
  /** Description sentence. */
  readonly description: string;
  /** Primary CTA label (e.g. "📷 Scatta il primo manuale"). */
  readonly cta: string;
}

export interface EmptyGamebooksProps {
  readonly onAddManualClick: () => void;
  readonly labels: EmptyGamebooksLabels;
  readonly className?: string;
}

// game + event entity colours replaced with Tailwind entity-token classes (P2 #807 Task 6+7+8)

export function EmptyGamebooks({
  onAddManualClick,
  labels,
  className,
}: EmptyGamebooksProps): ReactElement {
  return (
    <div
      data-slot="empty-gamebooks"
      role="status"
      className={clsx(
        'mx-auto flex max-w-md flex-col items-center justify-center gap-3 rounded-2xl px-6 py-12 text-center sm:py-16',
        className
      )}
    >
      {/* Illustration */}
      <div
        aria-hidden="true"
        data-slot="empty-gamebooks-illustration"
        className="flex h-24 w-24 items-center justify-center rounded-3xl border border-dashed border-entity-game/40 sm:h-28 sm:w-28"
        style={{
          /* TODO #807-followup: two-entity gradient (game+event) — keep inline until CSS vars support alpha stops */
          // eslint-disable-next-line meepleai/no-inline-hsl-v2 -- TODO #807-followup: two-entity alpha gradient (game+event); CSS vars cannot carry alpha stops in style string
          background: 'linear-gradient(155deg, hsla(25,95%,45%,0.18), hsla(350,89%,60%,0.14))',
        }}
      >
        <span className="text-4xl sm:text-5xl">📖</span>
        <span className="-ml-3 text-2xl sm:text-3xl">🎲</span>
      </div>

      {/* Title */}
      <h2
        data-slot="empty-gamebooks-title"
        className="text-lg font-bold tracking-tight text-foreground sm:text-xl"
      >
        {labels.title}
      </h2>

      {/* Description */}
      <p className="max-w-xs text-sm leading-relaxed text-foreground">{labels.description}</p>

      {/* Primary CTA */}
      <button
        type="button"
        onClick={onAddManualClick}
        aria-label={labels.cta}
        data-slot="empty-gamebooks-cta"
        className={clsx(
          'mt-2 inline-flex h-11 items-center justify-center gap-1.5 rounded-lg px-5 text-sm font-bold text-white shadow-sm',
          'bg-entity-game transition-colors motion-reduce:transition-none',
          'hover:opacity-90 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'
        )}
      >
        {labels.cta}
      </button>
    </div>
  );
}

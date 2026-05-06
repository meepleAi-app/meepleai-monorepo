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

const GAME_HSL_SOLID = 'hsl(25, 95%, 39%)';
const GAME_HSL_HOVER = 'hsl(25, 95%, 32%)';
const GAME_HSL_BORDER = 'hsla(25, 95%, 45%, 0.40)';
const GAME_HSL_BG_TOP = 'hsla(25, 95%, 45%, 0.18)';
const EVENT_HSL_BG_BOTTOM = 'hsla(350, 89%, 60%, 0.14)';

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
        className="flex h-24 w-24 items-center justify-center rounded-3xl border border-dashed sm:h-28 sm:w-28"
        style={{
          background: `linear-gradient(155deg, ${GAME_HSL_BG_TOP}, ${EVENT_HSL_BG_BOTTOM})`,
          borderColor: GAME_HSL_BORDER,
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
      <p className="max-w-xs text-sm leading-relaxed text-slate-700">{labels.description}</p>

      {/* Primary CTA */}
      <button
        type="button"
        onClick={onAddManualClick}
        aria-label={labels.cta}
        data-slot="empty-gamebooks-cta"
        className={clsx(
          'mt-2 inline-flex h-11 items-center justify-center gap-1.5 rounded-lg px-5 text-sm font-bold text-white shadow-sm',
          'transition-colors motion-reduce:transition-none',
          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'
        )}
        style={{ backgroundColor: GAME_HSL_SOLID }}
        onMouseEnter={e => (e.currentTarget.style.backgroundColor = GAME_HSL_HOVER)}
        onMouseLeave={e => (e.currentTarget.style.backgroundColor = GAME_HSL_SOLID)}
      >
        {labels.cta}
      </button>
    </div>
  );
}

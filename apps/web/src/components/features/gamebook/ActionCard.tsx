/**
 * ActionCard — SP6 Phase C.1.B v2 component (Issue #789).
 *
 * Clickable card with leading icon + title + description. Used by
 * `NoResultsPanel` for the 3 fallback actions on Step 1 (Create new gamebook,
 * Search BGG, Add private game).
 *
 * Pure component (Wave D.3 pattern): all i18n strings injected. Receives a
 * `ReactNode` icon so the parent can pass entity-colored span / SVG without
 * coupling this primitive to a specific iconography.
 *
 * Mapped from `admin-mockups/design_files/sp6-libro-game-photo-upload.jsx`
 * (`pu-action-card` blocks under Step1_NoResults).
 *
 * a11y:
 *   - `<button type="button">` semantic (NOT `<div onClick>` — keyboard-
 *     activatable + focus-visible by default).
 *   - Icon container is `aria-hidden="true"` (decorative).
 *   - `aria-label` includes title for SR (description carries supplementary
 *     context but is rendered as plain text + scoped via `aria-describedby`-
 *     equivalent inline reading order).
 *
 * data-slot="action-card" for E2E selectors.
 */

'use client';

import type { ReactElement, ReactNode } from 'react';

import clsx from 'clsx';

export interface ActionCardProps {
  /** Leading icon node (e.g. emoji span or SVG). Decorative. */
  readonly icon: ReactNode;
  /** Action title (e.g. "Crea gioco nuovo"). */
  readonly title: string;
  /** Action description (e.g. "Manuale che non esiste in nessun database"). */
  readonly description: string;
  /** Click handler. */
  readonly onClick: () => void;
  readonly className?: string;
}

export function ActionCard({
  icon,
  title,
  description,
  onClick,
  className,
}: ActionCardProps): ReactElement {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-label={title}
      data-slot="action-card"
      className={clsx(
        'group flex w-full items-start gap-3 rounded-xl border border-border bg-card p-3 text-left',
        'transition-shadow motion-reduce:transition-none',
        'hover:shadow-md',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
        className
      )}
    >
      <span
        data-slot="action-card-icon"
        aria-hidden="true"
        className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-muted text-xl"
      >
        {icon}
      </span>
      <span className="flex flex-1 flex-col gap-0.5">
        <span
          data-slot="action-card-title"
          className="text-sm font-bold leading-tight text-foreground"
        >
          {title}
        </span>
        <span data-slot="action-card-description" className="text-xs leading-snug text-foreground">
          {description}
        </span>
      </span>
    </button>
  );
}

/**
 * NotFoundState — dedicated 404 surface for /shared-games/[id] V2.
 *
 * Issue #615 (Wave A.4 follow-up · P1). Rendered when the FSM hook
 * (`useSharedGameDetail`) reports `status === 'not-found'`, which maps
 * 1:1 to a backend HTTP 404. Distinct from the generic `not-found.tsx`
 * route file: that one fires when SSR detects 404 before the client
 * mounts; this component covers the post-mount case (e.g. user navigated
 * to a stale id after the game was unpublished).
 *
 * Mirrors the visual language of `EmptyState` (dashed-border card, 64px
 * icon circle, centered text) so the whole detail page family has one
 * coherent placeholder vocabulary.
 */

import type { JSX } from 'react';

import clsx from 'clsx';

export interface NotFoundStateLabels {
  /** Heading; e.g. "Gioco non trovato". */
  readonly title: string;
  /** Body copy explaining the cause and any next step. */
  readonly description: string;
  /** CTA label; e.g. "Torna al catalogo". */
  readonly backLabel: string;
}

export interface NotFoundStateProps {
  readonly labels: NotFoundStateLabels;
  /**
   * Where the back-CTA links to. Defaults to the public catalog index.
   */
  readonly backHref?: string;
  readonly className?: string;
}

export function NotFoundState({
  labels,
  backHref = '/shared-games',
  className,
}: NotFoundStateProps): JSX.Element {
  return (
    <div
      data-slot="shared-game-detail-not-found-state"
      role="status"
      className={clsx(
        'flex flex-col items-center justify-center gap-3 rounded-lg border border-dashed border-border bg-muted/30 px-6 py-10 text-center',
        className
      )}
    >
      <span
        aria-hidden="true"
        className="flex h-16 w-16 items-center justify-center rounded-full bg-muted text-[28px]"
      >
        🔍
      </span>
      <h2 className="m-0 font-display text-base font-semibold text-foreground">{labels.title}</h2>
      <p className="m-0 max-w-md text-sm text-[hsl(var(--text-muted))]">{labels.description}</p>
      <a
        href={backHref}
        className="mt-2 inline-flex items-center justify-center rounded-md border border-border bg-background px-4 py-2 text-sm font-medium text-foreground transition-colors hover:bg-muted/40 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
      >
        {labels.backLabel}
      </a>
    </div>
  );
}

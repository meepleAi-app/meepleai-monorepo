/**
 * ErrorState — dedicated error surface for /shared-games/[id] V2.
 *
 * Issue #615 (Wave A.4 follow-up · P1). Rendered when the FSM hook
 * (`useSharedGameDetail`) reports `status === 'error'`, which maps to
 * any non-404 failure: 5xx, network, timeout, or schema mismatch.
 * Intentionally generic — we do NOT leak the underlying `error.message`
 * (it can contain the raw URL or backend stack frames). Diagnostic
 * digest still flows to the route-level `error.tsx`, which the user
 * can check there.
 *
 * Visual language matches `EmptyState` and `NotFoundState` so the whole
 * detail page family shares one placeholder vocabulary. Renders a
 * `Riprova` button instead of a static link — error state is most
 * commonly transient, so retry is the primary action.
 */

import type { JSX } from 'react';

import clsx from 'clsx';

export interface ErrorStateLabels {
  /** Heading; e.g. "Errore di caricamento". */
  readonly title: string;
  /** Body copy with general guidance ("Riprova fra un momento"). */
  readonly description: string;
  /** Retry CTA label; e.g. "Riprova". */
  readonly retryLabel: string;
}

export interface ErrorStateProps {
  readonly labels: ErrorStateLabels;
  /**
   * Invoked when the user clicks the retry button. Typically wired to
   * `useSharedGameDetail().refetch` (post-mount errors) or to Next.js's
   * `reset()` callback (route-level `error.tsx`).
   */
  readonly onRetry: () => void;
  /**
   * Disables the retry button — useful while a refetch is already in
   * flight to prevent duplicate requests.
   */
  readonly isRetrying?: boolean;
  readonly className?: string;
}

export function ErrorState({
  labels,
  onRetry,
  isRetrying = false,
  className,
}: ErrorStateProps): JSX.Element {
  return (
    <div
      data-slot="shared-game-detail-error-state"
      role="alert"
      aria-live="assertive"
      className={clsx(
        'flex flex-col items-center justify-center gap-3 rounded-lg border border-destructive/40 bg-destructive/5 px-6 py-10 text-center',
        className
      )}
    >
      <span
        aria-hidden="true"
        className="flex h-16 w-16 items-center justify-center rounded-full bg-destructive/10 text-[28px]"
      >
        ⚠️
      </span>
      <h2 className="m-0 font-display text-base font-semibold text-foreground">{labels.title}</h2>
      <p className="m-0 max-w-md text-sm text-[hsl(var(--text-muted))]">{labels.description}</p>
      <button
        type="button"
        onClick={onRetry}
        disabled={isRetrying}
        aria-busy={isRetrying || undefined}
        className="mt-2 inline-flex items-center justify-center rounded-md border border-border bg-background px-4 py-2 text-sm font-medium text-foreground transition-colors hover:bg-muted/40 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-60"
      >
        {labels.retryLabel}
      </button>
    </div>
  );
}

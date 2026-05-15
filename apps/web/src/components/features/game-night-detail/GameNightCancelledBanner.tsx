/**
 * GameNightCancelledBanner - v2 SP7 #951 commit 2b
 *
 * Danger-toned banner rendered when status=Cancelled. Surfaces cancellation
 * meta (who, when) plus an optional reason and a CTA to schedule a new event.
 *
 * Mapped from `admin-mockups/design_files/sp7-game-night-detail-rsvp.jsx`
 * (CancelledBanner).
 *
 * Pure presentational — visibility is controlled by the caller (page-client
 * branches on `event.status === 'Cancelled'`). Labels resolved by caller.
 *
 * AC: T A V
 */

'use client';

import clsx from 'clsx';

import { Button } from '@/components/ui/primitives/button';

export interface GameNightCancelledBannerLabels {
  /** "Serata cancellata" / "Game night cancelled" */
  readonly title: string;
  /** Meta line, e.g. "Cancellata da Marco · 2 giorni fa". Caller resolves. */
  readonly meta: string;
  /** Label for reason heading, e.g. "Motivo:". */
  readonly reasonLabel: string;
  /** CTA label, e.g. "Crea nuova serata". */
  readonly ctaLabel: string;
}

export interface GameNightCancelledBannerProps {
  readonly labels: GameNightCancelledBannerLabels;
  /** Optional cancellation reason verbatim from the host. */
  readonly reason?: string | null;
  /** Optional CTA click handler. When undefined, the CTA button is omitted. */
  readonly onCreateNew?: () => void;
  readonly className?: string;
}

export function GameNightCancelledBanner({
  labels,
  reason,
  onCreateNew,
  className,
}: GameNightCancelledBannerProps): React.JSX.Element {
  const hasReason = reason !== null && reason !== undefined && reason.trim().length > 0;

  return (
    <div
      role="alert"
      data-testid="game-night-cancelled-banner"
      className={clsx(
        'flex flex-col gap-2.5 rounded-lg border border-destructive/30 bg-destructive/10 p-4',
        className
      )}
    >
      <div className="flex items-center gap-2.5">
        <span
          aria-hidden="true"
          className={clsx(
            'flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-destructive font-display text-lg font-extrabold',
            // eslint-disable-next-line local/no-hardcoded-color-utility -- white icon on destructive bg circle; mockup .e-bg pattern.
            'text-white'
          )}
        >
          ✕
        </span>
        <div className="min-w-0 flex-1">
          <div className="font-display text-sm font-extrabold text-foreground">{labels.title}</div>
          <div className="mt-0.5 font-mono text-[10px] font-bold text-muted-foreground">
            {labels.meta}
          </div>
        </div>
      </div>

      {hasReason && (
        <div className="rounded-md border border-border bg-card px-3 py-2.5 text-[13px] leading-relaxed text-muted-foreground">
          <strong className="text-foreground">{labels.reasonLabel}</strong> {reason}
        </div>
      )}

      {onCreateNew !== undefined && (
        <Button
          type="button"
          variant="outline"
          onClick={onCreateNew}
          className="w-full justify-center border-entity-event/40 text-entity-event hover:bg-entity-event/10 hover:text-entity-event"
        >
          <span aria-hidden="true" className="mr-1.5">
            📅
          </span>
          {labels.ctaLabel}
        </Button>
      )}
    </div>
  );
}

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

import { Dialog, DialogContent } from '@/components/ui/overlays/dialog';

export interface SoftWarningCreditsLabels {
  readonly title: string;
  /** Receives (used, total, remaining) and returns the interpolated subtitle. */
  readonly subtitle: (used: number, total: number, remaining: number) => string;
  readonly upgradeCta: string;
  readonly dismissCta: string;
  /** aria-label for the close X button (desktop modal only). */
  readonly close: string;
}

export interface SoftWarningCreditsProps {
  readonly used: number;
  readonly total: number;
  readonly variant: 'toast-mobile' | 'modal-desktop';
  readonly labels: SoftWarningCreditsLabels;
  readonly onUpgrade: () => void;
  readonly onDismiss: () => void;
}

export function SoftWarningCredits({
  used,
  total,
  variant,
  labels,
  onUpgrade,
  onDismiss,
}: SoftWarningCreditsProps): ReactElement {
  const remaining = Math.max(0, total - used);
  if (variant === 'modal-desktop') {
    // shadcn Dialog provides focus trap + ESC + aria-modal + role=dialog.
    // We hide its default close X and keep our own custom X (consistent
    // with the mockup styling).
    return (
      <Dialog open onOpenChange={(open) => !open && onDismiss()}>
        <DialogContent
          data-slot="soft-warning-modal"
          aria-labelledby="soft-warning-title"
          hideCloseButton
          className="max-w-md gap-0 border-entity-agent/30 p-0"
        >
          <div className="relative flex w-full gap-3.5 p-5 pb-4">
            <div
              aria-hidden="true"
              className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-entity-agent/15 text-xl text-entity-agent"
            >
              🟡
            </div>
            <div className="flex flex-1 flex-col gap-1">
              <h3 id="soft-warning-title" className="text-base font-bold text-foreground">
                {labels.title}
              </h3>
              <p className="text-xs leading-relaxed text-muted-foreground">
                {labels.subtitle(used, total, remaining)}
              </p>
              <div className="mt-2 flex gap-2">
                <button
                  type="button"
                  onClick={onUpgrade}
                  className={clsx(
                    'flex-1 rounded-md bg-entity-toolkit px-3.5 py-2.5 text-xs font-bold text-white',
                    'transition-[filter] hover:brightness-110 active:scale-[0.98]',
                    'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2'
                  )}
                >
                  {labels.upgradeCta}
                </button>
                <button
                  type="button"
                  onClick={onDismiss}
                  className={clsx(
                    'flex-1 rounded-md border border-border-strong bg-card px-3.5 py-2.5 text-xs font-semibold text-foreground',
                    'transition-colors hover:bg-muted',
                    'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2'
                  )}
                >
                  {labels.dismissCta}
                </button>
              </div>
            </div>
            <button
              type="button"
              aria-label={labels.close}
              onClick={onDismiss}
              className="absolute right-2.5 top-2.5 flex h-8 w-8 items-center justify-center rounded-full border border-border bg-muted text-foreground hover:bg-card focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
            >
              ×
            </button>
          </div>
        </DialogContent>
      </Dialog>
    );
  }
  return (
    <div
      role="status"
      aria-live="polite"
      data-slot="soft-warning-toast"
      className="fixed bottom-4 left-3 right-3 z-40 flex flex-col gap-2.5 rounded-lg border border-entity-agent/30 bg-card px-3.5 py-3 shadow-md"
    >
      <div className="flex items-start gap-2.5">
        <span
          aria-hidden="true"
          className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-entity-agent/15 text-entity-agent"
        >
          🟡
        </span>
        <div className="flex-1">
          <p className="text-xs font-bold text-foreground">{labels.title}</p>
          <p className="text-[11px] leading-relaxed text-muted-foreground">
            {labels.subtitle(used, total, remaining)}
          </p>
        </div>
      </div>
      <div className="flex gap-2">
        <button
          type="button"
          onClick={onUpgrade}
          className={clsx(
            'flex-1 rounded-md bg-entity-toolkit px-3 py-2 text-xs font-bold text-white',
            'transition-[filter] hover:brightness-110 active:scale-[0.98]',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2'
          )}
        >
          {labels.upgradeCta}
        </button>
        <button
          type="button"
          onClick={onDismiss}
          className={clsx(
            'flex-1 rounded-md border border-border-strong bg-card px-3 py-2 text-xs font-semibold text-foreground',
            'transition-colors hover:bg-muted',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2'
          )}
        >
          {labels.dismissCta}
        </button>
      </div>
    </div>
  );
}

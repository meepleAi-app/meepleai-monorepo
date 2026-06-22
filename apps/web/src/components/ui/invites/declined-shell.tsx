/**
 * DeclinedShell — confirmation state shown after the user declines.
 *
 * Wave A.5b (Issue #611). Mirrors mockup `sp3-accept-invite.jsx` lines 511-550
 * (DeclinedShell). Composition:
 *  - 60×60 🌙 icon (bg-muted + 1px border)
 *  - h2 "Ok, sarà per la prossima!" (display 18px, weight 700)
 *  - paragraph subtext (12px text-muted, line-height 1.6)
 *  - return-home link (display 13px, bg-muted, border)
 *  - undo link ("Ho cambiato idea, accetta l'invito") — onClick triggers
 *    re-render to default state, NOT a backend mutation. Switching from
 *    Declined → Accepted requires a fresh respond() POST and the FSM
 *    `state-switch-conflict` arm (409) handles the surface.
 *
 * Note: undo button does NOT call backend on its own. Caller wires it to
 * either close the shell (re-show pending state) or directly trigger
 * `mutate({ rsvp: 'Accepted' })`.
 */

import type { JSX, ReactNode } from 'react';

import clsx from 'clsx';

export interface DeclinedShellProps {
  /** Localized headline (e.g. "Ok, sarà per la prossima!"). */
  readonly headline: string;
  /** Subtext content. */
  readonly subText: ReactNode;
  /** Localized "Torna alla home →" copy. */
  readonly returnHomeLabel: string;
  /** Where return-home navigates to (default `/`). */
  readonly returnHomeHref?: string;
  /** Localized "Ho cambiato idea, accetta l'invito" copy. */
  readonly undoLabel: string;
  /**
   * Triggered when the undo link is clicked. Caller decides whether to
   * re-show pending shell or kick off an Accept mutation.
   */
  readonly onUndo?: () => void;
  readonly className?: string;
}

export function DeclinedShell({
  headline,
  subText,
  returnHomeLabel,
  returnHomeHref = '/',
  undoLabel,
  onUndo,
  className,
}: DeclinedShellProps): JSX.Element {
  return (
    <div
      data-slot="invite-declined-shell"
      className={clsx('flex flex-col items-center px-1 py-1 text-center', className)}
    >
      <div
        aria-hidden="true"
        className="mb-3.5 flex h-[60px] w-[60px] items-center justify-center rounded-full border bg-[hsl(var(--bg-muted))] text-[26px]"
      >
        🌙
      </div>
      <h2 className="m-0 mb-1.5 font-display text-[18px] font-bold text-foreground">{headline}</h2>
      <p className="m-0 mb-4 text-[12px] leading-relaxed text-[hsl(var(--text-muted))]">
        {subText}
      </p>
      <div className="flex w-full flex-col gap-2">
        <a
          href={returnHomeHref}
          className="inline-flex items-center justify-center rounded-md border bg-[hsl(var(--bg-muted))] px-3.5 py-2.5 font-display text-[13px] font-bold text-foreground no-underline"
        >
          {returnHomeLabel}
        </a>
        {onUndo ? (
          <button
            type="button"
            onClick={onUndo}
            className="cursor-pointer border-0 bg-transparent p-2 text-[11px] text-[hsl(var(--text-muted))] underline"
          >
            {undoLabel}
          </button>
        ) : null}
      </div>
    </div>
  );
}

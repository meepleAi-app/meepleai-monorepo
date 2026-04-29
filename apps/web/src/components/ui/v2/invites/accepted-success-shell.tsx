/**
 * AcceptedSuccessShell — celebratory state shown after the user accepts.
 *
 * Wave A.5b (Issue #611). Mirrors mockup `sp3-accept-invite.jsx` lines 451-506
 * (AcceptedSuccessShell). Composition:
 *  - 64×64 🎲 icon (c-toolkit/14% bg + inset 3px ring c-toolkit/25%)
 *  - h2 "Ci sei!" (display 19px, weight 800, tracking -0.01em)
 *  - paragraph subtext (12px text-muted, line-height 1.6)
 *  - summary card (bg-muted, border-light, "Riepilogo" eyebrow)
 *  - .ics calendar link (data-testid="ics-link") — stub anchor; real wiring
 *    happens later when backend exposes ICS endpoint
 *  - Confetti overlay (motion-reduce:hidden via Confetti component)
 *
 * Note: `position: relative` + `overflow: hidden` ensures Confetti particles
 * drift only within the shell bounds, never escaping into surrounding layout.
 */

import type { JSX, ReactNode } from 'react';

import clsx from 'clsx';

import { Confetti } from './confetti';

export interface AcceptedSuccessShellProps {
  /** Localized headline (e.g. "Ci sei!"). */
  readonly headline: string;
  /** Subtext content (may include line breaks via ReactNode). */
  readonly subText: ReactNode;
  /** Localized "Riepilogo" eyebrow label. */
  readonly summaryLabel: string;
  /** Game name shown in summary (e.g. "Gloomhaven"). */
  readonly gameName: string;
  /** Mono subtitle line (e.g. "ven 15 giu · 19:30 · Via Roma 42"). */
  readonly summaryMeta: string;
  /** Localized "Add to calendar (.ics)" CTA copy. */
  readonly icsLabel: string;
  /**
   * Optional href for the .ics file. When omitted, renders a button-like
   * disabled link (decorative). Real wiring lands in a follow-up backend PR.
   */
  readonly icsHref?: string;
  /** Optional ICS click handler (analytics, etc). */
  readonly onIcsClick?: () => void;
  /** Optional return-to-session CTA (rendered above the .ics link). */
  readonly returnCta?: ReactNode;
  readonly className?: string;
}

export function AcceptedSuccessShell({
  headline,
  subText,
  summaryLabel,
  gameName,
  summaryMeta,
  icsLabel,
  icsHref,
  onIcsClick,
  returnCta,
  className,
}: AcceptedSuccessShellProps): JSX.Element {
  return (
    <div
      data-slot="invite-accepted-success-shell"
      className={clsx('relative overflow-hidden', className)}
    >
      <Confetti />
      <div className="flex flex-col items-center px-1 py-1 text-center">
        <div
          aria-hidden="true"
          className="mb-3.5 flex h-16 w-16 items-center justify-center rounded-full bg-[hsl(var(--c-toolkit)/0.14)] text-[30px]"
          style={{ boxShadow: 'inset 0 0 0 3px hsl(var(--c-toolkit) / 0.25)' }}
        >
          🎲
        </div>
        <h2 className="m-0 mb-1.5 font-display text-[19px] font-extrabold leading-tight tracking-[-0.01em] text-foreground">
          {headline}
        </h2>
        <p className="m-0 mb-4 text-[12px] leading-relaxed text-[hsl(var(--text-muted))]">
          {subText}
        </p>
        <div
          className="mb-3.5 w-full rounded-md border border-[hsl(var(--border-light))] bg-[hsl(var(--bg-muted))] px-3.5 py-3 text-left"
          data-slot="invite-accepted-success-summary"
        >
          <div className="mb-1.5 font-mono text-[9px] font-bold uppercase tracking-[0.08em] text-[hsl(var(--text-muted))]">
            {summaryLabel}
          </div>
          <div className="mb-1 flex items-center gap-2">
            <span aria-hidden="true">🎲</span>
            <strong className="font-display text-[14px] font-bold text-foreground">
              {gameName}
            </strong>
          </div>
          <div className="font-mono text-[11px] tracking-[0.04em] text-[hsl(var(--text-sec))]">
            {summaryMeta}
          </div>
        </div>
        {returnCta ? <div className="w-full">{returnCta}</div> : null}
        <a
          data-testid="ics-link"
          href={icsHref ?? '#'}
          onClick={event => {
            if (!icsHref) event.preventDefault();
            onIcsClick?.();
          }}
          className="mt-2.5 cursor-pointer border-0 bg-transparent p-2 text-[11px] text-[hsl(var(--text-muted))] underline"
        >
          {icsLabel}
        </a>
      </div>
    </div>
  );
}

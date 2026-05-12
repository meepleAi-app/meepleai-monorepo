/* eslint-disable local/no-hardcoded-color-utility -- text-white / button color on style-prop colored bg or entity-colored CTA; mockup .e-bg pattern. DS-12 will introduce primitives encoding bg via className. */
/**
 * PlayAgainCta — Wave D.3 v2 component (Issue #756).
 *
 * Prominent CTA banner inviting the host to start a rematch with the same
 * players + game. The button click invokes the `onPlayAgain` callback the
 * orchestrator wires to the actual mutation (e.g., `useCompleteGameNight`).
 * If `onPlayAgain` is not provided, the button is rendered DISABLED.
 *
 * Mockup mapping:
 *   - admin-mockups/design_files/sp4-session-summary.jsx (PlayAgain)
 *
 * Contract reference: docs/frontend/contracts/sessions-id-summary-hooks.md §5.11.
 *
 * MeepleCard divergence (Gate C): action-banner with gradient background
 * and primary CTA. Not a card surface — a horizontal banner with embedded
 * CTA button. DIVERGE.
 *
 * A11y:
 *   - `<section>` with `<h3>` heading.
 *   - Primary CTA `<button>` with `aria-busy={isPending}` so screen readers
 *     announce the loading state.
 *   - Disabled state: `disabled` + `aria-disabled` (no callback wired).
 *
 * Pure component: orchestrator owns mutation invocation.
 */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

export interface PlayAgainCtaLabels {
  readonly title: string;
  readonly description: string;
  readonly cta: string;
}

export interface PlayAgainCtaProps {
  readonly sessionId: string;
  /** Optional — orchestrator wires the mutation. If absent, button disabled. */
  readonly onPlayAgain?: () => void;
  /** Loading flag for `aria-busy` announcement during mutation. */
  readonly isPending?: boolean;
  readonly labels: PlayAgainCtaLabels;
  readonly className?: string;
}

export function PlayAgainCta({
  sessionId,
  onPlayAgain,
  isPending = false,
  labels,
  className,
}: PlayAgainCtaProps): ReactElement {
  const disabled = !onPlayAgain || isPending;

  return (
    <section
      data-slot="play-again-cta"
      data-session-id={sessionId}
      className={clsx(
        'flex flex-wrap items-center justify-between gap-3 rounded-xl border px-5 py-4',
        'border-entity-game/30',
        className
      )}
      style={{
        /* TODO #807-followup: two-entity gradient (game+session) — keep inline until CSS vars support alpha stops */
        // eslint-disable-next-line meepleai/no-inline-hsl-v2 -- TODO #807-followup: two-entity alpha gradient (game+session); CSS vars cannot carry alpha stops in style string
        background: 'linear-gradient(135deg, hsla(25,95%,45%,0.1) 0%, hsla(240,60%,55%,0.1) 100%)',
      }}
    >
      <div className="flex min-w-0 flex-1 flex-col gap-0.5">
        <h3 className="font-display text-base font-extrabold tracking-tight text-foreground sm:text-lg">
          <span aria-hidden="true" className="mr-1.5">
            🎲
          </span>
          {labels.title}
        </h3>
        <p className="text-xs text-muted-foreground sm:text-sm">{labels.description}</p>
      </div>
      <button
        type="button"
        onClick={() => onPlayAgain?.()}
        disabled={disabled}
        aria-disabled={disabled || undefined}
        aria-busy={isPending || undefined}
        data-slot="play-again-cta-button"
        data-disabled={disabled || undefined}
        className={clsx(
          'inline-flex h-10 shrink-0 items-center justify-center gap-1.5 rounded-lg px-5 text-sm font-semibold text-white shadow-sm transition-colors',
          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
          disabled
            ? 'cursor-not-allowed bg-muted-foreground/40'
            : 'bg-entity-session hover:opacity-90'
        )}
      >
        <span aria-hidden="true">▶</span>
        {labels.cta}
      </button>
    </section>
  );
}

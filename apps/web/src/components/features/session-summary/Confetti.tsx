/**
 * Confetti — Wave D.3 v2 component (Issue #756).
 *
 * CSS-only celebratory animation rendered above the SummaryHeroPodium for the
 * happy-path winner cell. WCAG 2.3.3-compliant via `prefers-reduced-motion`
 * media query that disables the animation entirely (handled by global
 * `apps/web/src/styles/globals.css` `.mai-confetti-piece` rules).
 *
 * Mockup mapping:
 *   - admin-mockups/design_files/sp4-session-summary-parts.jsx (Confetti)
 *
 * Contract reference: docs/frontend/contracts/sessions-id-summary-hooks.md §9.
 *
 * MeepleCard divergence (Gate C): not a card surface — purely decorative
 * absolute-positioned overlay. Cannot fit MeepleCard API.
 *
 * A11y:
 *   - `aria-hidden="true"` (decorative — parent SessionSummaryHero owns the
 *     accessible name via confetti skipped/celebration aria-label).
 *   - Animation honors `@media (prefers-reduced-motion: reduce)` → opacity 0,
 *     animation: none.
 *
 * Stylesheet contract: relies on a global stylesheet selector
 * `.mai-confetti-piece` defined in `apps/web/src/styles/globals.css` that
 * provides the `mai-confetti-fall` keyframes + reduced-motion guard. This
 * component injects per-piece inline styles for randomized position/delay/
 * color but never inline-injects keyframes (avoids CSP unsafe-inline).
 */

'use client';

import type { CSSProperties, ReactElement } from 'react';

import clsx from 'clsx';

export interface ConfettiProps {
  /**
   * Whether to render the confetti tree at all. When `false` returns null —
   * useful when the parent has already determined no animation should fire
   * (e.g. abandoned status, sessionStorage flag).
   */
  readonly active?: boolean;
  readonly className?: string;
}

/**
 * Number of falling pieces. Tuned (24) to mockup parts.jsx — not too dense
 * (browser paint cost on low-end devices) but enough for celebratory feel.
 */
const PIECE_COUNT = 24;

/**
 * Entity-color HSL strings reused for confetti pieces (cycle through). Keep
 * in sync with `tokens.ts` entityColors but inlined here so this component
 * stays self-contained.
 */
// PIECE_COLORS: CSS string array used as canvas animation fill colors.
// Tailwind entity tokens not applicable — no DOM element to apply classes to.
// TODO #807-followup: replace with CSS vars when entity color vars gain alpha-stop support
const PIECE_COLORS = [
  // eslint-disable-next-line meepleai/no-inline-hsl-v2 -- TODO #807-followup: canvas fill color, Tailwind not applicable
  'hsla(240, 60%, 55%, 0.85)', // session
  // eslint-disable-next-line meepleai/no-inline-hsl-v2 -- TODO #807-followup: canvas fill color, Tailwind not applicable
  'hsla(142, 70%, 31%, 0.85)', // toolkit
  // eslint-disable-next-line meepleai/no-inline-hsl-v2 -- TODO #807-followup: canvas fill color, Tailwind not applicable
  'hsla(25, 95%, 39%, 0.85)', // game
  // eslint-disable-next-line meepleai/no-inline-hsl-v2 -- TODO #807-followup: canvas fill color, Tailwind not applicable
  'hsla(38, 92%, 33%, 0.85)', // agent
  // eslint-disable-next-line meepleai/no-inline-hsl-v2 -- TODO #807-followup: canvas fill color, Tailwind not applicable
  'hsla(220, 80%, 55%, 0.85)', // chat
  // eslint-disable-next-line meepleai/no-inline-hsl-v2 -- TODO #807-followup: canvas fill color, Tailwind not applicable
  'hsla(350, 89%, 48%, 0.85)', // event
] as const;

export function Confetti({ active = true, className }: ConfettiProps): ReactElement | null {
  if (!active) {
    return null;
  }

  return (
    <div
      data-slot="confetti"
      aria-hidden="true"
      className={clsx(
        'mai-confetti pointer-events-none absolute inset-0 overflow-hidden',
        className
      )}
      style={{ zIndex: 1 }}
    >
      {Array.from({ length: PIECE_COUNT }).map((_, i) => {
        const left = (i * 4.17) % 100;
        const delay = ((i * 0.13) % 2.4).toFixed(2);
        const top = (i * 7.3) % 50;
        const rot = (i * 31) % 360;
        const color = PIECE_COLORS[i % PIECE_COLORS.length];
        const style: CSSProperties = {
          left: `${left}%`,
          top: `${top}%`,
          background: color,
          transform: `rotate(${rot}deg)`,
          animationDelay: `${delay}s`,
        };
        return (
          <span key={i} className="mai-confetti-piece" data-slot="confetti-piece" style={style} />
        );
      })}
    </div>
  );
}

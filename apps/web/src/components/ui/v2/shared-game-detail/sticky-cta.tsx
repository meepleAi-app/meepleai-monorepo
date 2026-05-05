/**
 * StickyCta — guest gating CTA for /shared-games/[id] V2.
 *
 * Wave A.4 (Issue #603). Mirrors mockup `sp3-shared-game-detail.jsx`
 * StickyCTAMobile + FloatingCTADesktop (lines 832-875).
 *
 * Two responsive variants from a single primitive:
 *  - Mobile (< md): full-width sticky bar at the bottom edge with backdrop blur
 *    glass background. Renders only on small viewports.
 *  - Desktop (>= md): floating pill anchored bottom-right, with lock icon +
 *    label + filled CTA. Renders only at md+.
 *
 * Both share the same href + label (`signInHref` + `installLabel`). Component is
 * stateless / link-only — actual auth flow happens on `signInHref` route.
 */

import type { JSX } from 'react';

import clsx from 'clsx';

import { entityHsl } from '@/components/ui/data-display/meeple-card/tokens';

export interface StickyCtaLabels {
  /** Mobile full-width button label, e.g. "🔒 Accedi per installare". */
  readonly mobileLabel: string;
  /** Desktop pill body text, e.g. "Per installare questi contenuti...". */
  readonly desktopHint: string;
  /** Desktop pill CTA label, e.g. "Accedi". */
  readonly desktopCtaLabel: string;
  /** Aria label for the entire sticky region. */
  readonly regionAriaLabel: string;
}

export interface StickyCtaProps {
  readonly signInHref: string;
  readonly labels: StickyCtaLabels;
  readonly className?: string;
}

export function StickyCta({ signInHref, labels, className }: StickyCtaProps): JSX.Element {
  return (
    <>
      {/* Mobile: full-width sticky bar */}
      <div
        role="region"
        aria-label={labels.regionAriaLabel}
        data-slot="shared-game-detail-sticky-cta-mobile"
        className={clsx(
          'sticky bottom-0 left-0 right-0 z-30 border-t border-border p-3 backdrop-blur md:hidden',
          'supports-[backdrop-filter]:bg-card/70',
          className
        )}
        style={{
          backgroundColor: 'hsl(var(--card) / 0.85)',
        }}
      >
        <a
          href={signInHref}
          className={clsx(
            'flex w-full items-center justify-center gap-2 rounded-md px-4 py-3 font-display text-[14px] font-semibold text-white no-underline',
            'transition-[transform,box-shadow] duration-150',
            'hover:scale-[1.01]',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-offset-2'
          )}
          style={{
            backgroundColor: entityHsl('game'),
            boxShadow: `0 6px 20px ${entityHsl('game', 0.4)}`,
          }}
        >
          <span aria-hidden="true">🔒</span>
          <span>{labels.mobileLabel}</span>
        </a>
      </div>

      {/* Desktop: floating pill bottom-right */}
      <div
        role="region"
        aria-label={labels.regionAriaLabel}
        data-slot="shared-game-detail-sticky-cta-desktop"
        className={clsx(
          'fixed bottom-6 right-6 z-30 hidden items-center gap-3 rounded-full border border-border px-4 py-2 shadow-lg backdrop-blur md:inline-flex',
          'supports-[backdrop-filter]:bg-card/70'
        )}
        style={{ backgroundColor: 'hsl(var(--card) / 0.85)' }}
      >
        <span aria-hidden="true" className="text-base">
          🔒
        </span>
        <span className="font-mono text-[11px] uppercase tracking-[0.06em] text-[hsl(var(--text-muted))]">
          {labels.desktopHint}
        </span>
        <a
          href={signInHref}
          className={clsx(
            'inline-flex items-center rounded-full px-3 py-1.5 font-mono text-[11px] font-semibold uppercase tracking-[0.06em] text-white no-underline shadow-md',
            'transition-transform duration-150 hover:scale-[1.03]',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-offset-2'
          )}
          style={{ backgroundColor: entityHsl('game') }}
        >
          {labels.desktopCtaLabel}
        </a>
      </div>
    </>
  );
}

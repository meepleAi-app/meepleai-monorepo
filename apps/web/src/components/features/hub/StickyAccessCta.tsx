/**
 * StickyAccessCta — bottom-fixed CTA for public hub routes (#1166).
 *
 * Used only by `/hub/games` (D1 auth model: public route, visitor non-loggato).
 * Floats at bottom-right on desktop, sticky-full on mobile. Clicking triggers
 * `hub_signin_clicked` telemetry + navigation to `/login?redirect=<from>`.
 */

'use client';

import { useCallback } from 'react';

import Link from 'next/link';

import { trackEvent } from '@/lib/analytics/track-event';

export interface StickyAccessCtaLabels {
  readonly title: string;
  readonly description: string;
  readonly buttonLabel: string;
}

export interface StickyAccessCtaProps {
  /** Path the user came from (used for redirect param + telemetry). */
  readonly redirectFrom: string;
  readonly labels: StickyAccessCtaLabels;
}

export function StickyAccessCta({ redirectFrom, labels }: StickyAccessCtaProps) {
  const handleClick = useCallback(() => {
    trackEvent('hub_signin_clicked', { from: redirectFrom });
  }, [redirectFrom]);

  const loginHref = `/login?redirect=${encodeURIComponent(redirectFrom)}`;

  return (
    <aside
      data-slot="hub-sticky-access-cta"
      aria-label={labels.title}
      className="fixed bottom-4 right-4 z-30 hidden max-w-sm rounded-2xl border border-border bg-card p-4 shadow-lg sm:block"
      style={{
        backgroundImage:
          'linear-gradient(135deg, hsl(var(--c-game) / 0.10) 0%, hsl(var(--c-game) / 0.04) 100%)',
      }}
    >
      <div className="flex items-start gap-3">
        <div
          aria-hidden="true"
          className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-[hsl(var(--c-game)/0.15)] text-xl text-[hsl(var(--c-game))]"
        >
          🔐
        </div>
        <div className="min-w-0 flex-1">
          <h2 className="font-bold font-[Quicksand] text-sm text-foreground">{labels.title}</h2>
          <p className="mt-1 text-xs text-muted-foreground" style={{ lineHeight: 1.45 }}>
            {labels.description}
          </p>
          <Link
            href={loginHref}
            onClick={handleClick}
            className="mt-3 inline-flex items-center rounded-lg bg-foreground px-3 py-1.5 font-bold font-[Quicksand] text-xs text-background hover:opacity-90"
          >
            {labels.buttonLabel}
          </Link>
        </div>
      </div>

      {/* Mobile variant: full-width sticky bottom */}
      <style>{`
        @media (max-width: 639px) {
          [data-slot="hub-sticky-access-cta"] {
            position: sticky;
            display: block !important;
            inset: auto 0 0 0;
            margin: 0 -16px -16px;
            border-radius: 16px 16px 0 0;
            max-width: none;
          }
        }
      `}</style>
    </aside>
  );
}

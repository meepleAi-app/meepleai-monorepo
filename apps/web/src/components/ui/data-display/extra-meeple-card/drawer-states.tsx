/* eslint-disable local/no-hardcoded-color-utility -- text-white / button color on style-prop colored bg or entity-colored CTA; mockup .e-bg pattern. DS-12 primitive — see token-bridge-map.md for migration plan. */
'use client';

import { AlertCircle, RefreshCw, Wrench } from 'lucide-react';

import { cn } from '@/lib/utils';

import { DRAWER_TEST_IDS } from './drawer-test-ids';

// ============================================================================
// Shared UI States
// ============================================================================

/** Animated skeleton shown while entity content loads */
export function DrawerLoadingSkeleton({ 'data-testid': testId }: { 'data-testid'?: string }) {
  return (
    <div
      className="flex flex-col gap-3 p-5"
      data-testid={testId ?? DRAWER_TEST_IDS.LOADING_SKELETON}
      aria-busy="true"
      aria-label="Caricamento in corso"
    >
      {/* Fake hero image */}
      <div className="h-[140px] w-full animate-pulse rounded-xl bg-muted" />
      {/* Fake tab strip */}
      <div className="flex gap-2">
        {[80, 110, 70].map(w => (
          <div key={w} className="h-8 animate-pulse rounded-lg bg-muted" style={{ width: w }} />
        ))}
      </div>
      {/* Fake stat cards */}
      <div className="grid grid-cols-3 gap-2">
        {[1, 2, 3].map(i => (
          <div key={i} className="h-16 animate-pulse rounded-lg bg-muted" />
        ))}
      </div>
      {/* Fake text block */}
      <div className="h-20 w-full animate-pulse rounded-lg bg-muted" />
    </div>
  );
}

/** Error state with optional retry button */
export function DrawerErrorState({
  error,
  onRetry,
  'data-testid': testId,
}: {
  error: string;
  onRetry?: () => void;
  'data-testid'?: string;
}) {
  return (
    <div
      className="flex flex-col items-center justify-center gap-4 p-8 text-center"
      data-testid={testId ?? DRAWER_TEST_IDS.ERROR_STATE}
      role="alert"
    >
      <AlertCircle className="h-10 w-10 text-red-400" aria-hidden="true" />
      <div className="space-y-1">
        <p className="font-quicksand text-sm font-semibold text-foreground">
          Si è verificato un errore
        </p>
        <p className="font-nunito text-xs text-muted-foreground">{error}</p>
      </div>
      {onRetry && (
        <button
          onClick={onRetry}
          className={cn(
            'flex items-center gap-1.5 rounded-lg',
            'bg-muted px-3 py-1.5',
            'font-nunito text-xs font-medium text-foreground',
            'transition-colors duration-150 hover:bg-muted',
            'focus:outline-none focus:ring-2 focus:ring-slate-400 focus:ring-offset-1'
          )}
        >
          <RefreshCw className="h-3.5 w-3.5" aria-hidden="true" />
          Riprova
        </button>
      )}
    </div>
  );
}

/** Placeholder shown for entity types not yet implemented */
export function DrawerComingSoon({ label, issueNumber }: { label: string; issueNumber: number }) {
  return (
    <div
      className="flex flex-col items-center justify-center gap-3 p-8 text-center"
      data-testid={DRAWER_TEST_IDS.COMING_SOON(issueNumber)}
    >
      <div className="flex h-12 w-12 items-center justify-center rounded-full bg-muted">
        <Wrench className="h-5 w-5 text-muted-foreground" aria-hidden="true" />
      </div>
      <div className="space-y-1">
        <p className="font-quicksand text-sm font-semibold text-muted-foreground">{label}</p>
        <p className="font-nunito text-xs text-muted-foreground">In arrivo — Issue #{issueNumber}</p>
      </div>
    </div>
  );
}

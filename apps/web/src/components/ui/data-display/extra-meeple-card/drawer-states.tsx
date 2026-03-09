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
      <div className="h-[140px] w-full animate-pulse rounded-xl bg-slate-200" />
      {/* Fake tab strip */}
      <div className="flex gap-2">
        {[80, 110, 70].map(w => (
          <div key={w} className="h-8 animate-pulse rounded-lg bg-slate-200" style={{ width: w }} />
        ))}
      </div>
      {/* Fake stat cards */}
      <div className="grid grid-cols-3 gap-2">
        {[1, 2, 3].map(i => (
          <div key={i} className="h-16 animate-pulse rounded-lg bg-slate-200" />
        ))}
      </div>
      {/* Fake text block */}
      <div className="h-20 w-full animate-pulse rounded-lg bg-slate-200" />
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
        <p className="font-quicksand text-sm font-semibold text-slate-700">
          Si è verificato un errore
        </p>
        <p className="font-nunito text-xs text-slate-500">{error}</p>
      </div>
      {onRetry && (
        <button
          onClick={onRetry}
          className={cn(
            'flex items-center gap-1.5 rounded-lg',
            'bg-slate-100 px-3 py-1.5',
            'font-nunito text-xs font-medium text-slate-700',
            'transition-colors duration-150 hover:bg-slate-200',
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
      <div className="flex h-12 w-12 items-center justify-center rounded-full bg-slate-100">
        <Wrench className="h-5 w-5 text-slate-400" aria-hidden="true" />
      </div>
      <div className="space-y-1">
        <p className="font-quicksand text-sm font-semibold text-slate-600">{label}</p>
        <p className="font-nunito text-xs text-slate-400">In arrivo — Issue #{issueNumber}</p>
      </div>
    </div>
  );
}

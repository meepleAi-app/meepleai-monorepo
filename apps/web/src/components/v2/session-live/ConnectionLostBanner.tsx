'use client';

/**
 * ConnectionLostBanner — Wave D.2 Interactions sub-PR (Issue #750).
 *
 * SSE connection state indicator banner. Three variants:
 *
 * - 'reconnecting': polite announcement + retry count indicator
 *     role="status" aria-live="polite"
 * - 'degraded-polling': manual retry CTA (polling fallback active)
 *     role="status" aria-live="polite"
 * - 'failed': urgent error + retry button (disabled if 60s cooldown)
 *     role="alert" (assertive, live)
 *
 * Labels.retryCountResolved: ICU plural string pre-resolved by orchestrator
 *   (e.g. "Tentativo 1 di 5").
 *
 * Gate C: DIVERGES from MeepleCard — inline status banner, not a card pattern.
 *
 * data-slot="connection-lost-banner" — required by unit tests.
 * data-kind={kind} — variant assertion in unit tests.
 */

import type { ReactElement } from 'react';

import { WifiOff, RefreshCw, AlertTriangle } from 'lucide-react';

// ─── Types ────────────────────────────────────────────────────────────────────

export type ConnectionLostBannerKind = 'reconnecting' | 'degraded-polling' | 'failed';

// ─── Labels ───────────────────────────────────────────────────────────────────

export interface ConnectionLostBannerLabels {
  /** ICU plural string pre-resolved by orchestrator: "Tentativo {n}/5" */
  readonly retryCountResolved: string;
  readonly reconnecting: string;
  readonly degradedPolling: string;
  readonly failed: string;
  readonly manualRetryLabel: string;
}

// ─── Props ────────────────────────────────────────────────────────────────────

export interface ConnectionLostBannerProps {
  readonly kind: ConnectionLostBannerKind;
  readonly retryCount?: number;
  readonly retryAt?: Date | null;
  readonly onManualRetry?: () => void;
  readonly labels: ConnectionLostBannerLabels;
  readonly className?: string;
}

// ─── Component ────────────────────────────────────────────────────────────────

export function ConnectionLostBanner({
  kind,
  retryCount,
  retryAt: _retryAt,
  onManualRetry,
  labels,
  className,
}: ConnectionLostBannerProps): ReactElement {
  const isAlert = kind === 'failed';
  const role = isAlert ? 'alert' : 'status';
  const ariaLive = isAlert ? undefined : 'polite';

  // Color scheme per kind
  const bannerColor = {
    reconnecting: 'border-amber-700/40 bg-amber-900/20 text-amber-200',
    'degraded-polling': 'border-blue-700/40 bg-blue-900/20 text-blue-200',
    failed: 'border-rose-700/40 bg-rose-900/20 text-rose-200',
  }[kind];

  const IconComponent = {
    reconnecting: RefreshCw,
    'degraded-polling': WifiOff,
    failed: AlertTriangle,
  }[kind];

  const messageText = {
    reconnecting: labels.reconnecting,
    'degraded-polling': labels.degradedPolling,
    failed: labels.failed,
  }[kind];

  const showRetryCount = kind === 'reconnecting' && typeof retryCount === 'number';
  const showRetryButton = (kind === 'degraded-polling' || kind === 'failed') && !!onManualRetry;

  // Retry button is disabled for 60s cooldown when kind='failed' and retryAt is in future
  // The orchestrator controls this via onManualRetry being undefined when in cooldown.
  // We expose the button whenever onManualRetry is provided.
  const retryButtonDisabled = false; // orchestrator manages cooldown externally

  return (
    <div
      data-slot="connection-lost-banner"
      data-kind={kind}
      role={role}
      aria-live={ariaLive}
      className={`flex items-center gap-3 rounded-lg border px-4 py-2.5 text-sm
        ${bannerColor} ${className ?? ''}`}
    >
      <IconComponent
        className={`h-4 w-4 shrink-0 ${kind === 'reconnecting' ? 'animate-spin' : ''}`}
        aria-hidden="true"
      />

      <div className="flex flex-1 flex-wrap items-center gap-2">
        <span>{messageText}</span>
        {showRetryCount && <span className="text-xs opacity-80">{labels.retryCountResolved}</span>}
      </div>

      {showRetryButton && (
        <button
          type="button"
          onClick={onManualRetry}
          disabled={retryButtonDisabled}
          aria-label={labels.manualRetryLabel}
          className="shrink-0 rounded-md border border-current px-2 py-1 text-xs font-medium
            transition-opacity hover:opacity-80
            disabled:cursor-not-allowed disabled:opacity-40
            focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-current"
        >
          {labels.manualRetryLabel}
        </button>
      )}
    </div>
  );
}

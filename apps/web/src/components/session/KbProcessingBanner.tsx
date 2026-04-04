'use client';

/**
 * KbProcessingBanner — Degraded mode banner when KB is still processing.
 *
 * Polls document status every 10 seconds, up to 30 attempts (5 min).
 * Shows processing state, then transitions to "ready" when KB indexing completes.
 *
 * Issue #123 — Game Night Quick Start Wizard
 */

import { useEffect, useRef, useState } from 'react';

import { AlertTriangle, Check, Loader2 } from 'lucide-react';

// ============================================================================
// Types
// ============================================================================

interface KbProcessingBannerProps {
  gameId: string;
  onReady?: () => void;
}

type BannerState = 'processing' | 'ready' | 'timeout';

const POLL_INTERVAL_MS = 10_000;
const MAX_POLL_ATTEMPTS = 30; // 5 minutes

// ============================================================================
// Component
// ============================================================================

export function KbProcessingBanner({ gameId, onReady }: KbProcessingBannerProps) {
  const [state, setState] = useState<BannerState>('processing');
  const pollCount = useRef(0);

  useEffect(() => {
    const interval = setInterval(async () => {
      pollCount.current++;

      if (pollCount.current > MAX_POLL_ATTEMPTS) {
        setState('timeout');
        clearInterval(interval);
        return;
      }

      try {
        const response = await fetch(`/api/v1/documents/game/${gameId}/status`);
        if (!response.ok) return; // Retry on next interval
        const data = await response.json();
        if (data.isReady) {
          setState('ready');
          onReady?.();
          clearInterval(interval);
        }
      } catch {
        // Ignore polling errors — will retry on next interval
      }
    }, POLL_INTERVAL_MS);

    return () => clearInterval(interval);
  }, [gameId, onReady]);

  if (state === 'ready') {
    return (
      <div
        className="rounded-lg border border-green-500/30 bg-green-500/10 px-4 py-2 text-sm flex items-center gap-2 text-green-700 dark:text-green-300"
        data-testid="kb-banner-ready"
      >
        <Check className="h-4 w-4" aria-hidden="true" />
        Regolamento pronto! L&apos;agente AI conosce le regole.
      </div>
    );
  }

  if (state === 'timeout') {
    return (
      <div
        className="rounded-lg border border-slate-500/30 bg-slate-500/10 px-4 py-2 text-sm flex items-center gap-2 text-slate-700 dark:text-slate-300"
        data-testid="kb-banner-timeout"
      >
        <AlertTriangle className="h-4 w-4" aria-hidden="true" />
        L&apos;elaborazione sta richiedendo più tempo del previsto. Puoi continuare a giocare.
      </div>
    );
  }

  return (
    <div
      className="rounded-lg border border-amber-500/30 bg-amber-500/10 px-4 py-2 text-sm flex items-center gap-2 text-amber-700 dark:text-amber-200"
      data-testid="kb-banner-processing"
    >
      <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
      Regolamento in elaborazione... L&apos;agente avrà accesso alle regole tra poco.
    </div>
  );
}

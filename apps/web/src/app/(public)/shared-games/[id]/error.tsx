/**
 * /shared-games/[id] — route-level error boundary (Wave A.4 follow-up · Issue #615).
 *
 * Fires when `page.tsx`'s SSR fetch re-throws a non-404 failure (5xx, network,
 * timeout, schema mismatch). The `loadInitialData` helper deliberately
 * differentiates 404 (→ `notFound()` → `not-found.tsx`) from everything else
 * (→ this boundary), so this surface is reserved for unexpected backend
 * trouble — retry is the right primary action.
 *
 * Distinct from `page-client.tsx`'s post-mount error branch: that one covers
 * background refetch failures after a successful first paint; this one covers
 * the cold-start SSR case. The user-facing copy is the same on purpose.
 *
 * Client Component (mandatory for Next.js error boundaries — `reset` is a
 * client-only callback).
 */

'use client';

import { useEffect, type JSX } from 'react';

import { ErrorState } from '@/components/ui/v2/shared-game-detail';
import { logger } from '@/lib/logger';

interface SharedGameDetailErrorProps {
  readonly error: Error & { digest?: string };
  readonly reset: () => void;
}

export default function SharedGameDetailError({
  error,
  reset,
}: SharedGameDetailErrorProps): JSX.Element {
  useEffect(() => {
    logger.error('SharedGameDetail SSR error:', error);
  }, [error]);

  return (
    <main
      data-testid="shared-game-detail-page"
      data-state="error"
      className="min-h-screen bg-background"
    >
      <div className="mx-auto max-w-[1024px] px-4 py-8 sm:px-6 lg:px-8">
        <ErrorState
          labels={{
            title: 'Errore di caricamento',
            description:
              'Non siamo riusciti a caricare il dettaglio del gioco. Riprova fra un momento.',
            retryLabel: 'Riprova',
          }}
          onRetry={reset}
        />
        {process.env.NODE_ENV === 'development' && error.digest && (
          <p className="mt-3 text-center font-mono text-xs text-[hsl(var(--text-muted))]">
            Digest: {error.digest}
          </p>
        )}
      </div>
    </main>
  );
}

/**
 * /shared-games/[id] — server-side 404 boundary (Wave A.4 follow-up · Issue #615).
 *
 * Fires when `page.tsx` calls `notFound()` after detecting a backend HTTP 404
 * during SSR data fetch. Distinct from `page-client.tsx`'s post-mount
 * `NotFoundState` branch (which fires when the user navigates to a stale id
 * after the game was unpublished). Same component, same labels — Next.js
 * routes the SSR case here automatically.
 *
 * Server Component: labels hardcoded in Italian (project is single-locale —
 * mirror `apps/web/src/app/admin/(dashboard)/shared-games/not-found.tsx`).
 */

import type { JSX } from 'react';

import { NotFoundState } from '@/components/ui/v2/shared-game-detail';

export default function SharedGameDetailNotFound(): JSX.Element {
  return (
    <main
      data-testid="shared-game-detail-page"
      data-state="not-found"
      className="min-h-screen bg-background"
    >
      <div className="mx-auto max-w-[1024px] px-4 py-8 sm:px-6 lg:px-8">
        <NotFoundState
          labels={{
            title: 'Gioco non trovato',
            description:
              'Il gioco richiesto non esiste o non è più disponibile nel catalogo community.',
            backLabel: 'Torna al catalogo',
          }}
        />
      </div>
    </main>
  );
}

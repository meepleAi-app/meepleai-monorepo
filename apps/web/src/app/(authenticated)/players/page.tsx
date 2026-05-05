/**
 * Players page — /players
 *
 * Wave 4 D1 (Issue #682): thin client shell.
 * All rendering, data-fetching, and FSM logic live in PlayersLibraryView.
 *
 * Suspense boundary required: PlayersLibraryView calls `useSearchParams()`
 * for the `?state=` URL override, which triggers CSR bailout during static
 * prerender (Next.js 16). Wrapping in Suspense allows the page to prerender
 * without the search params, which are then resolved on client hydration.
 * Mirror Wave B.1 GamesHubPage pattern.
 *
 * @see apps/web/src/app/(authenticated)/players/_components/PlayersLibraryView.tsx
 */

'use client';

import { Suspense } from 'react';

import { PlayersLibraryView } from './_components/PlayersLibraryView';

export default function PlayersPage() {
  return (
    <Suspense>
      <PlayersLibraryView />
    </Suspense>
  );
}

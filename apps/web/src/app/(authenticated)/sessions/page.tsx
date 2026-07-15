/**
 * Sessions page — /sessions
 *
 * Wave D.1 (Issue #735): thin client shell.
 * All rendering, data-fetching, and FSM logic live in SessionsLibraryView.
 *
 * Suspense boundary required: SessionsLibraryView calls `useSearchParams()`
 * for URL-based filter state (status/view/search/state overrides), which
 * triggers CSR bailout during static prerender (Next.js 16). Wrapping in
 * Suspense allows the page to prerender without the search params, which
 * are then resolved on client hydration.
 * Mirror Wave 4 D1 PlayersPage + Wave B.1 GamesHubPage pattern.
 *
 * @see apps/web/src/app/(authenticated)/sessions/_components/SessionsLibraryView.tsx
 */

'use client';

import { Suspense } from 'react';

import { SessionsLibraryView } from './_components/SessionsLibraryView';

export default function SessionsPage() {
  return (
    <Suspense>
      <SessionsLibraryView />
    </Suspense>
  );
}

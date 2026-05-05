/**
 * Gamebook upload page route (Sprint 1, Task 1.8)
 *
 * Path: /gamebook/upload?gameId=<uuid>
 *
 * The game picker is Phase 2 scope — Sprint 1 accepts `gameId` via
 * query param with a stub fallback for manual testing.
 *
 * SSR: no data fetching (upload is client-only mutation).
 */

import { Suspense, type JSX } from 'react';

import { GamebookUploadClient } from './_components/GamebookUploadClient';

export const metadata = {
  title: 'Carica Foto Manuale | MeepleAI',
  description: "Carica le foto del manuale di gioco per l'indicizzazione AI",
};

export default function GamebookUploadPage(): JSX.Element {
  return (
    <div className="container mx-auto max-w-2xl py-8 px-4">
      <Suspense fallback={<div className="animate-pulse h-64 rounded-lg bg-muted" />}>
        <GamebookUploadClient />
      </Suspense>
    </div>
  );
}

'use client';

import type { ReactElement } from 'react';

import { TranslateViewer } from '@/components/features/gamebook';
import { GameRefKind, type GameRef } from '@/lib/api/gamebook';

/**
 * Routes under `/library/[gameId]/...` (PR #1037 IA consolidation) accept any
 * SharedGame id. Until the campaign DTO exposes the `GameRef` discriminator
 * (deferred follow-up), we default `kind` to `Shared` here — the BookPicker
 * endpoint will gracefully return an empty list for private-game gameIds,
 * surfacing the "no narrative books" copy.
 */
export function Content({
  gameId,
  campaignId,
}: {
  gameId: string;
  campaignId: string;
}): ReactElement {
  const gameRef: GameRef = { id: gameId, kind: GameRefKind.Shared };
  return (
    <main className="min-h-[calc(100vh-var(--app-topbar-height,64px))]">
      <TranslateViewer campaignId={campaignId} gameRef={gameRef} />
    </main>
  );
}

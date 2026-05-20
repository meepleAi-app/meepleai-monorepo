'use client';

import { useCallback, type ReactElement } from 'react';

import { useRouter } from 'next/navigation';

import { GamebookPlayShell } from '@/components/features/gamebook';
import { ResumeBooksList } from '@/components/features/gamebook/ResumeBooksList';
import { GameRefKind, type GameRef } from '@/lib/api/gamebook';

export function Content({
  campaignId,
  gameId,
}: {
  campaignId: string;
  gameId: string;
}): ReactElement {
  const router = useRouter();
  // E3 (multi-book): default discriminator to Shared until the campaign DTO
  // exposes `GameRef` natively (see _content.tsx in /translate for the same
  // rationale). Empty-book responses are handled by the picker.
  const gameRef: GameRef = { id: gameId, kind: GameRefKind.Shared };

  // E2 wiring placeholder: a GET endpoint for per-book `SessionBookProgress`
  // does not yet exist (deferred Phase F follow-up — query handler + endpoint
  // pair). Until then we pass an empty progress list so the component renders
  // its empty-state copy. When the endpoint lands, swap to:
  //   const { data: progress } = useSessionBookProgress(campaignId);
  const bookProgress: Array<{
    bookId: string;
    bookName: string;
    lastLocation: string;
    lastVisitedAt: string;
  }> = [];

  const handleResume = useCallback(
    (bookId: string) => {
      // Future: route to a per-book play surface; for now we just refresh
      // the current page so the picker re-selects.
      router.refresh();
      void bookId;
    },
    [router]
  );

  return (
    <main className="h-[calc(100vh-var(--app-topbar-height,64px))] flex flex-col">
      <ResumeBooksList progress={bookProgress} onResume={handleResume} />
      <GamebookPlayShell campaignId={campaignId} gameId={gameId} gameRef={gameRef} />
    </main>
  );
}

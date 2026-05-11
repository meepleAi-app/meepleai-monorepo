'use client';

import type { ReactElement } from 'react';

import { GamebookPlayShell } from '@/components/v2/gamebook';

export function Content({
  campaignId,
  gameId,
}: {
  campaignId: string;
  gameId: string;
}): ReactElement {
  return (
    <main className="h-[calc(100vh-var(--app-topbar-height,64px))]">
      <GamebookPlayShell campaignId={campaignId} gameId={gameId} />
    </main>
  );
}

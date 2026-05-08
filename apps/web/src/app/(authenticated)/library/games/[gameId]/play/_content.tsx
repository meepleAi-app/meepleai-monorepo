'use client';

import type { ReactElement } from 'react';

import { GamebookResumeShell } from './_components/GamebookResumeShell';

export function ResumePageContent({ gameId }: { gameId: string }): ReactElement {
  return (
    <main className="min-h-[calc(100vh-var(--app-topbar-height,64px))]">
      <GamebookResumeShell gameId={gameId} />
    </main>
  );
}

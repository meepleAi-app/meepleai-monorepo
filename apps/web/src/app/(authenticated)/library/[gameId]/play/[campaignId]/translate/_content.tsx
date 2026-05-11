'use client';

import type { ReactElement } from 'react';

import { TranslateViewer } from '@/components/features/gamebook';

export function Content({ campaignId }: { gameId: string; campaignId: string }): ReactElement {
  return (
    <main className="min-h-[calc(100vh-var(--app-topbar-height,64px))]">
      <TranslateViewer campaignId={campaignId} />
    </main>
  );
}

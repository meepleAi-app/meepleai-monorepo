'use client';

import { useState, type ReactElement } from 'react';

import { CampaignSetupDrawer } from '@/components/v2/gamebook/CampaignSetupDrawer';
import { useLibraryGameDetail } from '@/hooks/queries/useLibrary';

import { GamebookResumeShell } from './_components/GamebookResumeShell';

/**
 * Iter 5 wiring: lift the CampaignSetupDrawer to page level so EmptyFirstTime,
 * MultiCampaignList "+ new" and StaleWarning "+ new" all open the same wizard
 * via the `onCreateCampaign` callback prop.
 */
export function ResumePageContent({ gameId }: { gameId: string }): ReactElement {
  const [drawerOpen, setDrawerOpen] = useState(false);
  const { data: gameDetail } = useLibraryGameDetail(gameId);
  const gameTitle = gameDetail?.gameTitle ?? '';

  return (
    <main className="min-h-[calc(100vh-var(--app-topbar-height,64px))]">
      <GamebookResumeShell
        gameId={gameId}
        gameTitle={gameTitle}
        onCreateCampaign={() => setDrawerOpen(true)}
      />
      <CampaignSetupDrawer
        gameId={gameId}
        gameTitle={gameTitle}
        open={drawerOpen}
        onOpenChange={setDrawerOpen}
      />
    </main>
  );
}

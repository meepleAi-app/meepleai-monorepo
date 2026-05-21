'use client';

import { useMemo, type ReactElement } from 'react';

import { TranslateViewer } from '@/components/features/gamebook';
import { GameRefKind, type GameRef } from '@/lib/api/gamebook';
import { useGamebookCampaign } from '@/lib/gamebook/hooks/useGamebookCampaign';

/**
 * Routes under `/library/[gameId]/...` (PR #1037 IA consolidation) accept any
 * SharedGame id. Issue #1392: derive the GameRef discriminator from the
 * campaign DTO so private-game campaigns route correctly. Fallback to Shared +
 * route gameId while the campaign is still loading — the BookPicker endpoint
 * gracefully returns an empty list for unknown gameIds.
 */
export function Content({
  gameId,
  campaignId,
}: {
  gameId: string;
  campaignId: string;
}): ReactElement {
  const { data: campaign } = useGamebookCampaign(campaignId);
  const gameRef: GameRef = useMemo(() => {
    if (campaign) {
      return {
        id: campaign.gameRefId,
        kind: campaign.gameRefKind === 1 ? GameRefKind.Private : GameRefKind.Shared,
      };
    }
    return { id: gameId, kind: GameRefKind.Shared };
  }, [campaign, gameId]);
  return (
    <main className="min-h-[calc(100vh-var(--app-topbar-height,64px))]">
      <TranslateViewer campaignId={campaignId} gameRef={gameRef} />
    </main>
  );
}

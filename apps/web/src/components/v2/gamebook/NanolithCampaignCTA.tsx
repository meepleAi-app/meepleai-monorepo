'use client';

import type { ReactElement } from 'react';

import { NewCampaignDialog } from './NewCampaignDialog';

export interface NanolithCampaignCTAProps {
  gameId: string;
  gameTitle: string;
}

/**
 * Iter 1.A hardcoded CTA — renders only for the Nanolith game.
 * Will be replaced by a feature-flag / libro-game metadata check in Iter 2.
 */
export function NanolithCampaignCTA({
  gameId,
  gameTitle,
}: NanolithCampaignCTAProps): ReactElement | null {
  if (gameTitle !== 'Nanolith') return null;

  return (
    <NewCampaignDialog
      gameId={gameId}
      trigger={
        <button
          className="rounded-md bg-[var(--c-game)] px-4 py-2 text-sm font-medium text-white hover:opacity-90"
          data-testid="nanolith-campaign-cta"
        >
          Nuova campagna libro game
        </button>
      }
    />
  );
}

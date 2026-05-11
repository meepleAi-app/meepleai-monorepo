'use client';

import type { ReactElement } from 'react';

import { isLibroGame } from '@/lib/games/is-libro-game';

import { CampaignSetupDrawer } from './CampaignSetupDrawer';

export interface NanolithCampaignCTAProps {
  gameId: string;
  gameTitle: string;
}

/**
 * Libro game CTA — renders when the game qualifies as a libro game.
 *
 * Iter 2 (storyboard step 03): detection now flows through the centralised
 * `isLibroGame()` helper so the catalog card badge, this CTA, and any future
 * surface share a single allowlist. The hardcoded `title === 'Nanolith'`
 * check from Iter 1.A is preserved internally and will swap to a metadata
 * flag once the backend exposes it.
 */
export function NanolithCampaignCTA({
  gameId,
  gameTitle,
}: NanolithCampaignCTAProps): ReactElement | null {
  if (!isLibroGame({ gameTitle })) return null;

  return (
    <div className="flex flex-col items-stretch gap-1" data-slot="libro-game-cta">
      <CampaignSetupDrawer
        gameId={gameId}
        gameTitle={gameTitle}
        trigger={
          <button
            type="button"
            data-testid="nanolith-campaign-cta"
            className="
              group inline-flex w-full items-center justify-center gap-2
              rounded-[14px] bg-[hsl(var(--c-session))] px-5 py-3.5
              font-[var(--font-quicksand)] text-[17px] font-bold text-white
              shadow-[0_6px_20px_hsl(var(--c-session)/0.35)]
              transition-transform duration-[180ms] ease-out
              hover:-translate-y-px hover:shadow-[0_8px_24px_hsl(var(--c-session)/0.45)]
              focus-visible:outline-none focus-visible:ring-[3px] focus-visible:ring-[hsl(var(--c-session)/0.4)] focus-visible:ring-offset-2
              motion-reduce:transition-none
            "
          >
            <span aria-hidden="true" className="text-[1.1em]">
              📖
            </span>
            Avvia libro game
          </button>
        }
      />
      <p className="text-center text-[11px] text-[var(--mc-text-muted,var(--text-muted))]">
        Tutorial setup · Q&amp;A regole · Traduzione storybook
      </p>
    </div>
  );
}

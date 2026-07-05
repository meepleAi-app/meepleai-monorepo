'use client';

import { useCallback, useMemo, type ReactElement } from 'react';

import { useRouter } from 'next/navigation';

import { GamebookPlayShell } from '@/components/features/gamebook';
import { ResumeBooksList } from '@/components/features/gamebook/ResumeBooksList';
import {
  SerataResumeButton,
  isSerataResumable,
} from '@/components/features/gamebook/SerataResumeButton';
import { SerataSpineStrip } from '@/components/features/gamebook/SerataSpineStrip';
import { useCurrentUser } from '@/hooks/queries/useCurrentUser';
import { GameRefKind, type GameRef } from '@/lib/api/gamebook';
import { useCampaignProgress } from '@/lib/gamebook/hooks/useCampaignProgress';
import {
  useGamebookCampaign,
  useGamebookCampaignSpine,
} from '@/lib/gamebook/hooks/useGamebookCampaign';

export function Content({
  campaignId,
  gameId,
}: {
  campaignId: string;
  gameId: string;
}): ReactElement {
  const router = useRouter();
  // Issue #1392: derive the GameRef discriminator from the campaign DTO so
  // private-game campaigns route correctly (the BE now emits gameRefKind).
  // Fallback to Shared + route gameId while the campaign is still loading.
  // On fetch errors (e.g. deleted campaign) we log + still render the shell
  // with the route-gameId fallback to avoid a hard crash — the picker copy
  // surfaces the empty state to the user.
  const { data: campaign, isError, error } = useGamebookCampaign(campaignId);
  if (isError && process.env.NODE_ENV !== 'production') {
    console.warn('[gamebook] campaign fetch failed, using route-gameId fallback', error);
  }
  const gameRef: GameRef = useMemo(() => {
    if (campaign) {
      return {
        id: campaign.gameRefId,
        kind: campaign.gameRefKind === 1 ? GameRefKind.Private : GameRefKind.Shared,
      };
    }
    return { id: gameId, kind: GameRefKind.Shared };
  }, [campaign, gameId]);

  // Issue #1388: GET /campaigns/{id}/progress returns one row per book the user
  // has engaged with, server-side sorted by most-recent visit first.
  const { data: bookProgress = [] } = useCampaignProgress(campaignId);

  // #2632 (SI-1b Phase 4): the owning GameNight "Serata" spine. Null when the campaign is played
  // standalone (204) — the strip is only shown for GameNight-attached play.
  const { data: spine } = useGamebookCampaignSpine(campaignId);

  // SI-4 (#2635): the organizer can resume the serata as a NEW live sitting when the night is
  // resumable (Published/InProgress, no live session). Standalone campaigns keep pure reading.
  const { data: currentUser } = useCurrentUser();

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
      {spine ? (
        <div className="px-3 pt-2">
          <SerataSpineStrip spine={spine} />
          {isSerataResumable(spine, currentUser?.id) ? (
            <div className="mt-2">
              <SerataResumeButton gameNightId={spine.gameNightId} campaignId={campaignId} />
            </div>
          ) : null}
        </div>
      ) : null}
      <ResumeBooksList progress={bookProgress} onResume={handleResume} />
      <GamebookPlayShell campaignId={campaignId} gameId={gameId} gameRef={gameRef} />
    </main>
  );
}

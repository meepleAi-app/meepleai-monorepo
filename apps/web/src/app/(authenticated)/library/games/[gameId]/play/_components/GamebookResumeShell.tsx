'use client';

import type { ReactElement } from 'react';

import { useUserCampaigns } from '@/lib/gamebook/hooks/useUserCampaigns';

import { EmptyFirstTime } from './EmptyFirstTime';
import { MultiCampaignList } from './MultiCampaignList';
import { ResumeHero } from './ResumeHero';
import { StaleWarningCard } from './StaleWarningCard';

export interface GamebookResumeShellProps {
  gameId: string;
  gameTitle?: string;
  onCreateCampaign?: () => void;
  onArchive?: (campaignId: string) => void;
}

/**
 * Resume picker shell per `/library/games/[gameId]/play` (issue #835).
 *
 * Dispatch logic sui 4 stati di mockup G:
 *   - state-01-first-time      : data.length === 0
 *   - state-02-single-resume   : data.length === 1 && !data[0].isStale
 *   - state-03-multi-campaign  : data.length >= 2
 *   - state-04-stale-warning   : data.length === 1 && data[0].isStale
 *
 * Note: distinto da `GamebookPlayShell` che gestisce la singola campagna in-game
 * a `/play/[campaignId]`. Vedi #836 per chiarimento scope routing.
 */
export function GamebookResumeShell({
  gameId,
  gameTitle,
  onCreateCampaign,
  onArchive,
}: GamebookResumeShellProps): ReactElement {
  const { data, isLoading, isError } = useUserCampaigns(gameId);

  if (isLoading) {
    return (
      <div
        data-testid="gamebook-resume-shell-skeleton"
        className="max-w-screen-sm mx-auto px-4 py-6"
      >
        <div className="aspect-[4/3] animate-pulse rounded-2xl bg-muted/40" />
      </div>
    );
  }

  if (isError || !data) {
    return (
      <div
        role="alert"
        data-testid="gamebook-resume-shell-error"
        className="max-w-screen-sm mx-auto px-4 py-6"
      >
        <div className="rounded-lg border border-destructive/40 bg-destructive/5 px-4 py-3 text-sm text-destructive">
          Impossibile caricare le tue campagne. Riprova più tardi.
        </div>
      </div>
    );
  }

  if (data.length === 0) {
    return (
      <EmptyFirstTime
        gameId={gameId}
        gameTitle={gameTitle}
        onCreateCampaign={onCreateCampaign}
      />
    );
  }

  if (data.length === 1) {
    const campaign = data[0];
    if (campaign.isStale) {
      return (
        <StaleWarningCard
          campaign={campaign}
          gameId={gameId}
          onCreateNew={onCreateCampaign}
          onArchive={onArchive ? () => onArchive(campaign.id) : undefined}
        />
      );
    }
    return (
      <ResumeHero
        campaign={campaign}
        gameId={gameId}
        onCreateNew={onCreateCampaign}
      />
    );
  }

  return (
    <MultiCampaignList
      campaigns={data}
      gameId={gameId}
      onCreateNew={onCreateCampaign}
    />
  );
}

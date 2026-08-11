'use client';

import { useSharedGameNightPhotos, useSharedGameNightSummary } from '@/hooks/queries/useGameNights';
import { useTranslation } from '@/hooks/useTranslation';

import { toNightSummaryViewModel } from './night-summary-adapter';
import { NightSummaryView } from './NightSummaryView';
import { GameNightPhotoGallery } from '../photos/GameNightPhotoGallery';

export interface SharedGameNightSummaryViewProps {
  readonly token: string;
}

/**
 * Public, read-only game-night summary reached via a share token (#2702). Possession of the
 * token authorises the read; no share/archive/navigation CTAs are exposed.
 */
export function SharedGameNightSummaryView({ token }: SharedGameNightSummaryViewProps) {
  const { t, locale } = useTranslation();
  const query = useSharedGameNightSummary(token);
  const photosQuery = useSharedGameNightPhotos(token);

  if (query.isLoading) {
    return (
      <div data-testid="shared-summary-loading" className="p-8 text-center text-muted-foreground">
        {t('gameNightDetail.summary.loading')}
      </div>
    );
  }

  if (query.isError || !query.data) {
    return (
      <div data-testid="shared-summary-error" className="p-8 text-center text-muted-foreground">
        {t('gameNightDetail.summary.error')}
      </div>
    );
  }

  const { night, mvp, games, eventsCount } = toNightSummaryViewModel(query.data, { locale, t });

  return (
    <div className="flex flex-col gap-6">
      <NightSummaryView
        night={night}
        mvp={mvp}
        games={games}
        eventsCount={eventsCount}
        archived={query.data.isArchived}
      />
      {(photosQuery.data?.length ?? 0) > 0 && (
        <GameNightPhotoGallery photos={photosQuery.data ?? []} />
      )}
    </div>
  );
}

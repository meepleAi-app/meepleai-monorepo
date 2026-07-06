'use client';

import { useCallback, useState } from 'react';

import { useRouter } from 'next/navigation';

import { GameNightPhotoGallery } from '@/components/features/game-nights/photos/GameNightPhotoGallery';
import { GameNightPhotoUploadDialog } from '@/components/features/game-nights/photos/GameNightPhotoUploadDialog';
import { NightSummaryView } from '@/components/features/game-nights/summary';
import { toNightSummaryViewModel } from '@/components/features/game-nights/summary/night-summary-adapter';
import { useCurrentUser } from '@/hooks/queries/useCurrentUser';
import {
  useDeleteGameNightPhoto,
  useGameNightPhotos,
  useGameNightSummary,
  useGenerateGameNightShareToken,
  useSetGameNightArchived,
} from '@/hooks/queries/useGameNights';
import { useTranslation } from '@/hooks/useTranslation';
import type { GameNightPhotoDto } from '@/lib/api/schemas/game-nights.schemas';

export interface NightSummaryClientViewProps {
  readonly nightId: string;
}

export function NightSummaryClientView({ nightId }: NightSummaryClientViewProps) {
  const router = useRouter();
  const { t, locale } = useTranslation();

  const summaryQuery = useGameNightSummary(nightId);
  const photosQuery = useGameNightPhotos(nightId);
  const generateShare = useGenerateGameNightShareToken();
  const setArchived = useSetGameNightArchived();
  const deletePhoto = useDeleteGameNightPhoto(nightId);
  const { data: currentUser } = useCurrentUser();

  const [shareSuccess, setShareSuccess] = useState<{ visible: boolean; subline?: string }>({
    visible: false,
  });
  const [uploadOpen, setUploadOpen] = useState(false);

  const isViewerOrganizer = summaryQuery.data?.isViewerOrganizer ?? false;
  const canDeletePhoto = useCallback(
    (photo: GameNightPhotoDto) => isViewerOrganizer || photo.uploadedByUserId === currentUser?.id,
    [isViewerOrganizer, currentUser?.id]
  );
  const handleDeletePhoto = useCallback(
    (photoId: string) => deletePhoto.mutate(photoId),
    [deletePhoto]
  );

  const handleShare = useCallback(() => {
    generateShare.mutate(nightId, {
      onSuccess: ({ shareToken }) => {
        if (typeof window !== 'undefined' && navigator.clipboard) {
          // Public share route resolves the night from the token alone.
          const url = `${window.location.origin}/game-nights/shared/${shareToken}`;
          void navigator.clipboard.writeText(url).catch(() => undefined);
        }
        setShareSuccess({ visible: true, subline: t('gameNightDetail.summary.shareCopied') });
      },
    });
  }, [generateShare, nightId, t]);

  const handleArchive = useCallback(
    () => setArchived.mutate({ id: nightId, archived: true }),
    [setArchived, nightId]
  );
  const handleUnarchive = useCallback(
    () => setArchived.mutate({ id: nightId, archived: false }),
    [setArchived, nightId]
  );
  const handleGoToList = useCallback(() => router.push('/game-nights'), [router]);
  const handleJumpToSession = useCallback(
    (sessionId: string) => router.push(`/sessions/${sessionId}`),
    [router]
  );

  if (summaryQuery.isLoading) {
    return (
      <div data-testid="summary-loading" className="p-8 text-center text-muted-foreground">
        {t('gameNightDetail.summary.loading')}
      </div>
    );
  }

  if (summaryQuery.isError || !summaryQuery.data) {
    return (
      <div data-testid="summary-error" className="p-8 text-center text-muted-foreground">
        {t('gameNightDetail.summary.error')}
      </div>
    );
  }

  const { night, mvp, games, eventsCount } = toNightSummaryViewModel(summaryQuery.data, {
    locale,
    t,
  });

  return (
    <div className="flex flex-col gap-6">
      <NightSummaryView
        night={night}
        mvp={mvp}
        games={games}
        eventsCount={eventsCount}
        archived={summaryQuery.data.isArchived}
        shareSuccess={shareSuccess}
        onShare={handleShare}
        onArchive={handleArchive}
        onUnarchive={handleUnarchive}
        onGoToList={handleGoToList}
        onJumpToSession={handleJumpToSession}
      />

      <GameNightPhotoGallery
        photos={photosQuery.data ?? []}
        onAddPhoto={() => setUploadOpen(true)}
        onDeletePhoto={handleDeletePhoto}
        canDeletePhoto={canDeletePhoto}
      />

      <GameNightPhotoUploadDialog
        gameNightId={nightId}
        open={uploadOpen}
        onClose={() => setUploadOpen(false)}
      />
    </div>
  );
}

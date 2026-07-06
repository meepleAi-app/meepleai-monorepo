'use client';

import { useCallback, useState } from 'react';

import { useRouter } from 'next/navigation';

import { NightSummaryView } from '@/components/features/game-nights/summary';
import { toNightSummaryViewModel } from '@/components/features/game-nights/summary/night-summary-adapter';
import {
  useGameNightSummary,
  useGenerateGameNightShareToken,
  useSetGameNightArchived,
} from '@/hooks/queries/useGameNights';
import { useTranslation } from '@/hooks/useTranslation';

export interface NightSummaryClientViewProps {
  readonly nightId: string;
}

export function NightSummaryClientView({ nightId }: NightSummaryClientViewProps) {
  const router = useRouter();
  const { t, locale } = useTranslation();

  const summaryQuery = useGameNightSummary(nightId);
  const generateShare = useGenerateGameNightShareToken();
  const setArchived = useSetGameNightArchived();

  const [shareSuccess, setShareSuccess] = useState<{ visible: boolean; subline?: string }>({
    visible: false,
  });

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
  );
}

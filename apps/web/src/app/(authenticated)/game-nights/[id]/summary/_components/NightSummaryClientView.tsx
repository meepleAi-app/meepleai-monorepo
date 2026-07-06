'use client';

import { useCallback, useState } from 'react';

import { useRouter } from 'next/navigation';

import {
  NightSummaryView,
  type NightSummaryMVP,
  type NightSummaryNight,
} from '@/components/features/game-nights/summary';
import type { PerGameRecapGame } from '@/components/features/game-nights/summary';
import {
  useGameNightSummary,
  useGenerateGameNightShareToken,
  useSetGameNightArchived,
} from '@/hooks/queries/useGameNights';
import { useTranslation } from '@/hooks/useTranslation';

// ─── DTO → view-model adapter ───────────────────────────────────────────────
// Deferred (need new subsystems, #2702 follow-up): per-game top-3 leaderboard scores,
// per-session event counts, and the photo gallery. Those render empty for now.

function formatDuration(minutes: number): string {
  const h = Math.floor(minutes / 60);
  const m = minutes % 60;
  return h > 0 ? `${h}h ${m}m` : `${m}m`;
}

function initialsOf(name: string): string {
  return name
    .split(/\s+/)
    .map(w => w.charAt(0))
    .join('')
    .slice(0, 2)
    .toUpperCase();
}

function hueOf(name: string): number {
  let hue = 0;
  for (let i = 0; i < name.length; i++) {
    hue = (hue * 31 + name.charCodeAt(i)) % 360;
  }
  return hue;
}

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
          const url = `${window.location.origin}/game-nights/${nightId}/summary?share=${shareToken}`;
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

  const dto = summaryQuery.data;
  const durationUnknown = t('gameNightDetail.summary.durationUnknown');

  const startTime = new Intl.DateTimeFormat(locale, { hour: '2-digit', minute: '2-digit' }).format(
    new Date(dto.scheduledAt)
  );
  const night: NightSummaryNight = {
    title: dto.title,
    dateLine: new Intl.DateTimeFormat(locale, {
      weekday: 'long',
      day: 'numeric',
      month: 'long',
      year: 'numeric',
    }).format(new Date(dto.scheduledAt)),
    ...(dto.location ? { location: dto.location } : {}),
    startedAt: startTime,
    endedAt: '',
    duration: formatDuration(dto.kpis.totalDurationMinutes),
    nightCode: `#${dto.id.slice(0, 8).toUpperCase()}`,
  };

  const mvp: NightSummaryMVP | null = dto.mvp
    ? {
        id: dto.mvp.playerName,
        name: dto.mvp.playerName,
        initials: initialsOf(dto.mvp.playerName),
        color: hueOf(dto.mvp.playerName),
        achievements: t('gameNightDetail.summary.mvpWins', { wins: dto.mvp.wins }),
      }
    : null;

  const games: PerGameRecapGame[] = dto.games.map(g => ({
    id: g.sessionId,
    sessionId: g.sessionId,
    title: g.gameTitle,
    order: g.playOrder,
    duration: g.durationMinutes != null ? formatDuration(g.durationMinutes) : durationUnknown,
    eventsCount: 0,
    ...(g.winnerName
      ? {
          winner: {
            id: g.winnerName,
            name: g.winnerName,
            initials: initialsOf(g.winnerName),
            color: hueOf(g.winnerName),
            score: 0,
          },
        }
      : {}),
  }));

  return (
    <NightSummaryView
      night={night}
      mvp={mvp}
      games={games}
      eventsCount={0}
      archived={dto.isArchived}
      shareSuccess={shareSuccess}
      onShare={handleShare}
      onArchive={handleArchive}
      onUnarchive={handleUnarchive}
      onGoToList={handleGoToList}
      onJumpToSession={handleJumpToSession}
    />
  );
}

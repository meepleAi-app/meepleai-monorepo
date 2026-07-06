import type { GameNightSummaryDto } from '@/lib/api/schemas/game-nights.schemas';

import type { NightSummaryMVP, NightSummaryNight } from './NightSummaryView';
import type { PerGameRecapGame } from './PerGameRecapRow';

// Deferred (need new subsystems, #2702 follow-up): per-game top-3 leaderboard scores,
// per-session event counts, and the photo gallery. Those render empty for now.

export interface NightSummaryViewModel {
  readonly night: NightSummaryNight;
  readonly mvp: NightSummaryMVP | null;
  readonly games: PerGameRecapGame[];
  readonly eventsCount: number;
}

export interface NightSummaryAdapterOptions {
  readonly locale: string;
  readonly t: (key: string, vars?: Record<string, unknown>) => string;
}

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

/**
 * Maps the backend {@link GameNightSummaryDto} into the props the presentational
 * {@link NightSummaryView} expects (#2702). Shared by the authenticated and public views.
 */
export function toNightSummaryViewModel(
  dto: GameNightSummaryDto,
  { locale, t }: NightSummaryAdapterOptions
): NightSummaryViewModel {
  const scheduled = new Date(dto.scheduledAt);

  const night: NightSummaryNight = {
    title: dto.title,
    dateLine: new Intl.DateTimeFormat(locale, {
      weekday: 'long',
      day: 'numeric',
      month: 'long',
      year: 'numeric',
    }).format(scheduled),
    ...(dto.location ? { location: dto.location } : {}),
    startedAt: new Intl.DateTimeFormat(locale, { hour: '2-digit', minute: '2-digit' }).format(
      scheduled
    ),
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

  const durationUnknown = t('gameNightDetail.summary.durationUnknown');
  const games: PerGameRecapGame[] = dto.games.map(g => ({
    id: g.sessionId,
    sessionId: g.sessionId,
    title: g.gameTitle,
    order: g.playOrder,
    duration: g.durationMinutes != null ? formatDuration(g.durationMinutes) : durationUnknown,
    eventsCount: g.eventsCount,
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

  const eventsCount = dto.games.reduce((sum, g) => sum + g.eventsCount, 0);

  return { night, mvp, games, eventsCount };
}

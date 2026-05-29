/**
 * RecordCardList — List View Card
 *
 * AC-1.3: RecordCardList mostra cover gioco (via useSharedGames, fallback emoji),
 * titolo, data relativa, OutcomeBadge, meta (durata+player count+vincitore), ChipStrip.
 * No scoring inline (K2).
 *
 * Issue #1488: Play Records Index Reskin (Task 1)
 */

'use client';

import { useRouter } from 'next/navigation';
import { useTranslations } from 'next-intl';

import type { PlayRecordSummary } from '@/lib/api/schemas/play-records.schemas';
import { formatRelativeDate } from '@/lib/play-records/formatRelativeDate';
import { useSharedGames } from '@/lib/play-records/useSharedGames';
import { cn } from '@/lib/utils';

import { ChipStrip } from '../primitives/ChipStrip';
import { OutcomeBadge, type OutcomeBadgeVariant } from '../primitives/OutcomeBadge';

export interface RecordCardListProps {
  record: PlayRecordSummary;
}

function formatDuration(duration: string | null): string {
  if (!duration) return '';
  // eslint-disable-next-line security/detect-unsafe-regex
  const match = duration.match(/^(?:(\d+)\.)?(\d+):(\d+):(\d+)$/);
  if (match) {
    const days = match[1] ? parseInt(match[1], 10) : 0;
    const hours = parseInt(match[2], 10) + days * 24;
    const minutes = parseInt(match[3], 10);
    const h = hours > 0 ? `${hours}h` : '';
    const m = minutes > 0 ? `${minutes}min` : '';
    return [h, m].filter(Boolean).join(' ') || '';
  }
  return duration;
}

export function RecordCardList({ record }: RecordCardListProps) {
  const router = useRouter();
  const t = useTranslations('playRecords.index');
  const { data: games } = useSharedGames(record.gameId ? [record.gameId] : []);

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const game = record.gameId && games ? (games as any)[record.gameId] : null;
  const isInProgress = record.status === 'InProgress';
  const isPlanned = record.status === 'Planned';

  // Determine outcome badge variant (AC-1.3)
  const outcomeVariant: OutcomeBadgeVariant = isInProgress
    ? 'inprogress'
    : isPlanned
      ? 'planned'
      : record.outcomeType === 'none'
        ? 'cooperative'
        : record.winnerPlayerIds?.length === 1
          ? 'won'
          : record.winnerPlayerIds?.length === 2
            ? 'tie'
            : 'tie';

  const badgeLabels = {
    won: '🏆 Vinta',
    lost: '△ Persa',
    tie: '= Pareggio',
    cooperative: '♦ Cooperativa',
    inprogress: '● In corso',
    planned: '📅 Pianificata',
  };

  return (
    <button
      type="button"
      onClick={() => router.push(`/play-records/${record.id}`)}
      className={cn(
        'group relative flex items-stretch gap-0 rounded-lg border border-border bg-card transition-colors hover:border-border-strong hover:bg-card/90',
        'overflow-hidden cursor-pointer text-left',
        isPlanned && 'opacity-82'
      )}
      data-testid={`record-card-list-${record.id}`}
    >
      {/* Cover emoji */}
      <div className="flex w-14 flex-shrink-0 items-center justify-center border-r border-border-light bg-muted text-xl sm:w-16 sm:text-2xl">
        <span aria-hidden="true" className="drop-shadow">
          {game?.coverEmoji || '🎯'}
        </span>
      </div>

      {/* Content */}
      <div className="flex flex-1 min-w-0 flex-col justify-between gap-2 px-3 py-2.5 sm:px-4 sm:py-3">
        {/* Title + metadata row */}
        <div className="space-y-1">
          <div className="flex flex-wrap items-center gap-2">
            <h3 className="font-display text-sm font-extrabold text-foreground sm:text-base">
              {game?.title || record.gameName}
            </h3>
            <span className="font-mono text-[10px] text-muted-foreground">
              · {formatRelativeDate(record.sessionDate)}
            </span>
            <OutcomeBadge variant={outcomeVariant} labels={badgeLabels} className="text-[9px]" />
          </div>

          {/* Duration + players + winner */}
          <div className="font-mono text-xs text-muted-foreground sm:text-sm">
            ⏱ {formatDuration(record.duration)} · 👥 {record.playerCount} {t('card.players')}
          </div>
        </div>

        {/* Bottom row: CTA + Chips */}
        <div className="flex items-end justify-between gap-2">
          {isInProgress && (
            <button
              type="button"
              className="flex-shrink-0 inline-flex items-center gap-1 rounded-md bg-entity-session px-2.5 py-1.5 font-display text-[11px] font-extrabold text-white transition-all hover:bg-entity-session/90"
            >
              <span aria-hidden="true">▶</span>
              {t('card.resume')}
            </button>
          )}
          {isPlanned && (
            <button
              type="button"
              className="flex-shrink-0 inline-flex items-center gap-1 rounded-md border border-entity-event/40 px-2.5 py-1.5 font-display text-[11px] font-bold text-entity-event transition-colors hover:bg-entity-event/5"
            >
              {t('card.launch')}
            </button>
          )}
          {!isInProgress && !isPlanned && <div />}

          {/* Chips */}
          <ChipStrip
            chips={[
              { entity: 'game', label: game?.title?.slice(0, 8) || 'gioco' },
              { entity: 'player', count: record.playerCount },
              { entity: 'chat', empty: true },
            ]}
            className="text-[8px]"
          />
        </div>
      </div>
    </button>
  );
}

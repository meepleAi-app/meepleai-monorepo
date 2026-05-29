/**
 * RecordCardGrid — Grid View Card
 *
 * AC-1.4: RecordCardGrid 3-col desktop, 1-col mobile; cover prominente,
 * OutcomeBadge top-right, no scoring inline.
 *
 * Issue #1488: Play Records Index Reskin (Task 1)
 */

'use client';

import { useRouter } from 'next/navigation';

import type { PlayRecordSummary } from '@/lib/api/schemas/play-records.schemas';
import { formatRelativeDate } from '@/lib/play-records/formatRelativeDate';
import { useSharedGames } from '@/lib/play-records/useSharedGames';
import { cn } from '@/lib/utils';

import { ChipStrip } from '../primitives/ChipStrip';
import { OutcomeBadge, type OutcomeBadgeVariant } from '../primitives/OutcomeBadge';

export interface RecordCardGridProps {
  record: PlayRecordSummary;
}

function formatDuration(duration: string | null): string {
  if (!duration) return '';
  const match = duration.match(/^(?:(\d+)\.)?(\d+):(\d+):(\d+)$/);
  if (match) {
    const hours = parseInt(match[2]) + (match[1] ? parseInt(match[1]) * 24 : 0);
    const minutes = parseInt(match[3]);
    const h = hours > 0 ? `${hours}h` : '';
    const m = minutes > 0 ? `${minutes}min` : '';
    return [h, m].filter(Boolean).join(' ') || '';
  }
  return duration;
}

export function RecordCardGrid({ record }: RecordCardGridProps) {
  const router = useRouter();
  const { data: games } = useSharedGames(record.gameId ? [record.gameId] : []);

  const game = record.gameId ? games?.[record.gameId] : null;
  const isPlanned = record.status === 'Planned';

  // Determine outcome badge variant
  const outcomeVariant: OutcomeBadgeVariant = isPlanned
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
        'group relative flex flex-col overflow-hidden rounded-lg border border-border bg-card transition-all hover:border-border-strong hover:shadow-md',
        'cursor-pointer text-left',
        isPlanned && 'opacity-82'
      )}
      data-testid={`record-card-grid-${record.id}`}
    >
      {/* Decorative top bar */}
      <div aria-hidden="true" className="h-0.5 bg-entity-session" />

      {/* Cover area */}
      <div className="relative flex h-24 items-center justify-center bg-muted sm:h-28">
        <span aria-hidden="true" className="text-4xl drop-shadow sm:text-5xl">
          {game?.coverEmoji || '🎯'}
        </span>

        {/* Entity label top-left */}
        <div className="absolute left-2 top-2 inline-flex items-center gap-1 rounded-full border border-entity-session/20 bg-white/85 px-2 py-1 backdrop-blur-sm font-mono text-[8px] font-extrabold uppercase tracking-wider text-entity-session">
          <span aria-hidden="true">🎯</span>
          Partita
        </div>

        {/* Outcome badge top-right */}
        <div className="absolute right-2 top-2">
          <OutcomeBadge variant={outcomeVariant} labels={badgeLabels} className="text-[9px]" />
        </div>
      </div>

      {/* Content */}
      <div className="flex flex-1 flex-col gap-2 px-3 py-2.5 sm:gap-3 sm:px-4 sm:py-3">
        {/* Title + metadata */}
        <div className="min-w-0">
          <h3 className="font-display text-sm font-extrabold text-foreground line-clamp-2">
            {game?.title || record.gameName}
          </h3>
          <div className="font-mono text-[10px] text-muted-foreground">
            {formatRelativeDate(record.sessionDate)} · ⏱ {formatDuration(record.duration)} · 👥
            {record.playerCount}
          </div>
        </div>

        {/* Spacer */}
        <div className="flex-1" />

        {/* Chips footer */}
        <div className="border-t border-border-light pt-2">
          <ChipStrip
            chips={[
              { entity: 'game', label: game?.title?.slice(0, 10) || 'gioco' },
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

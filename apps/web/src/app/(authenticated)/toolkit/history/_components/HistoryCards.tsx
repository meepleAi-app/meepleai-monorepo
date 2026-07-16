'use client';

import type { JSX } from 'react';

import { EntityChip } from '@/components/ui/entity-chip/entity-chip';
import { useTranslation } from '@/hooks/useTranslation';
import { cn } from '@/lib/utils';

import { getRelativeTimeParts } from '../_lib/relative-time';

import type { HistoryRow } from '../_lib/history-filters';

export interface HistoryCardsProps {
  rows: HistoryRow[];
  onOpenDetail: (row: HistoryRow) => void;
}

/** Max number of player avatars shown before collapsing the rest into a "+N" pill. */
const MAX_AVATARS = 3;

/** Formats a session duration in minutes as `"{h}h {m}m"`. */
function formatDuration(minutes: number): string {
  const h = Math.floor(minutes / 60);
  const m = minutes % 60;
  return `${h}h ${m}m`;
}

/** Derives up to 2 uppercase initials from a player's display name. */
function getInitials(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return '?';
  if (parts.length === 1) return (parts[0] as string).slice(0, 2).toUpperCase();
  const first = parts[0] as string;
  const last = parts[parts.length - 1] as string;
  return `${first[0]}${last[0]}`.toUpperCase();
}

interface HistoryCardProps {
  row: HistoryRow;
  onOpenDetail: (row: HistoryRow) => void;
}

/**
 * A single session card matching the mockup's `SessionCard` structure:
 * `chead` (game chip + relative date), `cmid` (avatar stack + winner cell +
 * duration pill), `cfoot` (score pill + note flag + absolute date). The left
 * border is highlighted when the session has a winner (a "win").
 */
function HistoryCard({ row, onOpenDetail }: HistoryCardProps): JSX.Element {
  const { t, formatDate, formatRelativeTime } = useTranslation();
  const now = new Date();
  const { value: relValue, unit: relUnit } = getRelativeTimeParts(row.startedAt, now);

  const isWin = !row.isCoop && row.winnerName != null;

  const handleOpen = () => onOpenDetail(row);

  return (
    <div
      role="button"
      tabIndex={0}
      aria-label={`${row.gameName} — ${t('pages.toolkitHistory.table.viewDetails')}`}
      className={cn(
        'flex cursor-pointer flex-col gap-3 rounded-lg border border-border bg-card p-4 shadow-sm transition-shadow hover:shadow-md',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
        isWin && 'border-l-4 border-l-primary'
      )}
      onClick={handleOpen}
      onKeyDown={e => {
        if (e.key === 'Enter') {
          e.preventDefault();
          handleOpen();
        }
      }}
    >
      {/* chead */}
      <div className="flex items-center gap-2">
        <EntityChip entity="game" label={row.gameName} />
        <span className="flex-1" />
        <span className="text-xs text-muted-foreground">
          {formatRelativeTime(relValue, relUnit)}
        </span>
      </div>

      {/* cmid */}
      <div className="flex flex-wrap items-center gap-2">
        <span className="sr-only">{t('pages.toolkitHistory.table.players')}</span>
        <span
          role="img"
          aria-label={t('pages.toolkitHistory.table.playersAria', {
            count: row.playerCount,
            names: row.playerNames.join(', '),
          })}
          className="inline-flex items-center -space-x-2"
        >
          {row.playerNames.slice(0, MAX_AVATARS).map((name, i) => (
            <span
              key={`${row.id}-avatar-${i}`}
              aria-hidden="true"
              className="inline-flex h-7 w-7 items-center justify-center rounded-full border border-border bg-muted text-xs font-semibold text-foreground"
            >
              {getInitials(name)}
            </span>
          ))}
          {row.playerCount > MAX_AVATARS && (
            <span
              aria-hidden="true"
              className="inline-flex h-7 w-7 items-center justify-center rounded-full border border-border bg-muted text-xs font-semibold text-muted-foreground"
            >
              +{row.playerCount - MAX_AVATARS}
            </span>
          )}
        </span>

        <span className="sr-only">{t('pages.toolkitHistory.table.winner')}</span>
        {row.isCoop ? (
          <span className="inline-flex items-center gap-1 rounded-full bg-muted px-2 py-0.5 text-xs font-medium text-muted-foreground">
            <span aria-hidden="true">🤝</span>
            {t('pages.toolkitHistory.table.coop')}
          </span>
        ) : row.winnerName == null ? (
          <span
            className="text-xs text-muted-foreground"
            aria-label={t('pages.toolkitHistory.table.noWinner')}
          >
            —
          </span>
        ) : (
          <span
            className="inline-flex items-center gap-1 rounded-full bg-primary px-2 py-0.5 text-xs font-medium text-primary-foreground"
            title={t('pages.toolkitHistory.table.winnerAria', { name: row.winnerName })}
          >
            <span aria-hidden="true">🏆</span>
            {row.winnerName}
          </span>
        )}

        <span className="ml-auto inline-flex items-center gap-1 rounded-full bg-muted px-2 py-0.5 text-xs font-medium text-muted-foreground">
          <span aria-hidden="true">⏱</span>
          {formatDuration(row.durationMinutes)}
        </span>
      </div>

      {/* cfoot */}
      <div className="flex items-center gap-2 border-t border-border pt-3">
        {row.isCoop ? (
          <span className="inline-flex items-center gap-1 rounded-md bg-muted px-2 py-1 text-xs font-semibold text-muted-foreground">
            <span aria-hidden="true">🤝</span>
            {t('pages.toolkitHistory.cards.coop')}
          </span>
        ) : (
          <span className="inline-flex items-center gap-1 rounded-md bg-primary/10 px-2 py-1 text-xs font-semibold text-primary">
            <span aria-hidden="true">🏆</span>
            {row.winScore ?? '—'}
          </span>
        )}
        <span className="flex-1" />
        {row.notes && (
          <span aria-label={t('pages.toolkitHistory.table.hasNote')} title={row.notes}>
            <span aria-hidden="true">📝</span>
          </span>
        )}
        <span className="text-xs text-muted-foreground">{formatDate(new Date(row.startedAt))}</span>
      </div>
    </div>
  );
}

/**
 * Card stack for /toolkit/history (Issue #3006, Task A5) — used for the
 * "cards" view toggle on desktop and as the sole view on mobile.
 *
 * Matches `admin-mockups/design_files/sp4-toolkit-history-ui.jsx`'s
 * `Cards`/`SessionCard` components. Shares the relative-time bucketing
 * helper (`_lib/relative-time.ts`) with `HistoryTable` so both views render
 * identical relative-date labels.
 */
export function HistoryCards({ rows, onOpenDetail }: HistoryCardsProps): JSX.Element {
  return (
    <div className="flex flex-col gap-3">
      {rows.map(row => (
        <HistoryCard key={row.id} row={row} onOpenDetail={onOpenDetail} />
      ))}
    </div>
  );
}

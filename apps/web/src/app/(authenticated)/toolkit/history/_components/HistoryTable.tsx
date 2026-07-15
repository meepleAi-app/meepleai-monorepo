'use client';

import type { JSX } from 'react';

import { ArrowDown, ArrowUp, ArrowUpDown } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/data-display/table';
import { EntityChip } from '@/components/ui/entity-chip/entity-chip';
import { Button } from '@/components/ui/primitives/button';
import { useTranslation } from '@/hooks/useTranslation';
import { cn } from '@/lib/utils';

import type { HistoryRow, HistorySort } from '../_lib/history-filters';

export interface HistoryTableProps {
  rows: HistoryRow[];
  sort: HistorySort;
  onSortChange: (sort: HistorySort) => void;
  onOpenDetail: (row: HistoryRow) => void;
  onOpenGameStats: (gameId: string) => void;
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

/** Buckets an ISO timestamp relative to `now` into an `Intl.RelativeTimeFormat` value/unit pair. */
function getRelativeTimeParts(
  startedAt: string,
  now: Date
): { value: number; unit: Intl.RelativeTimeFormatUnit } {
  const diffMinutes = (new Date(startedAt).getTime() - now.getTime()) / 60_000;
  if (Math.abs(diffMinutes) < 60) return { value: Math.round(diffMinutes), unit: 'minute' };

  const diffHours = diffMinutes / 60;
  if (Math.abs(diffHours) < 24) return { value: Math.round(diffHours), unit: 'hour' };

  const diffDays = diffHours / 24;
  if (Math.abs(diffDays) < 30) return { value: Math.round(diffDays), unit: 'day' };

  const diffMonths = diffDays / 30;
  if (Math.abs(diffMonths) < 12) return { value: Math.round(diffMonths), unit: 'month' };

  return { value: Math.round(diffMonths / 12), unit: 'year' };
}

interface SortableTableHeadProps {
  label: string;
  active: boolean;
  ariaSort: 'ascending' | 'descending' | 'none';
  onSort: () => void;
  className?: string;
}

/** A `<TableHead>` whose label is a click/keyboard-activatable sort trigger, with an `aria-sort` state. */
function SortableTableHead({
  label,
  active,
  ariaSort,
  onSort,
  className,
}: SortableTableHeadProps): JSX.Element {
  const Icon = !active ? ArrowUpDown : ariaSort === 'ascending' ? ArrowUp : ArrowDown;
  return (
    <TableHead aria-sort={ariaSort} className={cn('whitespace-nowrap', className)}>
      <button
        type="button"
        onClick={onSort}
        className="inline-flex items-center gap-1 rounded font-medium text-muted-foreground hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
      >
        {label}
        <Icon
          className={cn('h-3.5 w-3.5', active ? 'text-foreground' : 'text-muted-foreground')}
          aria-hidden="true"
        />
      </button>
    </TableHead>
  );
}

/**
 * Desktop 8-column sortable history table for /toolkit/history (Issue #3006, Task A4).
 *
 * Columns: Data, Gioco, Durata, Giocatori, Vincitore, Score, Note, Azioni — matching
 * `admin-mockups/design_files/sp4-toolkit-history-ui.jsx`'s `Table`/`Row` components.
 *
 * Only Data/Durata/Score are sortable via header click — `HistorySort` has no 'game'
 * value, so the Gioco column has no header-driven sort (the primary sort control is
 * the toolbar dropdown built in a later task).
 */
export function HistoryTable({
  rows,
  sort,
  onSortChange,
  onOpenDetail,
  onOpenGameStats,
}: HistoryTableProps): JSX.Element {
  const { t, formatDate, formatTime, formatRelativeTime } = useTranslation();
  const now = new Date();

  const isDateActive = sort === 'recent' || sort === 'oldest';
  const dateAriaSort = sort === 'recent' ? 'descending' : sort === 'oldest' ? 'ascending' : 'none';

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <SortableTableHead
            label={t('pages.toolkitHistory.table.date')}
            active={isDateActive}
            ariaSort={dateAriaSort}
            onSort={() => onSortChange(sort === 'recent' ? 'oldest' : 'recent')}
          />
          <TableHead>{t('pages.toolkitHistory.table.game')}</TableHead>
          <SortableTableHead
            label={t('pages.toolkitHistory.table.duration')}
            active={sort === 'longest'}
            ariaSort={sort === 'longest' ? 'descending' : 'none'}
            onSort={() => onSortChange('longest')}
          />
          <TableHead>{t('pages.toolkitHistory.table.players')}</TableHead>
          <TableHead>{t('pages.toolkitHistory.table.winner')}</TableHead>
          <SortableTableHead
            label={t('pages.toolkitHistory.table.score')}
            active={sort === 'score'}
            ariaSort={sort === 'score' ? 'descending' : 'none'}
            onSort={() => onSortChange('score')}
          />
          <TableHead className="text-center">{t('pages.toolkitHistory.table.notes')}</TableHead>
          <TableHead className="text-right">{t('pages.toolkitHistory.table.actions')}</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {rows.map(row => {
          const { value: relValue, unit: relUnit } = getRelativeTimeParts(row.startedAt, now);
          const startedAtDate = new Date(row.startedAt);

          return (
            <TableRow
              key={row.id}
              tabIndex={0}
              className="cursor-pointer"
              onClick={() => onOpenDetail(row)}
              onKeyDown={e => {
                if (e.key === 'Enter') {
                  e.preventDefault();
                  onOpenDetail(row);
                }
              }}
            >
              {/* Data */}
              <TableCell>
                <div className="text-foreground">
                  {formatDate(startedAtDate)} · {formatTime(startedAtDate)}
                </div>
                <div className="text-xs text-muted-foreground">
                  {formatRelativeTime(relValue, relUnit)}
                </div>
              </TableCell>

              {/* Gioco */}
              <TableCell>
                <EntityChip entity="game" label={row.gameName} />
              </TableCell>

              {/* Durata */}
              <TableCell>{formatDuration(row.durationMinutes)}</TableCell>

              {/* Giocatori */}
              <TableCell>
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
              </TableCell>

              {/* Vincitore */}
              <TableCell>
                {row.isCoop ? (
                  <Badge variant="secondary" className="gap-1">
                    <span aria-hidden="true">🤝</span>
                    {t('pages.toolkitHistory.table.coop')}
                  </Badge>
                ) : row.winnerName == null ? (
                  <span
                    className="text-muted-foreground"
                    aria-label={t('pages.toolkitHistory.table.noWinner')}
                  >
                    —
                  </span>
                ) : (
                  <Badge
                    variant="default"
                    className="gap-1"
                    title={t('pages.toolkitHistory.table.winnerAria', { name: row.winnerName })}
                  >
                    <span aria-hidden="true">🏆</span>
                    {row.winnerName}
                  </Badge>
                )}
              </TableCell>

              {/* Score */}
              <TableCell>
                {row.isCoop ? (
                  <span className="text-muted-foreground">
                    {t('pages.toolkitHistory.table.coopShort')}
                  </span>
                ) : (
                  <span>{row.winScore ?? '—'}</span>
                )}
              </TableCell>

              {/* Note */}
              <TableCell className="text-center">
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  className={cn('px-2', !row.notes && 'opacity-40')}
                  disabled={!row.notes}
                  title={row.notes ?? undefined}
                  aria-label={
                    row.notes
                      ? t('pages.toolkitHistory.table.hasNote')
                      : t('pages.toolkitHistory.table.noNote')
                  }
                  onClick={e => {
                    e.stopPropagation();
                    if (row.notes) onOpenDetail(row);
                  }}
                >
                  <span aria-hidden="true">📝</span>
                </Button>
              </TableCell>

              {/* Azioni */}
              <TableCell className="text-right">
                <div className="flex justify-end gap-1">
                  <Button
                    type="button"
                    variant="ghost"
                    size="sm"
                    className="px-2"
                    aria-label={t('pages.toolkitHistory.table.viewDetails')}
                    onClick={e => {
                      e.stopPropagation();
                      onOpenDetail(row);
                    }}
                  >
                    <span aria-hidden="true">👁</span>
                  </Button>
                  <Button
                    type="button"
                    variant="ghost"
                    size="sm"
                    className="px-2"
                    aria-label={t('pages.toolkitHistory.table.gameStats')}
                    onClick={e => {
                      e.stopPropagation();
                      onOpenGameStats(row.gameId);
                    }}
                  >
                    <span aria-hidden="true">📊</span>
                  </Button>
                </div>
              </TableCell>
            </TableRow>
          );
        })}
      </TableBody>
    </Table>
  );
}

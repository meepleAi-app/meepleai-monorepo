'use client';

import type { JSX } from 'react';

import {
  Drawer,
  DrawerClose,
  DrawerContent,
  DrawerDescription,
  DrawerHeader,
  DrawerTitle,
} from '@/components/ui/drawer/drawer';
import { useTranslation } from '@/hooks/useTranslation';
import { cn } from '@/lib/utils';

import type { HistoryRow } from '../_lib/history-filters';

export interface HistoryDetailModalProps {
  row: HistoryRow | null;
  onClose: () => void;
}

/** Formats a session duration in minutes as `"{h}h {m}m"` — mirrors HistoryTable/HistoryCards. */
function formatDuration(minutes: number): string {
  const h = Math.floor(minutes / 60);
  const m = minutes % 60;
  return `${h}h ${m}m`;
}

/** Derives up to 2 uppercase initials from a player's display name — mirrors HistoryTable/HistoryCards. */
function getInitials(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return '?';
  if (parts.length === 1) return (parts[0] as string).slice(0, 2).toUpperCase();
  const first = parts[0] as string;
  const last = parts[parts.length - 1] as string;
  return `${first[0]}${last[0]}`.toUpperCase();
}

/**
 * Session detail overlay for /toolkit/history (Issue #3006, Task A8) — a desktop
 * side panel / mobile bottom sheet via the shared `Drawer` primitive (`side="auto"`
 * picks the mode from the viewport breakpoint).
 *
 * Matches `admin-mockups/design_files/sp4-toolkit-history-ui.jsx`'s `DetailModal`,
 * degraded to the fields the real `HistoryRow` view-model actually carries:
 *  - **Classifica finale**: player list from `playerNames`, winner marked with 🏆
 *    (`playerName === winnerName`). Per-player scores are NOT derivable — the API
 *    payload has no name→score correlation once mapped to `HistoryRow` — so every
 *    score slot renders `modal.noData` rather than inventing a number.
 *  - **Statistiche partita**: turns/variance/highlights have no backing data at all
 *    → `modal.noData`. Only "Score vincitore" has real data (`row.winScore`).
 *  - **Timeline eventi**: omitted entirely — the API exposes no session event log.
 *  - **Note**: readonly display of `row.notes`, or `modal.notesEmpty` when absent.
 *
 * No footer actions are rendered: the mockup's stats/replay/edit-notes/delete
 * buttons had no backing command, so they were removed as dead affordances
 * (Issue #3010) rather than shipped unwired.
 */
export function HistoryDetailModal({ row, onClose }: HistoryDetailModalProps): JSX.Element | null {
  const { t, formatDate, formatTime } = useTranslation();

  if (row == null) return null;

  const startedAtDate = new Date(row.startedAt);
  const noData = t('pages.toolkitHistory.modal.noData');

  return (
    <Drawer
      open
      onOpenChange={next => {
        if (!next) onClose();
      }}
      side="auto"
      entity="session"
    >
      <DrawerContent>
        <DrawerHeader>
          <div className="flex items-start justify-between gap-2">
            <div className="flex flex-col gap-1">
              <DrawerTitle>
                {t('pages.toolkitHistory.modal.title', {
                  game: row.gameName,
                  date: formatDate(startedAtDate),
                })}
              </DrawerTitle>
              <DrawerDescription>
                {t('pages.toolkitHistory.modal.sub', {
                  duration: formatDuration(row.durationMinutes),
                  count: row.playerCount,
                  time: formatTime(startedAtDate),
                })}
              </DrawerDescription>
            </div>
            <DrawerClose aria-label={t('pages.toolkitHistory.modal.close')} className="shrink-0">
              <span aria-hidden="true">✕</span>
            </DrawerClose>
          </div>
        </DrawerHeader>

        <div className="flex min-h-0 flex-1 flex-col gap-6 overflow-y-auto p-4">
          {/* Classifica finale */}
          <section>
            <h4 className="mb-2 text-sm font-semibold text-foreground">
              {t('pages.toolkitHistory.modal.leaderboard')}
            </h4>
            <ul className="flex flex-col gap-2">
              {row.playerNames.map((name, i) => {
                const isWinner = !row.isCoop && row.winnerName != null && name === row.winnerName;
                return (
                  <li
                    key={`${row.id}-player-${i}`}
                    className={cn(
                      'flex items-center justify-between rounded-md border border-border px-3 py-2',
                      isWinner && 'border-primary/40 bg-primary/5'
                    )}
                  >
                    <span className="flex items-center gap-2">
                      <span
                        aria-hidden="true"
                        className="inline-flex h-7 w-7 items-center justify-center rounded-full border border-border bg-muted text-xs font-semibold text-foreground"
                      >
                        {getInitials(name)}
                      </span>
                      <span className="text-sm text-foreground">{name}</span>
                    </span>
                    <span className="flex items-center gap-1 text-sm text-muted-foreground">
                      {noData}
                      {isWinner && <span aria-hidden="true">🏆</span>}
                    </span>
                  </li>
                );
              })}
            </ul>
          </section>

          {/* Note */}
          <section>
            <h4 className="mb-2 text-sm font-semibold text-foreground">
              {t('pages.toolkitHistory.modal.notes')}
            </h4>
            {row.notes ? (
              <p className="whitespace-pre-wrap rounded-md border border-border bg-muted/30 p-3 text-sm text-foreground">
                {row.notes}
              </p>
            ) : (
              <p className="text-sm text-muted-foreground">
                {t('pages.toolkitHistory.modal.notesEmpty')}
              </p>
            )}
          </section>

          {/* Statistiche partita */}
          <section>
            <h4 className="mb-2 text-sm font-semibold text-foreground">
              {t('pages.toolkitHistory.modal.stats')}
            </h4>
            <div className="grid grid-cols-2 gap-3">
              <div
                className="rounded-md border border-border p-3"
                data-testid="history-detail-modal-stat-turns"
              >
                <div className="text-xs text-muted-foreground">
                  {t('pages.toolkitHistory.modal.turns')}
                </div>
                <div className="text-sm font-semibold text-foreground">{noData}</div>
              </div>
              <div
                className="rounded-md border border-border p-3"
                data-testid="history-detail-modal-stat-winner-score"
              >
                <div className="text-xs text-muted-foreground">
                  {t('pages.toolkitHistory.modal.winnerScore')}
                </div>
                <div className="text-sm font-semibold text-foreground">
                  {row.winScore ?? noData}
                </div>
              </div>
              <div
                className="rounded-md border border-border p-3"
                data-testid="history-detail-modal-stat-variance"
              >
                <div className="text-xs text-muted-foreground">
                  {t('pages.toolkitHistory.modal.variance')}
                </div>
                <div className="text-sm font-semibold text-foreground">{noData}</div>
              </div>
              <div
                className="rounded-md border border-border p-3"
                data-testid="history-detail-modal-stat-highlights"
              >
                <div className="text-xs text-muted-foreground">
                  {t('pages.toolkitHistory.modal.highlights')}
                </div>
                <div className="text-sm font-semibold text-foreground">{noData}</div>
              </div>
            </div>
          </section>
        </div>
      </DrawerContent>
    </Drawer>
  );
}

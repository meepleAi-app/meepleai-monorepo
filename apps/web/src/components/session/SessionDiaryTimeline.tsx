/**
 * SessionDiaryTimeline — Append-only event timeline for a session
 *
 * Renders diary entries (from useContextualHandStore or direct API call)
 * as a vertical timeline with event-type icons and timestamps.
 *
 * Plan 2 Task 4 — Session Flow v2.1
 */

'use client';

import { useEffect, useMemo } from 'react';

import { cn } from '@/lib/utils';
import { useContextualHandStore, selectDiaryEntries, selectIsDiaryLoading } from '@/stores/contextual-hand';

import { getEventMeta, parseSummary } from './diary-utils';

// ─── Component ────────────────────────────────────────────────────────────

interface SessionDiaryTimelineProps {
  sessionId: string;
  /** Maximum number of entries to show (default: 30). */
  limit?: number;
}

export function SessionDiaryTimeline({ sessionId, limit = 30 }: SessionDiaryTimelineProps) {
  const diaryEntries = useContextualHandStore(selectDiaryEntries);
  const isDiaryLoading = useContextualHandStore(selectIsDiaryLoading);
  const loadDiary = useContextualHandStore(s => s.loadDiary);

  // Load diary on mount if store has a matching session
  useEffect(() => {
    const currentSession = useContextualHandStore.getState().currentSession;
    if (currentSession?.sessionId === sessionId) {
      loadDiary();
    }
  }, [sessionId, loadDiary]);

  // Sort chronologically descending and limit
  const entries = useMemo(() => {
    return [...diaryEntries]
      .sort((a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime())
      .slice(0, limit);
  }, [diaryEntries, limit]);

  if (isDiaryLoading && entries.length === 0) {
    return (
      <div className="flex items-center justify-center py-8">
        <div className="h-5 w-5 animate-spin rounded-full border-2 border-muted-foreground border-t-transparent" />
      </div>
    );
  }

  if (entries.length === 0) {
    return (
      <div className="rounded-lg border border-border bg-muted/30 p-4 text-center">
        <p className="text-sm text-muted-foreground">
          Nessun evento registrato — le attività appariranno qui
        </p>
      </div>
    );
  }

  return (
    <section className="space-y-2" data-testid="session-diary-timeline">
      <h2 className="text-sm font-semibold text-muted-foreground uppercase tracking-wider">
        Diario sessione
      </h2>
      <div className="relative space-y-0">
        {/* Vertical line */}
        <div className="absolute left-[15px] top-2 bottom-2 w-px bg-border" />

        {entries.map(entry => {
          const meta = getEventMeta(entry.eventType);
          const Icon = meta.icon;
          const time = new Date(entry.timestamp).toLocaleTimeString('it-IT', {
            hour: '2-digit',
            minute: '2-digit',
            second: '2-digit',
          });

          // Try to extract meaningful payload summary
          const summary = parseSummary(entry.eventType, entry.payload);

          return (
            <div
              key={entry.id}
              className="relative flex items-start gap-3 py-2 pl-0"
              data-testid="diary-entry"
            >
              {/* Icon circle */}
              <div
                className={cn(
                  'relative z-10 flex h-[30px] w-[30px] shrink-0 items-center justify-center rounded-full border bg-background',
                  meta.color
                )}
              >
                <Icon className="h-3.5 w-3.5" />
              </div>

              {/* Content */}
              <div className="flex-1 min-w-0 pt-0.5">
                <div className="flex items-baseline gap-2">
                  <span className="text-sm font-medium text-foreground">{meta.label}</span>
                  <span className="text-[10px] text-muted-foreground tabular-nums">{time}</span>
                </div>
                {summary && (
                  <p className="text-xs text-muted-foreground mt-0.5 truncate">{summary}</p>
                )}
              </div>
            </div>
          );
        })}
      </div>
    </section>
  );
}

export type { SessionDiaryTimelineProps };

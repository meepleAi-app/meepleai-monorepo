/**
 * ActionLogTimeline — Wave D.2 Foundation sub-PR (Issue #746).
 *
 * Append-only timeline of session events (score updates, tool usage, chat, etc.).
 * Foundation: static entries from fixture. Interactions sub-PR prepends SSE events.
 *
 * Gate C: DIVERGES from MeepleCard — real-time event log, not a card.
 */

import type { ReactElement } from 'react';

import type { Citation } from '@/lib/api/schemas/streaming.schemas';

// ─── Types ────────────────────────────────────────────────────────────────────

export interface ActionLogEntry {
  readonly id: string;
  readonly type: 'score' | 'tool' | 'agent' | 'chat' | 'photo' | 'event';
  readonly authorName: string;
  readonly content: string;
  readonly timestamp: string;
  /**
   * RAG citations attached to a broadcast chat entry (#2564 AC-SSE-4). Rendered read-only
   * under the content so non-author participants see the shared citations too.
   */
  readonly citations?: ReadonlyArray<Citation>;
}

// ─── Labels ───────────────────────────────────────────────────────────────────

export interface ActionLogTimelineLabels {
  readonly title: string;
  readonly emptyLabel: string;
  readonly typeScore: string;
  readonly typeTool: string;
  readonly typeAgent: string;
  readonly typeChat: string;
  readonly typePhoto: string;
  readonly typeEvent: string;
  readonly timestampAriaLabel: string;
}

// ─── Props ────────────────────────────────────────────────────────────────────

export interface ActionLogTimelineProps {
  readonly entries: ReadonlyArray<ActionLogEntry>;
  readonly compact?: boolean;
  readonly labels: ActionLogTimelineLabels;
}

// ─── Helpers ─────────────────────────────────────────────────────────────────

function typeLabel(type: ActionLogEntry['type'], labels: ActionLogTimelineLabels): string {
  const map: Record<ActionLogEntry['type'], string> = {
    score: labels.typeScore,
    tool: labels.typeTool,
    agent: labels.typeAgent,
    chat: labels.typeChat,
    photo: labels.typePhoto,
    event: labels.typeEvent,
  };
  return map[type];
}

function typeColor(type: ActionLogEntry['type']): string {
  const map: Record<ActionLogEntry['type'], string> = {
    score: 'text-emerald-400',
    // eslint-disable-next-line meepleai/no-inline-hsl-v2 -- TODO #807-followup: session hue in Tailwind arbitrary class; no `text-entity-session/light` token exists for dark-bg variant
    tool: 'text-[hsl(240,60%,75%)]',
    agent: 'text-amber-400',
    chat: 'text-sky-400',
    photo: 'text-rose-400',
    event: 'text-muted-foreground',
  };
  return map[type];
}

function formatTime(iso: string): string {
  try {
    return new Date(iso).toLocaleTimeString('it-IT', {
      hour: '2-digit',
      minute: '2-digit',
    });
  } catch {
    return '';
  }
}

// ─── Component ────────────────────────────────────────────────────────────────

export function ActionLogTimeline({
  entries,
  compact = false,
  labels,
}: ActionLogTimelineProps): ReactElement {
  return (
    <div data-slot="action-log-timeline" className="flex flex-col gap-2">
      {!compact && <h3 className="text-sm font-semibold text-foreground">{labels.title}</h3>}

      {entries.length === 0 ? (
        <p className="text-xs text-muted-foreground">{labels.emptyLabel}</p>
      ) : (
        <ol aria-label={labels.timestampAriaLabel} className="flex flex-col gap-1" reversed>
          {entries.map(entry => (
            <li
              key={entry.id}
              data-slot="action-log-entry"
              data-entry-id={entry.id}
              data-entry-type={entry.type}
              className="flex items-start gap-2 rounded-lg px-2 py-1.5 bg-card"
            >
              {/* Type badge */}
              <span className={['shrink-0 text-xs font-medium', typeColor(entry.type)].join(' ')}>
                {typeLabel(entry.type, labels)}
              </span>

              {/* Content */}
              <div className="min-w-0 flex-1">
                <p className="truncate text-xs text-foreground">{entry.content}</p>
                <p className="text-xs text-muted-foreground">{entry.authorName}</p>

                {/* #2564 AC-SSE-4: broadcast RAG citations rendered read-only so non-author
                    participants see the shared citations, not just the agent's text. */}
                {entry.citations && entry.citations.length > 0 && (
                  <ul data-slot="action-log-citations" className="mt-1 flex flex-wrap gap-1">
                    {entry.citations.map((citation, index) => (
                      <li
                        key={`${entry.id}-cite-${index}`}
                        data-slot="action-log-citation"
                        title={citation.snippet ?? citation.text ?? undefined}
                        className="inline-flex max-w-[10rem] items-center gap-1 rounded-full bg-muted px-2 py-0.5 font-mono text-[0.65rem] font-medium text-sky-400"
                      >
                        <span aria-hidden="true">📖</span>
                        {typeof citation.page === 'number' && <span>p. {citation.page}</span>}
                        {citation.source ? (
                          <span className="truncate">{citation.source}</span>
                        ) : null}
                      </li>
                    ))}
                  </ul>
                )}
              </div>

              {/* Timestamp */}
              <time
                dateTime={entry.timestamp}
                className="shrink-0 tabular-nums text-xs text-muted-foreground"
              >
                {formatTime(entry.timestamp)}
              </time>
            </li>
          ))}
        </ol>
      )}
    </div>
  );
}

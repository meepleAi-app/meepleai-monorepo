/**
 * ActionLogTimeline — Wave D.2 Foundation sub-PR (Issue #746).
 *
 * Append-only timeline of session events (score updates, tool usage, chat, etc.).
 * Foundation: static entries from fixture. Interactions sub-PR prepends SSE events.
 *
 * Gate C: DIVERGES from MeepleCard — real-time event log, not a card.
 */

import type { ReactElement } from 'react';

// ─── Types ────────────────────────────────────────────────────────────────────

export interface ActionLogEntry {
  readonly id: string;
  readonly type: 'score' | 'tool' | 'agent' | 'chat' | 'photo' | 'event';
  readonly authorName: string;
  readonly content: string;
  readonly timestamp: string;
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
    tool: 'text-[hsl(240,60%,70%)]',
    agent: 'text-amber-400',
    chat: 'text-sky-400',
    photo: 'text-rose-400',
    event: 'text-slate-400',
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
      {!compact && <h3 className="text-sm font-semibold text-slate-200">{labels.title}</h3>}

      {entries.length === 0 ? (
        <p className="text-xs text-slate-500">{labels.emptyLabel}</p>
      ) : (
        <ol aria-label={labels.timestampAriaLabel} className="flex flex-col gap-1" reversed>
          {entries.map(entry => (
            <li
              key={entry.id}
              data-slot="action-log-entry"
              data-entry-id={entry.id}
              data-entry-type={entry.type}
              className="flex items-start gap-2 rounded-lg px-2 py-1.5 bg-slate-800/40"
            >
              {/* Type badge */}
              <span className={['shrink-0 text-xs font-medium', typeColor(entry.type)].join(' ')}>
                {typeLabel(entry.type, labels)}
              </span>

              {/* Content */}
              <div className="min-w-0 flex-1">
                <p className="truncate text-xs text-slate-200">{entry.content}</p>
                <p className="text-xs text-slate-500">{entry.authorName}</p>
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

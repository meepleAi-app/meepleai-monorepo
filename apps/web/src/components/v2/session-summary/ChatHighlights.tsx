/**
 * ChatHighlights — Wave D.3 v2 component (Issue #756).
 *
 * Curated chat snippets surfaced from the agent + user dialog during the
 * session. Each highlight is a small bubble (user-aligned right, agent-
 * aligned left) showing message + timestamp + optional citations.
 *
 * Mockup mapping:
 *   - admin-mockups/design_files/sp4-session-summary.jsx (ChatHighlights)
 *
 * Contract reference: docs/frontend/contracts/sessions-id-summary-hooks.md §5.9.
 *
 * Schema reality v1 carryover (Gate B): no backend `/chat-highlights`
 * endpoint exists in v1. The `ChatHighlight` interface is a frontend-only
 * stub (defined inline here per task brief — promoted to schemas.ts when
 * backend lands). Orchestrator sources from fixture only in v1.
 *
 * MeepleCard divergence (Gate C): chat bubble layout (alternating align,
 * no avatar/title/subtitle). MeepleCard cannot represent dialog turns.
 * DIVERGE.
 *
 * A11y:
 *   - `<ul role="list">` (list semantics for screen readers).
 *   - Each highlight `<li>` carries `data-author={authorId}`.
 *   - Empty state: `<div role="status">` with placeholder copy.
 *
 * Pure component: orchestrator resolves all i18n strings.
 */

import type { ReactElement } from 'react';

import clsx from 'clsx';

/**
 * Stub chat highlight schema (Gate B v1 — no backend).
 *
 * `authorId` distinguishes user vs agent vs other participants. The orchestrator
 * is responsible for marking which side aligns based on `authorId === currentUserId`.
 * For v1 (stub fixture), the convention is `authorId === 'agent'` for agent-side.
 */
export interface ChatHighlight {
  readonly id: string;
  readonly authorId: string;
  readonly authorName: string;
  readonly message: string;
  readonly timestamp: string;
}

export interface ChatHighlightsLabels {
  readonly title: string;
  readonly emptyTitle: string;
  readonly emptyDescription: string;
}

export interface ChatHighlightsProps {
  readonly highlights: readonly ChatHighlight[];
  /**
   * Function to identify "agent" highlights (left-aligned). Defaults to
   * `authorId === 'agent'`. Orchestrator may override if the highlight
   * shape later includes a richer `kind` discriminator.
   */
  readonly isAgent?: (h: ChatHighlight) => boolean;
  readonly labels: ChatHighlightsLabels;
  readonly className?: string;
}

const DEFAULT_IS_AGENT = (h: ChatHighlight): boolean => h.authorId === 'agent';

export function ChatHighlights({
  highlights,
  isAgent = DEFAULT_IS_AGENT,
  labels,
  className,
}: ChatHighlightsProps): ReactElement {
  const isEmpty = highlights.length === 0;

  if (isEmpty) {
    return (
      <section
        data-slot="chat-highlights"
        data-empty="true"
        className={clsx(
          'flex flex-col items-center gap-2 rounded-lg border border-dashed border-border bg-card px-4 py-8 text-center',
          className
        )}
        role="status"
      >
        <span aria-hidden="true" className="text-3xl">
          💬
        </span>
        <h3 className="font-display text-sm font-extrabold text-foreground">{labels.emptyTitle}</h3>
        <p className="text-xs text-muted-foreground">{labels.emptyDescription}</p>
      </section>
    );
  }

  return (
    <section
      data-slot="chat-highlights"
      data-empty={undefined}
      className={clsx('flex flex-col gap-2', className)}
    >
      <h3 className="font-display text-base font-extrabold text-foreground">
        <span aria-hidden="true" className="mr-1.5">
          💬
        </span>
        {labels.title}
      </h3>
      <ul role="list" className="flex flex-col gap-2 rounded-lg border border-border bg-card p-3">
        {highlights.map(h => {
          const agent = isAgent(h);
          return (
            <li
              key={h.id}
              role="listitem"
              data-slot="chat-highlight-item"
              data-author={h.authorId}
              data-agent={agent || undefined}
              className={clsx(
                'flex max-w-[88%] flex-col gap-0.5',
                agent ? 'self-start' : 'self-end'
              )}
            >
              <div
                className={clsx(
                  'rounded-2xl border px-3 py-1.5 text-sm leading-snug text-foreground',
                  agent
                    ? 'border-[hsla(38,92%,33%,0.25)] bg-[hsla(38,92%,33%,0.1)] rounded-tl-sm'
                    : 'border-[hsla(220,80%,55%,0.25)] bg-[hsla(220,80%,55%,0.12)] rounded-tr-sm'
                )}
              >
                {h.message}
              </div>
              <div
                className={clsx(
                  'flex items-center gap-1.5 text-[10px] text-muted-foreground',
                  agent ? 'pl-1' : 'self-end pr-1'
                )}
              >
                <span className="font-mono font-bold">{h.authorName}</span>
                <span aria-hidden="true">·</span>
                <span className="font-mono">
                  {new Date(h.timestamp).toLocaleTimeString(undefined, {
                    hour: '2-digit',
                    minute: '2-digit',
                  })}
                </span>
              </div>
            </li>
          );
        })}
      </ul>
    </section>
  );
}

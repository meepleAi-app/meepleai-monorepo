/**
 * ChatHistoryTimeline - v2 Wave C.2 (Issue #581)
 *
 * Mapped from `admin-mockups/design_files/sp4-agent-detail.jsx` (ChatTimelineItem).
 * Spec: docs/superpowers/specs/2026-04-26-v2-design-migration.md (Phase 1+2)
 * Tracking: docs/frontend/v2-migration-matrix.md (Issue #581)
 *
 * Pure presentational component — no hooks, no i18n calls, no data fetching.
 * Orchestrator passes `state` derived from `threadsQuery`.
 *
 * 4-state discriminated union per Phase 0.5 contract sez. 4.3:
 *   - `loading`: shimmer skeleton
 *   - `error`: error message + retry button
 *   - `empty`: no chat threads yet
 *   - `success`: renders thread list entries
 *
 * AC: T A V
 */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

export interface ChatThreadEntry {
  readonly id: string;
  readonly firstMessage: string;
  readonly when: string;
  readonly messageCount: number;
  readonly resolved: boolean;
}

export interface ChatHistoryTimelineLabels {
  readonly title: string;
  readonly loadingLabel: string;
  readonly errorLabel: string;
  readonly retryLabel: string;
  readonly empty: string;
  readonly emptySubtitle: string;
  readonly threadCount: string;
  readonly resolvedLabel: string;
  readonly unresolvedLabel: string;
  readonly messagesLabel: string;
}

/**
 * Discriminated union per Phase 0.5 contract sez. 4.3.
 */
export type ChatHistoryState =
  | { kind: 'loading' }
  | { kind: 'error'; retry: () => void }
  | { kind: 'empty' }
  | { kind: 'success'; threads: ReadonlyArray<ChatThreadEntry> };

export interface ChatHistoryTimelineProps {
  readonly state: ChatHistoryState;
  readonly labels: ChatHistoryTimelineLabels;
  readonly className?: string;
}

export function ChatHistoryTimeline(props: ChatHistoryTimelineProps): ReactElement {
  const { state, labels, className } = props;

  return (
    <section
      data-slot="agent-detail-chat-history-timeline"
      data-history-kind={state.kind}
      className={clsx('flex flex-col gap-4', className)}
    >
      {/* Section header */}
      <header className="flex items-baseline justify-between gap-3">
        <div>
          <h3 className="font-display text-[15px] font-extrabold text-foreground">
            {labels.title}
          </h3>
          {state.kind === 'success' ? (
            <p className="mt-0.5 font-mono text-[11px] text-muted-foreground">
              {labels.threadCount.replace('{count}', String(state.threads.length))}
            </p>
          ) : null}
        </div>
      </header>

      {/* Loading state */}
      {state.kind === 'loading' ? (
        <div className="flex flex-col gap-3" aria-label={labels.loadingLabel} aria-busy="true">
          {[0, 1, 2, 3].map(i => (
            <div key={i} className="h-14 animate-pulse rounded-xl bg-muted" aria-hidden="true" />
          ))}
        </div>
      ) : null}

      {/* Error state */}
      {state.kind === 'error' ? (
        <div className="flex flex-col items-center gap-3 rounded-xl border border-rose-200 bg-rose-50 px-6 py-8 text-center dark:border-rose-900/40 dark:bg-rose-950/20">
          <span aria-hidden="true" className="text-2xl">
            ⚠
          </span>
          <p className="font-display text-[13px] font-semibold text-rose-700 dark:text-rose-300">
            {labels.errorLabel}
          </p>
          <button
            type="button"
            onClick={state.retry}
            className="inline-flex items-center gap-1.5 rounded-lg bg-rose-700 px-4 py-2 font-display text-[12px] font-bold text-white hover:bg-rose-800 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-rose-700"
          >
            {labels.retryLabel}
          </button>
        </div>
      ) : null}

      {/* Empty state */}
      {state.kind === 'empty' ? (
        <div className="flex flex-col items-center gap-3 rounded-xl border border-dashed border-border bg-muted/30 px-6 py-10 text-center">
          <span aria-hidden="true" className="text-3xl">
            💬
          </span>
          <div>
            <p className="font-display text-[14px] font-bold text-foreground">{labels.empty}</p>
            <p className="mt-1 font-display text-[12px] text-muted-foreground">
              {labels.emptySubtitle}
            </p>
          </div>
        </div>
      ) : null}

      {/* Success state — thread list */}
      {state.kind === 'success' ? (
        <ul
          className="flex flex-col divide-y divide-border rounded-xl border border-border bg-card shadow-sm"
          role="list"
        >
          {state.threads.map(thread => (
            <li key={thread.id} className="flex items-start gap-4 px-5 py-4">
              <span aria-hidden="true" className="mt-0.5 text-xl">
                {thread.resolved ? '✅' : '💬'}
              </span>
              <div className="min-w-0 flex-1">
                <p className="truncate font-display text-[13px] font-bold text-foreground">
                  {thread.firstMessage}
                </p>
                <p className="mt-0.5 font-mono text-[11px] text-muted-foreground">
                  {thread.when} ·{' '}
                  {labels.messagesLabel.replace('{count}', String(thread.messageCount))}
                </p>
              </div>
              <span
                className={clsx(
                  'shrink-0 rounded-full px-2 py-0.5 font-mono text-[9px] font-extrabold uppercase tracking-[0.06em]',
                  thread.resolved
                    ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-300'
                    : 'bg-amber-100 text-amber-700 dark:bg-amber-950/40 dark:text-amber-300'
                )}
              >
                {thread.resolved ? labels.resolvedLabel : labels.unresolvedLabel}
              </span>
            </li>
          ))}
        </ul>
      ) : null}
    </section>
  );
}

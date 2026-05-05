/**
 * KbDocList - v2 Wave C.2 (Issue #581)
 *
 * Mapped from `admin-mockups/design_files/sp4-agent-detail.jsx` (KbDocItem tab).
 * Spec: docs/superpowers/specs/2026-04-26-v2-design-migration.md (Phase 1+2)
 * Tracking: docs/frontend/v2-migration-matrix.md (Issue #581)
 *
 * Pure presentational component — no hooks, no i18n calls, no data fetching.
 * Orchestrator passes `state` derived from `kbDocsQuery` and `agent.gameId`.
 *
 * CRITICAL — 5-state discriminated union per Phase 0.5 contract sez. 4.3:
 *   - `loading`: shimmer skeleton
 *   - `error`: error message + retry button
 *   - `empty` (Cell 8): gameId valid but no docs uploaded — generic empty copy
 *   - `standalone` (Cell 10): agent.gameId === null — DEDICATED copy (different from empty)
 *   - `success`: renders doc list entries
 *
 * The `standalone` vs `empty` distinction is Cell 10 of the FSM matrix.
 * Tests MUST verify the copy is distinct.
 *
 * AC: T A V
 */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

export type KbDocStatus = 'indexed' | 'processing' | 'failed';

export interface KbDocEntry {
  readonly id: string;
  readonly title: string;
  readonly status: KbDocStatus;
  readonly sizeFormatted: string;
  readonly pages: number;
  readonly chunks: number;
}

export interface KbDocListLabels {
  readonly title: string;
  readonly subtitle: string;
  readonly loadingLabel: string;
  readonly errorLabel: string;
  readonly retryLabel: string;
  readonly empty: string;
  readonly emptySubtitle: string;
  readonly uploadCta: string;
  readonly standaloneTitle: string;
  readonly standaloneSubtitle: string;
  readonly standaloneCta: string;
  readonly docsCount: string;
  readonly statusIndexed: string;
  readonly statusProcessing: string;
  readonly statusFailed: string;
}

/**
 * Discriminated union per Phase 0.5 contract sez. 4.3.
 * Prevents `data + loading` co-occurrence.
 * CRITICAL: `standalone` (Cell 10) and `empty` (Cell 8) are distinct states
 * with dedicated copy — do NOT merge them.
 */
export type KbDocsState =
  | { kind: 'loading' }
  | { kind: 'error'; retry: () => void }
  | { kind: 'empty' } // Cell 8: gameId valid, no docs uploaded yet
  | { kind: 'standalone' } // Cell 10: agent.gameId === null (no game association)
  | { kind: 'success'; docs: ReadonlyArray<KbDocEntry> };

export interface KbDocListProps {
  readonly state: KbDocsState;
  readonly labels: KbDocListLabels;
  readonly onUpload?: () => void;
  readonly onAssociateGame?: () => void;
  readonly className?: string;
}

const STATUS_CLASSES: Record<KbDocStatus, string> = {
  indexed: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-300',
  processing: 'bg-amber-100 text-amber-700 dark:bg-amber-950/40 dark:text-amber-300',
  failed: 'bg-rose-100 text-rose-700 dark:bg-rose-950/40 dark:text-rose-300',
};

function statusLabel(status: KbDocStatus, labels: KbDocListLabels): string {
  if (status === 'indexed') return labels.statusIndexed;
  if (status === 'processing') return labels.statusProcessing;
  return labels.statusFailed;
}

export function KbDocList(props: KbDocListProps): ReactElement {
  const { state, labels, onUpload, onAssociateGame, className } = props;

  return (
    <section
      data-slot="agent-detail-kb-doc-list"
      data-kb-kind={state.kind}
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
              {labels.docsCount.replace('{count}', String(state.docs.length))}
            </p>
          ) : null}
        </div>
        {state.kind === 'success' || state.kind === 'empty' ? (
          onUpload ? (
            <button
              type="button"
              onClick={onUpload}
              className="inline-flex shrink-0 items-center gap-1.5 rounded-lg border border-border px-3 py-1.5 font-display text-[12px] font-bold text-foreground hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
            >
              {labels.uploadCta}
            </button>
          ) : null
        ) : null}
      </header>

      {/* Loading state */}
      {state.kind === 'loading' ? (
        <div className="flex flex-col gap-3" aria-label={labels.loadingLabel} aria-busy="true">
          {[0, 1, 2].map(i => (
            <div key={i} className="h-16 animate-pulse rounded-xl bg-muted" aria-hidden="true" />
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

      {/* Empty state (Cell 8) — gameId valid, no docs */}
      {state.kind === 'empty' ? (
        <div className="flex flex-col items-center gap-3 rounded-xl border border-dashed border-border bg-muted/30 px-6 py-10 text-center">
          <span aria-hidden="true" className="text-3xl">
            📄
          </span>
          <div>
            <p className="font-display text-[14px] font-bold text-foreground">{labels.empty}</p>
            <p className="mt-1 font-display text-[12px] text-muted-foreground">
              {labels.emptySubtitle}
            </p>
          </div>
        </div>
      ) : null}

      {/* Standalone state (Cell 10) — agent.gameId === null — DEDICATED copy */}
      {state.kind === 'standalone' ? (
        <div className="flex flex-col items-center gap-3 rounded-xl border border-dashed border-border bg-muted/30 px-6 py-10 text-center">
          <span aria-hidden="true" className="text-3xl">
            🤖
          </span>
          <div>
            <p className="font-display text-[14px] font-bold text-foreground">
              {labels.standaloneTitle}
            </p>
            <p className="mt-1 font-display text-[12px] text-muted-foreground">
              {labels.standaloneSubtitle}
            </p>
          </div>
          {onAssociateGame ? (
            <button
              type="button"
              onClick={onAssociateGame}
              className="inline-flex items-center gap-1.5 rounded-lg border border-border px-4 py-2 font-display text-[12px] font-bold text-foreground hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
            >
              {labels.standaloneCta}
            </button>
          ) : null}
        </div>
      ) : null}

      {/* Success state — doc list */}
      {state.kind === 'success' ? (
        <ul
          className="flex flex-col divide-y divide-border rounded-xl border border-border bg-card shadow-sm"
          role="list"
        >
          {state.docs.map(doc => (
            <li key={doc.id} className="flex items-start gap-4 px-5 py-4">
              <span aria-hidden="true" className="mt-0.5 text-xl">
                📄
              </span>
              <div className="min-w-0 flex-1">
                <p className="truncate font-display text-[13px] font-bold text-foreground">
                  {doc.title}
                </p>
                <p className="mt-0.5 font-mono text-[11px] text-muted-foreground">
                  {doc.sizeFormatted} · {doc.pages}p · {doc.chunks} chunks
                </p>
              </div>
              <span
                className={clsx(
                  'shrink-0 rounded-full px-2 py-0.5 font-mono text-[9px] font-extrabold uppercase tracking-[0.06em]',
                  STATUS_CLASSES[doc.status]
                )}
              >
                {statusLabel(doc.status, labels)}
              </span>
            </li>
          ))}
        </ul>
      ) : null}
    </section>
  );
}

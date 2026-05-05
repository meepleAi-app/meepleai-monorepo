/**
 * GameDetailKbDocList - v2 Wave C.1 (Issue #581)
 *
 * Mapped from `admin-mockups/design_files/sp4-game-detail.jsx` (DocsTab + DocItem).
 * Spec: docs/superpowers/specs/2026-04-26-v2-design-migration.md (Phase 1+2)
 * Tracking: docs/frontend/v2-migration-matrix.md (Issue #573)
 *
 * Lists indexed KB documents (PDFs / rulebooks) for this game with status
 * pip (indexed / processing / failed). CTA opens the KB detail subroute.
 *
 * AC: T A V
 */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';
import Link from 'next/link';

export type GameDetailKbStatus = 'indexed' | 'processing' | 'failed';

export interface GameDetailKbDocEntry {
  readonly id: string;
  readonly title: string;
  readonly status: GameDetailKbStatus;
  readonly sizeFormatted: string;
  readonly pages: number;
  readonly chunks: number;
  readonly href?: string;
}

export interface GameDetailKbDocListLabels {
  readonly title: string;
  readonly subtitle: string;
  readonly empty: string;
  readonly emptySubtitle: string;
  readonly uploadCta: string;
  readonly openCta: string;
  readonly openAriaLabel: string;
  readonly statusIndexed: string;
  readonly statusProcessing: string;
  readonly statusFailed: string;
  readonly statsLineTemplate: string;
}

export interface GameDetailKbDocListProps {
  readonly docs: ReadonlyArray<GameDetailKbDocEntry>;
  readonly labels: GameDetailKbDocListLabels;
  readonly onUpload?: () => void;
  readonly className?: string;
}

const STATUS_CLASSES: Record<GameDetailKbStatus, string> = {
  indexed: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-300',
  processing: 'bg-amber-100 text-amber-700 dark:bg-amber-950/40 dark:text-amber-300',
  failed: 'bg-rose-100 text-rose-700 dark:bg-rose-950/40 dark:text-rose-300',
};

export function GameDetailKbDocList(props: GameDetailKbDocListProps): ReactElement {
  const { docs, labels, onUpload, className } = props;
  const isEmpty = docs.length === 0;

  function statusLabel(status: GameDetailKbStatus): string {
    if (status === 'indexed') return labels.statusIndexed;
    if (status === 'processing') return labels.statusProcessing;
    return labels.statusFailed;
  }

  function formatStatsLine(doc: GameDetailKbDocEntry): string {
    return labels.statsLineTemplate
      .replace('{size}', doc.sizeFormatted)
      .replace('{pages}', String(doc.pages))
      .replace('{chunks}', String(doc.chunks));
  }

  return (
    <section
      data-slot="game-detail-kb-doc-list"
      data-empty={isEmpty}
      className={clsx('flex flex-col gap-3', className)}
    >
      <header className="flex items-baseline justify-between gap-2">
        <div>
          <h3 className="font-display text-[15px] font-extrabold text-foreground">
            {labels.title}
          </h3>
          <p className="mt-0.5 text-[12px] text-muted-foreground">{labels.subtitle}</p>
        </div>
        {onUpload ? (
          <button
            type="button"
            onClick={onUpload}
            data-slot="game-detail-kb-upload"
            className="rounded-md border-none bg-sky-700 px-3 py-1 font-display text-[11px] font-extrabold text-white shadow-sm transition-shadow hover:shadow-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            {labels.uploadCta}
          </button>
        ) : null}
      </header>

      {isEmpty ? (
        <div
          data-slot="game-detail-kb-empty"
          className="flex flex-col items-center justify-center gap-2 rounded-2xl border border-dashed border-border bg-muted/30 px-6 py-10 text-center"
        >
          <span aria-hidden="true" className="text-3xl">
            📄
          </span>
          <h4 className="font-display text-[14px] font-extrabold text-foreground">
            {labels.empty}
          </h4>
          <p className="max-w-sm text-[12px] text-muted-foreground">{labels.emptySubtitle}</p>
        </div>
      ) : (
        <ul role="list" className="flex flex-col gap-2">
          {docs.map(doc => (
            <li
              key={doc.id}
              data-slot="game-detail-kb-row"
              data-doc-id={doc.id}
              data-status={doc.status}
              className="flex items-center gap-3 rounded-2xl border border-border bg-card p-3 shadow-sm transition-shadow hover:shadow-md"
            >
              <div
                aria-hidden="true"
                className="flex h-10 w-10 flex-shrink-0 items-center justify-center rounded-md bg-sky-100 text-[20px] text-sky-700 dark:bg-sky-950/40 dark:text-sky-300"
              >
                📄
              </div>
              <div className="flex min-w-0 flex-1 flex-col">
                <span className="truncate font-mono text-[13px] font-bold text-foreground">
                  {doc.title}
                </span>
                <span className="truncate font-mono text-[10px] font-semibold text-muted-foreground">
                  <span
                    data-slot="game-detail-kb-status"
                    className={clsx(
                      'mr-1.5 rounded px-1 py-0.5 font-extrabold uppercase tracking-[0.04em]',
                      STATUS_CLASSES[doc.status]
                    )}
                  >
                    {statusLabel(doc.status)}
                  </span>
                  {formatStatsLine(doc)}
                </span>
              </div>
              {doc.href ? (
                <Link
                  href={doc.href}
                  aria-label={labels.openAriaLabel.replace('{name}', doc.title)}
                  data-slot="game-detail-kb-open"
                  className="rounded-md border border-border px-2 py-1 font-display text-[11px] font-extrabold text-foreground transition-colors hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                >
                  {labels.openCta}
                </Link>
              ) : null}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

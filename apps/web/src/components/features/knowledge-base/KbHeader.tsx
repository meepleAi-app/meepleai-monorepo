/**
 * KbHeader — document metadata card for the KB detail surface (#2311 FE-3).
 * Renders the sticky sx column header in the split-view layout: title,
 * kind/version pill, last-ingested timestamp, uploader, chunk + page counts,
 * + language tag. Uses the `--c-kb` entity utility (DEC-D5 token rule).
 */

'use client';

import { type ReactElement } from 'react';

import clsx from 'clsx';

import type { KbDocument } from '@/lib/api/kb-detail-api';

export interface KbHeaderProps {
  readonly document: KbDocument;
  readonly className?: string;
}

function formatBytes(size: number): string {
  if (size < 1024) return `${size} B`;
  if (size < 1024 * 1024) return `${Math.round(size / 102.4) / 10} KB`;
  return `${Math.round(size / (1024 * 102.4)) / 10} MB`;
}

function formatDate(iso: string): string {
  try {
    return new Intl.DateTimeFormat('it-IT', {
      day: '2-digit',
      month: 'short',
      year: 'numeric',
    }).format(new Date(iso));
  } catch {
    return iso;
  }
}

export function KbHeader({ document, className }: KbHeaderProps): ReactElement {
  const sizeLabel = formatBytes(document.fileSize);
  const ingestedLabel = formatDate(document.lastIngestedAt);

  return (
    <header
      data-slot="kb-header"
      className={clsx(
        'flex flex-col gap-3 border-b border-border bg-card p-4',
        'text-sm text-card-foreground',
        className
      )}
    >
      <div className="flex items-start justify-between gap-2">
        <h1 className="font-display text-lg font-extrabold text-entity-kb">{document.title}</h1>
        <span
          aria-label={`Tipo documento ${document.docType}`}
          className="rounded-md bg-entity-kb/15 px-2 py-0.5 font-mono text-[11px] font-bold uppercase text-entity-kb"
        >
          {document.docType}
        </span>
      </div>

      <dl className="grid grid-cols-2 gap-x-3 gap-y-1 text-[12px] text-muted-foreground">
        {document.gameName ? (
          <>
            <dt className="font-semibold">Gioco</dt>
            <dd className="font-mono text-foreground">{document.gameName}</dd>
          </>
        ) : null}

        <dt className="font-semibold">Lingua</dt>
        <dd className="font-mono uppercase text-foreground">{document.language}</dd>

        <dt className="font-semibold">Chunk</dt>
        <dd className="font-mono text-foreground">{document.chunkCount}</dd>

        {document.pageCount !== null ? (
          <>
            <dt className="font-semibold">Pagine</dt>
            <dd className="font-mono text-foreground">{document.pageCount}</dd>
          </>
        ) : null}

        <dt className="font-semibold">Dimensione</dt>
        <dd className="font-mono text-foreground">{sizeLabel}</dd>

        <dt className="font-semibold">Caricato da</dt>
        <dd className="font-mono text-foreground">{document.uploaderName}</dd>

        <dt className="font-semibold">Indicizzato</dt>
        <dd className="font-mono text-foreground">{ingestedLabel}</dd>

        {document.indexerVersion ? (
          <>
            <dt className="font-semibold">Indexer</dt>
            <dd className="font-mono text-foreground">{document.indexerVersion}</dd>
          </>
        ) : null}
      </dl>
    </header>
  );
}

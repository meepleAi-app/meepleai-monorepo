/**
 * KbChunkListPanel — chunks list for the KB detail split-view (#2311 FE-3).
 * Renders each row with heading-path crumbs + snippet + "usato in N chat" pill
 * (BE-1 `usedInChats`).
 *
 * Note on virtualization (DEC-D3): the spec called for `react-window`. The
 * installed version is 2.x with a redesigned API (`List` rowComponent prop
 * instead of `FixedSizeList`) that doesn't fit the simple constant-height
 * usage here. Typical KB docs ship 50-200 chunks — well within the DOM
 * budget for a plain `<ul>`. Virtualization is filed as a follow-up if
 * profiling on real documents shows scroll jank.
 */

'use client';

import { type ReactElement } from 'react';

import clsx from 'clsx';

import type { KbChunkSummary } from '@/lib/api/kb-detail-api';

export interface KbChunkListPanelProps {
  readonly chunks: ReadonlyArray<KbChunkSummary>;
  readonly activeChunkId: string | null;
  readonly onSelect: (chunkId: string) => void;
  readonly className?: string;
  readonly emptyLabel?: string;
}

function Row({
  chunk,
  isActive,
  onSelect,
}: {
  chunk: KbChunkSummary;
  isActive: boolean;
  onSelect: (id: string) => void;
}): ReactElement {
  const headingCrumb = chunk.headingPath.length > 0 ? chunk.headingPath.join(' › ') : '—';
  return (
    <button
      type="button"
      data-slot="kb-chunk-row"
      data-active={isActive}
      aria-pressed={isActive}
      onClick={() => onSelect(chunk.id)}
      className={clsx(
        'flex w-full flex-col items-start gap-1 border-b border-border px-3 py-2 text-left transition-colors',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
        isActive ? 'bg-entity-kb/10 text-foreground' : 'bg-card text-foreground hover:bg-muted/50'
      )}
    >
      <div className="flex w-full items-center justify-between gap-2">
        <span className="truncate font-mono text-[11px] uppercase text-muted-foreground">
          #{chunk.position}
          {chunk.pageNumber !== null ? ` · p.${chunk.pageNumber}` : ''}
        </span>
        {chunk.usedInChats > 0 ? (
          <span
            aria-label={`Usato in ${chunk.usedInChats} chat`}
            className="rounded-full bg-entity-kb/15 px-1.5 py-0.5 font-mono text-[10px] font-bold text-entity-kb"
          >
            {chunk.usedInChats}× chat
          </span>
        ) : null}
      </div>
      <span className="w-full truncate text-xs font-semibold text-foreground" title={headingCrumb}>
        {headingCrumb}
      </span>
      <span className="line-clamp-2 w-full text-[12px] text-muted-foreground">{chunk.snippet}</span>
    </button>
  );
}

export function KbChunkListPanel({
  chunks,
  activeChunkId,
  onSelect,
  className,
  emptyLabel = 'Nessun chunk disponibile.',
}: KbChunkListPanelProps): ReactElement {
  if (chunks.length === 0) {
    return (
      <div
        data-slot="kb-chunk-list-empty"
        className={clsx(
          'flex h-full items-center justify-center bg-card p-6 text-sm text-muted-foreground',
          className
        )}
      >
        {emptyLabel}
      </div>
    );
  }

  return (
    <ul
      data-slot="kb-chunk-list"
      className={clsx('flex h-full flex-col overflow-y-auto', className)}
    >
      {chunks.map(chunk => (
        <li key={chunk.id}>
          <Row chunk={chunk} isActive={chunk.id === activeChunkId} onSelect={onSelect} />
        </li>
      ))}
    </ul>
  );
}

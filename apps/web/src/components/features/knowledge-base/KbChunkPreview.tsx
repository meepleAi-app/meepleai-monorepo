/**
 * KbChunkPreview — right pane of the KB detail split-view (#2311 FE-3).
 * Shows the selected chunk's full body via `MarkdownRenderBlock`, with an
 * `AnimatedUnderlineTabs`-powered Markdown / Raw toggle (DEC-D5 primitive).
 *
 * Empty / loading / error states render in-place so the orchestrator does
 * not have to fork on the FSM.
 */

'use client';

import { useState, type ReactElement } from 'react';

import clsx from 'clsx';

import { AnimatedUnderlineTabs } from '@/components/ui/navigation/animated-underline-tabs';
import type { KbChunkDetail } from '@/lib/api/kb-detail-api';

import { MarkdownRenderBlock } from './MarkdownRenderBlock';

export type KbChunkPreviewState =
  | { kind: 'empty' }
  | { kind: 'loading' }
  | { kind: 'error'; message: string }
  | { kind: 'ready'; chunk: KbChunkDetail };

export interface KbChunkPreviewProps {
  readonly state: KbChunkPreviewState;
  readonly className?: string;
  readonly emptyLabel?: string;
}

type SubTab = 'markdown' | 'raw';

const TABS = [
  { key: 'markdown' as SubTab, label: 'Markdown' },
  { key: 'raw' as SubTab, label: 'Raw' },
];

const ACCENT_ACTIVE = 'font-extrabold text-entity-kb';
const ACCENT_INDICATOR = 'bg-entity-kb';
const ACCENT_COUNT_ACTIVE = 'bg-entity-kb text-white';

export function KbChunkPreview({
  state,
  className,
  emptyLabel = 'Seleziona un chunk per visualizzarne il contenuto.',
}: KbChunkPreviewProps): ReactElement {
  const [tab, setTab] = useState<SubTab>('markdown');

  if (state.kind === 'empty') {
    return (
      <div
        data-slot="kb-chunk-preview-empty"
        className={clsx(
          'flex h-full items-center justify-center bg-background p-6 text-sm text-muted-foreground',
          className
        )}
      >
        {emptyLabel}
      </div>
    );
  }

  if (state.kind === 'loading') {
    return (
      <div
        data-slot="kb-chunk-preview-loading"
        aria-busy
        className={clsx(
          'flex h-full items-center justify-center bg-background p-6 text-sm text-muted-foreground',
          className
        )}
      >
        Caricamento chunk…
      </div>
    );
  }

  if (state.kind === 'error') {
    return (
      <div
        data-slot="kb-chunk-preview-error"
        role="alert"
        className={clsx(
          'flex h-full items-center justify-center bg-background p-6 text-sm text-destructive',
          className
        )}
      >
        {state.message}
      </div>
    );
  }

  const { chunk } = state;
  const headingCrumb =
    chunk.headingPath.length > 0 ? chunk.headingPath.join(' › ') : `Chunk #${chunk.position}`;

  return (
    <section
      data-slot="kb-chunk-preview"
      className={clsx('flex h-full flex-col bg-background', className)}
    >
      <div className="flex items-baseline justify-between gap-3 border-b border-border bg-card px-4 py-2">
        <h2
          className="truncate font-display text-sm font-extrabold text-foreground"
          title={headingCrumb}
        >
          {headingCrumb}
        </h2>
        <span className="font-mono text-[11px] uppercase text-muted-foreground">
          #{chunk.position}
          {chunk.pageNumber !== null ? ` · p.${chunk.pageNumber}` : ''}
        </span>
      </div>

      <AnimatedUnderlineTabs<SubTab>
        tabs={TABS}
        active={tab}
        onChange={setTab}
        ariaLabel="Vista preview chunk"
        slotPrefix="kb-preview-tab"
        accentActiveClassName={ACCENT_ACTIVE}
        accentIndicatorClassName={ACCENT_INDICATOR}
        accentCountActiveClassName={ACCENT_COUNT_ACTIVE}
      />

      <div
        role="tabpanel"
        id={`kb-preview-panel-${tab}`}
        aria-labelledby={`kb-preview-tab-${tab}`}
        className="flex-1 overflow-y-auto p-4"
      >
        {tab === 'markdown' ? (
          <MarkdownRenderBlock content={chunk.content} />
        ) : (
          <pre className="whitespace-pre-wrap font-mono text-[12px] text-foreground">
            {chunk.content}
          </pre>
        )}
      </div>
    </section>
  );
}

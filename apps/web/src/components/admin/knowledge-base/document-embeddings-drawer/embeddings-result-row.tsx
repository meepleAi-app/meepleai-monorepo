'use client';

import { useState, type JSX } from 'react';

import { VecThumb } from './vec-thumb';

/**
 * Issue #1674: single result row for semantic search inside DocumentEmbeddingsDrawer.
 * Collapsed: page, chunkIdx, snippet, score-pill. Expanded: full snippet + VecThumb + meta.
 */

/**
 * Mirrors BE DocChunkSearchHit (apps/api/src/Api/BoundedContexts/KnowledgeBase/
 * Application/Queries/SearchDocumentChunks/SearchDocumentChunksByVectorQuery.cs).
 */
export interface ScoredChunkDto {
  chunkIndex: number;
  pageNumber: number | null;
  snippet: string;
  score: number;
}

export interface EmbeddingsResultRowProps {
  chunk: ScoredChunkDto;
}

function scoreClass(score: number): string {
  if (score >= 0.75) return 'text-entity-kb';
  if (score >= 0.5) return 'text-amber-500';
  return 'text-muted-foreground';
}

export function EmbeddingsResultRow({ chunk }: EmbeddingsResultRowProps): JSX.Element {
  const [expanded, setExpanded] = useState(false);

  return (
    <div className={expanded ? 'bg-muted' : 'bg-card'}>
      <button
        type="button"
        onClick={() => setExpanded(prev => !prev)}
        aria-expanded={expanded}
        aria-label={expanded ? 'Collassa riga risultato' : 'Espandi riga risultato'}
        className="grid w-full grid-cols-[60px_60px_1fr_70px_30px] items-center gap-3 border-b border-border px-3 py-2 text-left text-xs focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
      >
        <span className="font-mono text-[10px] text-muted-foreground">
          {chunk.pageNumber !== null ? `p.${chunk.pageNumber}` : '—'}
        </span>
        <span className="font-mono text-[10px] text-muted-foreground">#{chunk.chunkIndex}</span>
        <span className="truncate text-muted-foreground">{chunk.snippet}</span>
        <span className={`text-right font-mono font-bold ${scoreClass(chunk.score)}`}>
          {chunk.score.toFixed(3)}
        </span>
        <span className="font-mono text-xs text-muted-foreground">{expanded ? '▾' : '›'}</span>
      </button>
      {expanded ? (
        <div className="border-b border-border bg-card px-3 pb-3 pt-2">
          <p className="mb-2 text-xs text-foreground">{chunk.snippet}</p>
          <dl className="grid grid-cols-[90px_1fr] gap-x-2 gap-y-1 font-mono text-[10px]">
            {chunk.pageNumber !== null ? (
              <>
                <dt className="text-muted-foreground">page</dt>
                <dd className="text-foreground">{chunk.pageNumber}</dd>
              </>
            ) : null}
            <dt className="text-muted-foreground">chunk idx</dt>
            <dd className="text-foreground">#{chunk.chunkIndex}</dd>
            <dt className="text-muted-foreground">score</dt>
            <dd className="text-foreground">{chunk.score.toFixed(4)} (cosine)</dd>
          </dl>
          <VecThumb seed={chunk.chunkIndex} />
        </div>
      ) : null}
    </div>
  );
}

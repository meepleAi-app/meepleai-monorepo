'use client';

import { useState, useRef } from 'react';

import { ChevronLeft, ChevronRight, Search, ChevronDown, ChevronUp } from 'lucide-react';

import {
  useChunksPreview,
  type ChunkPreviewDto,
} from '@/app/admin/(dashboard)/knowledge-base/queue/lib/queue-api';
import { Badge } from '@/components/ui/data-display/badge';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { cn } from '@/lib/utils';

// ── ChunkItem ─────────────────────────────────────────────────────────

interface ChunkItemProps {
  chunk: ChunkPreviewDto;
}

function ChunkItem({ chunk }: ChunkItemProps) {
  const [expanded, setExpanded] = useState(false);

  return (
    <div
      data-testid={`chunk-${chunk.chunkIndex}`}
      className="rounded-md border border-border bg-card p-3 text-sm"
    >
      {/* Header row */}
      <div className="flex items-center gap-2">
        <span className="font-mono text-xs font-semibold text-muted-foreground">
          #{chunk.chunkIndex}
        </span>
        <span className="text-xs text-muted-foreground">p.{chunk.pageNumber}</span>
        <Badge variant="secondary" className="ml-auto shrink-0 text-xs">
          {chunk.model}
        </Badge>
        <button
          type="button"
          onClick={() => setExpanded(v => !v)}
          className="ml-1 rounded p-0.5 text-muted-foreground hover:text-foreground focus:outline-none"
          aria-label={expanded ? 'Comprimi chunk' : 'Espandi chunk'}
        >
          {expanded ? (
            <ChevronUp className="h-3.5 w-3.5" />
          ) : (
            <ChevronDown className="h-3.5 w-3.5" />
          )}
        </button>
      </div>

      {/* Text preview */}
      <p
        className={cn('mt-2 text-xs leading-relaxed text-foreground', !expanded && 'line-clamp-2')}
      >
        {chunk.textContent}
      </p>
    </div>
  );
}

// ── ChunkPreviewTab ───────────────────────────────────────────────────

interface ChunkPreviewTabProps {
  pdfDocumentId: string | null;
  className?: string;
}

export function ChunkPreviewTab({ pdfDocumentId, className }: ChunkPreviewTabProps) {
  const [page, setPage] = useState(1);
  const [searchInput, setSearchInput] = useState('');
  const [committedSearch, setCommittedSearch] = useState<string | undefined>(undefined);
  const inputRef = useRef<HTMLInputElement>(null);

  const PAGE_SIZE = 20;

  const { data, isLoading, isError } = useChunksPreview(
    pdfDocumentId,
    page,
    PAGE_SIZE,
    committedSearch
  );

  if (pdfDocumentId === null) return null;

  const totalPages = data ? Math.max(1, Math.ceil(data.total / PAGE_SIZE)) : 1;

  function commitSearch() {
    const trimmed = searchInput.trim() || undefined;
    setCommittedSearch(trimmed);
    setPage(1);
  }

  function handleKeyDown(e: React.KeyboardEvent<HTMLInputElement>) {
    if (e.key === 'Enter') commitSearch();
  }

  function handlePrev() {
    if (page > 1) setPage(p => p - 1);
  }

  function handleNext() {
    if (page < totalPages) setPage(p => p + 1);
  }

  return (
    <div data-testid="chunk-preview-tab" className={cn('flex flex-col gap-3', className)}>
      {/* Search bar */}
      <div className="flex items-center gap-2">
        <div className="relative flex-1">
          <Search className="absolute left-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-muted-foreground" />
          <Input
            ref={inputRef}
            value={searchInput}
            onChange={e => setSearchInput(e.target.value)}
            onKeyDown={handleKeyDown}
            placeholder="Cerca nei chunk…"
            className="pl-8 text-sm"
          />
        </div>
        <Button variant="secondary" size="sm" onClick={commitSearch}>
          Cerca
        </Button>
      </div>

      {/* Content */}
      {isLoading && (
        <div className="flex items-center justify-center py-8 text-sm text-muted-foreground">
          Caricamento chunk…
        </div>
      )}

      {isError && (
        <div className="rounded-md border border-destructive/30 bg-destructive/10 px-4 py-3 text-sm text-destructive">
          Errore nel caricamento dei chunk.
        </div>
      )}

      {!isLoading && !isError && data && (
        <>
          {data.chunks.length === 0 ? (
            <div className="py-8 text-center text-sm text-muted-foreground">
              Nessun chunk trovato.
            </div>
          ) : (
            <div className="flex flex-col gap-2">
              {data.chunks.map(chunk => (
                <ChunkItem key={chunk.embeddingId} chunk={chunk} />
              ))}
            </div>
          )}

          {/* Pagination */}
          <div className="flex items-center justify-between pt-1">
            <span className="text-xs text-muted-foreground">
              {data.total} chunk{data.total !== 1 ? 's' : ''}
            </span>
            <div className="flex items-center gap-1">
              <Button
                variant="ghost"
                size="sm"
                disabled={page <= 1}
                onClick={handlePrev}
                className="h-7 w-7 p-0"
                aria-label="Previous page"
              >
                <ChevronLeft className="h-3.5 w-3.5" />
              </Button>
              <span className="min-w-[4rem] text-center text-xs text-muted-foreground">
                {page} / {totalPages}
              </span>
              <Button
                variant="ghost"
                size="sm"
                disabled={page >= totalPages}
                onClick={handleNext}
                className="h-7 w-7 p-0"
                aria-label="Next page"
              >
                <ChevronRight className="h-3.5 w-3.5" />
              </Button>
            </div>
          </div>
        </>
      )}
    </div>
  );
}

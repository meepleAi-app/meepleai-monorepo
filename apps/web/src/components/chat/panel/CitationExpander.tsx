'use client';

import { useState, useCallback, useRef } from 'react';

import { Loader2, ChevronDown, ChevronUp } from 'lucide-react';

import { api } from '@/lib/api';
import { cn } from '@/lib/utils';

interface CitationExpanderProps {
  pdfId: string;
  pageNumber: number;
  docName?: string;
}

/**
 * Inline citation badge that expands to show extracted page text.
 * Fetches text on first expand and caches the result.
 */
export function CitationExpander({ pdfId, pageNumber, docName }: CitationExpanderProps) {
  const [expanded, setExpanded] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [pageText, setPageText] = useState<string | null>(null);
  const [documentTitle, setDocumentTitle] = useState<string | null>(null);
  const fetchedRef = useRef(false);

  const handleToggle = useCallback(async () => {
    if (expanded) {
      setExpanded(false);
      return;
    }

    setExpanded(true);

    // Only fetch on first expand
    if (!fetchedRef.current) {
      setLoading(true);
      setError(null);
      try {
        const result = await api.pdf.getPageText(pdfId, pageNumber);
        if (result) {
          setPageText(result.text);
          setDocumentTitle(result.documentTitle);
        } else {
          setError('Pagina non trovata');
        }
        fetchedRef.current = true;
      } catch {
        setError('Errore nel caricamento del testo');
      } finally {
        setLoading(false);
      }
    }
  }, [expanded, pdfId, pageNumber]);

  const displayTitle = documentTitle ?? docName ?? 'Documento';

  return (
    <span className="inline-flex flex-col align-bottom">
      <button
        type="button"
        data-testid="citation-badge"
        onClick={handleToggle}
        className={cn(
          'inline-flex items-center gap-1 rounded-full px-2.5 py-0.5',
          'text-[0.72rem] font-extrabold text-white transition-colors',
          'bg-orange-500 hover:bg-orange-600',
          'cursor-pointer select-none'
        )}
      >
        <span aria-hidden>📄</span>
        Regolamento p.{pageNumber}
        {expanded ? <ChevronUp className="h-3 w-3" /> : <ChevronDown className="h-3 w-3" />}
      </button>

      {expanded && (
        <div
          data-testid="citation-expanded"
          className={cn(
            'mt-1.5 rounded-lg border-l-3 px-3 py-2.5',
            'border-orange-400 bg-orange-50/50 dark:bg-orange-950/20'
          )}
        >
          {loading && (
            <div className="flex items-center gap-2 text-[0.78rem] text-[var(--nh-text-muted)]">
              <Loader2 className="h-3.5 w-3.5 animate-spin" data-testid="citation-loading" />
              Caricamento...
            </div>
          )}

          {error && (
            <p
              data-testid="citation-error"
              className="text-[0.78rem] text-red-600 dark:text-red-400"
            >
              {error}
            </p>
          )}

          {pageText !== null && !loading && (
            <>
              <div className="mb-1.5 font-quicksand text-[0.68rem] font-extrabold uppercase tracking-wider text-orange-700 dark:text-orange-300">
                {displayTitle} — Pagina {pageNumber}
              </div>
              <p
                data-testid="citation-text"
                className="whitespace-pre-wrap text-[0.78rem] leading-relaxed text-[var(--nh-text-secondary)]"
              >
                {pageText}
              </p>
            </>
          )}
        </div>
      )}
    </span>
  );
}

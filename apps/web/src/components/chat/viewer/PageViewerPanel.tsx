'use client';

import { useState, useEffect, useCallback, useRef } from 'react';

import { ChevronLeft, ChevronRight, Loader2, X } from 'lucide-react';

import { api } from '@/lib/api';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export interface PageViewerPanelProps {
  pdfId: string;
  pageNumber: number;
  isOpen: boolean;
  onClose: () => void;
}

// ============================================================================
// Component
// ============================================================================

/**
 * Side panel that shows extracted text for a PDF page with page navigation.
 * Displayed alongside the chat in full-screen mode.
 */
export function PageViewerPanel({ pdfId, pageNumber, isOpen, onClose }: PageViewerPanelProps) {
  const [currentPage, setCurrentPage] = useState(pageNumber);
  const [pageText, setPageText] = useState<string | null>(null);
  const [documentTitle, setDocumentTitle] = useState<string>('Documento');
  const [totalPages, setTotalPages] = useState<number>(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const panelRef = useRef<HTMLDivElement>(null);

  // Sync currentPage when the external pageNumber prop changes
  useEffect(() => {
    setCurrentPage(pageNumber);
  }, [pageNumber]);

  // Fetch page text whenever pdfId or currentPage changes
  useEffect(() => {
    if (!isOpen) return;

    let cancelled = false;

    async function fetchPage() {
      setLoading(true);
      setError(null);
      try {
        const result = await api.pdf.getPageText(pdfId, currentPage);
        if (cancelled) return;
        if (result) {
          setPageText(result.text);
          setDocumentTitle(result.documentTitle || 'Documento');
          setTotalPages(result.totalPages);
        } else {
          setError('Pagina non trovata');
        }
      } catch {
        if (!cancelled) {
          setError('Errore nel caricamento del testo');
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    void fetchPage();
    return () => {
      cancelled = true;
    };
  }, [pdfId, currentPage, isOpen]);

  // Escape key handler
  useEffect(() => {
    if (!isOpen) return;

    function handleKeyDown(e: KeyboardEvent) {
      if (e.key === 'Escape') {
        onClose();
      }
    }

    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, onClose]);

  const goToPrevPage = useCallback(() => {
    setCurrentPage(prev => Math.max(1, prev - 1));
  }, []);

  const goToNextPage = useCallback(() => {
    setCurrentPage(prev => (totalPages > 0 ? Math.min(totalPages, prev + 1) : prev + 1));
  }, [totalPages]);

  if (!isOpen) return null;

  return (
    <div
      ref={panelRef}
      data-testid="page-viewer-panel"
      className={cn(
        'hidden lg:flex flex-col w-[400px] min-w-[400px] border-l border-border/50',
        'bg-background/95 backdrop-blur-sm'
      )}
    >
      {/* Header */}
      <div className="flex items-center justify-between px-4 py-3 border-b border-border/30">
        <h3
          className="text-sm font-quicksand font-bold text-foreground truncate flex-1 mr-2"
          title={documentTitle}
        >
          {documentTitle}
        </h3>
        <button
          type="button"
          data-testid="page-viewer-close"
          onClick={onClose}
          aria-label="Chiudi pannello"
          className={cn(
            'rounded-md p-1 text-muted-foreground transition-colors',
            'hover:bg-muted hover:text-foreground'
          )}
        >
          <X className="h-4 w-4" />
        </button>
      </div>

      {/* Navigation */}
      <div className="flex items-center justify-center gap-3 px-4 py-2 border-b border-border/30">
        <button
          type="button"
          data-testid="page-viewer-prev"
          onClick={goToPrevPage}
          disabled={currentPage <= 1}
          aria-label="Pagina precedente"
          className={cn(
            'rounded-md p-1 transition-colors',
            currentPage <= 1
              ? 'text-muted-foreground/40 cursor-not-allowed'
              : 'text-muted-foreground hover:bg-muted hover:text-foreground'
          )}
        >
          <ChevronLeft className="h-4 w-4" />
        </button>

        <span
          className="text-xs font-nunito text-muted-foreground"
          data-testid="page-viewer-nav-label"
        >
          p.{currentPage}
          {totalPages > 0 ? ` / ${totalPages}` : ''}
        </span>

        <button
          type="button"
          data-testid="page-viewer-next"
          onClick={goToNextPage}
          disabled={totalPages > 0 && currentPage >= totalPages}
          aria-label="Pagina successiva"
          className={cn(
            'rounded-md p-1 transition-colors',
            totalPages > 0 && currentPage >= totalPages
              ? 'text-muted-foreground/40 cursor-not-allowed'
              : 'text-muted-foreground hover:bg-muted hover:text-foreground'
          )}
        >
          <ChevronRight className="h-4 w-4" />
        </button>
      </div>

      {/* Content */}
      <div className="flex-1 overflow-y-auto px-4 py-4" data-testid="page-viewer-content">
        {loading && (
          <div className="flex items-center justify-center gap-2 py-8 text-sm text-muted-foreground">
            <Loader2 className="h-4 w-4 animate-spin" data-testid="page-viewer-loading" />
            Caricamento...
          </div>
        )}

        {error && !loading && (
          <p
            data-testid="page-viewer-error"
            className="text-sm text-red-600 dark:text-red-400 py-4"
          >
            {error}
          </p>
        )}

        {pageText !== null && !loading && !error && (
          <p
            data-testid="page-viewer-text"
            className="whitespace-pre-wrap text-[0.82rem] leading-relaxed text-[var(--nh-text-secondary)] font-nunito"
          >
            {pageText}
          </p>
        )}
      </div>
    </div>
  );
}

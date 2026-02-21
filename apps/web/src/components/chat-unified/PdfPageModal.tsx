'use client';

import { useCallback, useState } from 'react';

import dynamic from 'next/dynamic';

import { ChevronLeft, ChevronRight, Loader2 } from 'lucide-react';

import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { api } from '@/lib/api';
import type { Citation } from '@/types';

// ─── react-pdf dynamic imports (avoids pdfjs SSR issues) ───────────────────

const PdfDocument = dynamic(
  () => import('react-pdf').then((mod) => {
    // Configure worker on first load
    const { pdfjs } = mod;
    pdfjs.GlobalWorkerOptions.workerSrc = `//cdnjs.cloudflare.com/ajax/libs/pdf.js/${pdfjs.version}/pdf.worker.min.js`;
    return mod.Document;
  }),
  {
    ssr: false,
    loading: () => (
      <div className="flex items-center justify-center h-48">
        <Loader2 className="w-6 h-6 animate-spin text-amber-500" />
      </div>
    ),
  }
);

const PdfPage = dynamic(() => import('react-pdf').then((mod) => mod.Page), {
  ssr: false,
});

// ─── Component ──────────────────────────────────────────────────────────────

interface PdfPageModalProps {
  citation: Citation;
  open: boolean;
  onClose: () => void;
}

/**
 * Modal that shows the source PDF page for a RAG citation.
 * Issue #4919: Citations as clickable links to source PDF page.
 */
export function PdfPageModal({ citation, open, onClose }: PdfPageModalProps) {
  const [numPages, setNumPages] = useState<number | null>(null);
  const [currentPage, setCurrentPage] = useState(citation.pageNumber);
  const [loadError, setLoadError] = useState<string | null>(null);

  // Reset state when citation changes (different document/page)
  const pdfUrl = api.pdf.getPdfDownloadUrl(citation.documentId);

  const handleDocumentLoad = useCallback(({ numPages }: { numPages: number }) => {
    setNumPages(numPages);
    setLoadError(null);
    // Clamp page to valid range
    setCurrentPage((prev) => Math.min(Math.max(1, prev), numPages));
  }, []);

  const handleDocumentError = useCallback(() => {
    setLoadError('Impossibile caricare il documento PDF. Verificare che il file sia disponibile.');
  }, []);

  const goToPrevPage = () => setCurrentPage((p) => Math.max(1, p - 1));
  const goToNextPage = () => setCurrentPage((p) => (numPages ? Math.min(numPages, p + 1) : p));

  // Reset page to citation page when modal opens/citation changes
  const handleOpenChange = (isOpen: boolean) => {
    if (!isOpen) {
      onClose();
    } else {
      setCurrentPage(citation.pageNumber);
      setLoadError(null);
    }
  };

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent
        className="max-w-3xl w-full max-h-[90vh] overflow-hidden flex flex-col p-0"
        aria-describedby={undefined}
      >
        {/* Header */}
        <DialogHeader className="px-6 pt-6 pb-3 flex-shrink-0">
          <DialogTitle className="font-quicksand text-base">
            Sorgente — Pagina {citation.pageNumber}
          </DialogTitle>
        </DialogHeader>

        {/* PDF Viewer */}
        <div className="flex-1 overflow-y-auto px-6 min-h-0">
          {loadError ? (
            <div className="flex items-center justify-center h-48 text-sm text-red-600 dark:text-red-400">
              {loadError}
            </div>
          ) : (
            <div className="flex justify-center">
              <PdfDocument
                file={pdfUrl}
                onLoadSuccess={handleDocumentLoad}
                onLoadError={handleDocumentError}
                loading={
                  <div className="flex items-center justify-center h-48">
                    <Loader2 className="w-6 h-6 animate-spin text-amber-500" />
                  </div>
                }
                error={
                  <div className="flex items-center justify-center h-48 text-sm text-red-600 dark:text-red-400">
                    Errore nel caricamento del PDF.
                  </div>
                }
              >
                <PdfPage
                  pageNumber={currentPage}
                  width={Math.min(620, typeof window !== 'undefined' ? window.innerWidth - 80 : 620)}
                  renderTextLayer={false}
                  renderAnnotationLayer={false}
                />
              </PdfDocument>
            </div>
          )}
        </div>

        {/* Snippet */}
        {citation.snippet && (
          <div className="mx-6 mb-2 px-3 py-2 bg-amber-50 dark:bg-amber-500/10 rounded-lg">
            <p className="text-xs text-amber-800 dark:text-amber-300 font-nunito leading-relaxed">
              &ldquo;{citation.snippet}&rdquo;
            </p>
          </div>
        )}

        {/* Page navigation */}
        <div className="flex items-center justify-between px-6 py-3 border-t border-border/50 flex-shrink-0">
          <Button
            variant="ghost"
            size="sm"
            onClick={goToPrevPage}
            disabled={currentPage <= 1}
            aria-label="Pagina precedente"
          >
            <ChevronLeft className="w-4 h-4" />
          </Button>

          <span className="text-xs text-muted-foreground font-nunito tabular-nums">
            {numPages ? `Pagina ${currentPage} / ${numPages}` : `Pagina ${currentPage}`}
          </span>

          <Button
            variant="ghost"
            size="sm"
            onClick={goToNextPage}
            disabled={numPages !== null && currentPage >= numPages}
            aria-label="Pagina successiva"
          >
            <ChevronRight className="w-4 h-4" />
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}

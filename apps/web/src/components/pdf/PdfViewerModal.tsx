/**
 * PdfViewerModal - Modal for PDF viewing (BGAI-074, #4133, #4252)
 * Uses react-pdf with pdfjs-dist v5 for security (GHSA-wgrm-67xf-hhpq).
 */

'use client';

import { useState, useCallback, useRef, useEffect } from 'react';

import { AlertCircle, Loader2 } from 'lucide-react';
import { Document, Page, pdfjs } from 'react-pdf';
import 'react-pdf/dist/Page/AnnotationLayer.css';
import 'react-pdf/dist/Page/TextLayer.css';

import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/overlays/dialog';

pdfjs.GlobalWorkerOptions.workerSrc = new URL(
  'pdfjs-dist/build/pdf.worker.min.mjs',
  import.meta.url
).toString();

export interface PdfViewerModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  pdfUrl: string;
  initialPage?: number;
  documentName?: string;
}

export function PdfViewerModal({
  open,
  onOpenChange,
  pdfUrl,
  initialPage = 1,
  documentName = 'PDF Document',
}: PdfViewerModalProps) {
  const [numPages, setNumPages] = useState<number>(0);
  const [currentPage, setCurrentPage] = useState<number>(initialPage);
  const [pdfBlob, setPdfBlob] = useState<Blob | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  const onDocumentLoadSuccess = useCallback(({ numPages }: { numPages: number }) => {
    setNumPages(numPages);
  }, []);

  // Fetch PDF as blob with credentials to bypass pdfjs worker cookie issues.
  // AbortController cancels in-flight downloads when modal closes or URL changes.
  useEffect(() => {
    if (!open || !pdfUrl) {
      setPdfBlob(null);
      setLoadError(null);
      return;
    }

    const controller = new AbortController();
    setLoading(true);
    setLoadError(null);
    setNumPages(0);
    setCurrentPage(initialPage);

    fetch(pdfUrl, { credentials: 'include', signal: controller.signal })
      .then(res => {
        if (!res.ok) throw new Error(`HTTP ${res.status}: ${res.statusText}`);
        return res.blob();
      })
      .then(blob => {
        setPdfBlob(blob);
        setLoading(false);
      })
      .catch(err => {
        if (err.name !== 'AbortError') {
          setLoadError(err.message);
          setLoading(false);
        }
      });

    return () => {
      controller.abort();
    };
  }, [open, pdfUrl, initialPage]);

  const goToPage = useCallback(
    (page: number) => {
      setCurrentPage(Math.max(1, Math.min(page, numPages)));
    },
    [numPages]
  );

  if (!open) return null;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-6xl h-[90vh]">
        <DialogHeader>
          <DialogTitle>{documentName}</DialogTitle>
        </DialogHeader>

        {/* Loading state */}
        {loading && (
          <div className="flex-1 flex items-center justify-center">
            <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
          </div>
        )}

        {/* Error state */}
        {loadError && (
          <div className="flex-1 flex flex-col items-center justify-center gap-3 text-red-600">
            <AlertCircle className="h-8 w-8" />
            <p className="text-sm font-medium">Errore nel caricamento del PDF</p>
            <p className="text-xs text-muted-foreground">{loadError}</p>
          </div>
        )}

        {/* Navigation controls */}
        {!loading && !loadError && numPages > 0 && (
          <div className="flex items-center justify-center gap-4 py-2 border-b">
            <button
              onClick={() => goToPage(currentPage - 1)}
              disabled={currentPage <= 1}
              className="px-3 py-1 text-sm rounded border disabled:opacity-50 hover:bg-gray-100"
            >
              Precedente
            </button>
            <span className="text-sm">
              Pag. {currentPage} / {numPages}
            </span>
            <button
              onClick={() => goToPage(currentPage + 1)}
              disabled={currentPage >= numPages}
              className="px-3 py-1 text-sm rounded border disabled:opacity-50 hover:bg-gray-100"
            >
              Successiva
            </button>
          </div>
        )}

        {/* PDF viewer */}
        {!loading && !loadError && pdfBlob && (
          <div ref={containerRef} className="flex-1 overflow-auto flex justify-center">
            <Document file={pdfBlob} onLoadSuccess={onDocumentLoadSuccess}>
              <Page pageNumber={currentPage} width={800} />
            </Document>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}

export default PdfViewerModal;

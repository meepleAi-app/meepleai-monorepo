/**
 * PdfViewerModal - Modal for PDF viewing (BGAI-074, #4133, #4252)
 * Uses react-pdf with pdfjs-dist v5 for security (GHSA-wgrm-67xf-hhpq).
 */

'use client';

import { useState, useCallback, useRef, useEffect, useMemo } from 'react';

import { Document, Page, pdfjs } from 'react-pdf';
import 'react-pdf/dist/Page/AnnotationLayer.css';
import 'react-pdf/dist/Page/TextLayer.css';

import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';

pdfjs.GlobalWorkerOptions.workerSrc = new URL(
  'pdfjs-dist/build/pdf.worker.min.mjs',
  import.meta.url,
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
  const containerRef = useRef<HTMLDivElement>(null);

  const onDocumentLoadSuccess = useCallback(({ numPages }: { numPages: number }) => {
    setNumPages(numPages);
  }, []);

  // Reset to initial page when modal opens
  useEffect(() => {
    if (open) {
      setCurrentPage(initialPage);
    }
  }, [open, initialPage]);

  const goToPage = useCallback((page: number) => {
    setCurrentPage(Math.max(1, Math.min(page, numPages)));
  }, [numPages]);

  // Stable reference — prevents react-pdf from cancelling the load task on every render
  const pdfOptions = useMemo(() => ({ withCredentials: true }), []);

  if (!open) return null;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-6xl h-[90vh]">
        <DialogHeader>
          <DialogTitle>{documentName}</DialogTitle>
        </DialogHeader>

        {/* Navigation controls */}
        {numPages > 0 && (
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

        <div ref={containerRef} className="flex-1 overflow-auto flex justify-center">
          <Document file={pdfUrl} options={pdfOptions} onLoadSuccess={onDocumentLoadSuccess}>
            <Page pageNumber={currentPage} width={800} />
          </Document>
        </div>
      </DialogContent>
    </Dialog>
  );
}

export default PdfViewerModal;

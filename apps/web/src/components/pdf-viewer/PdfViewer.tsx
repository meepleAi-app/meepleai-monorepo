/**
 * PDF Viewer Component (Issue #3155)
 *
 * Reusable PDF viewer with navigation, zoom, and interactive controls.
 * Supports jump-to-page from external triggers (chat references).
 */

'use client';

import { useState, useCallback, useRef, useEffect } from 'react';
import { Document, Page, pdfjs } from 'react-pdf';
import 'react-pdf/dist/esm/Page/AnnotationLayer.css';
import 'react-pdf/dist/esm/Page/TextLayer.css';

import { PdfControls } from './PdfControls';

// Configure PDF.js worker
pdfjs.GlobalWorkerOptions.workerSrc = `//cdnjs.cloudflare.com/ajax/libs/pdf.js/${pdfjs.version}/pdf.worker.min.js`;

export interface PdfViewerProps {
  /** URL or file path of the PDF to display */
  pdfUrl: string;
  /** Initial page number to display (1-indexed) */
  initialPage?: number;
  /** Callback when page changes */
  onPageChange?: (page: number) => void;
  /** Page to highlight (triggered from chat references) */
  highlightedPage?: number;
  /** Additional CSS classes */
  className?: string;
  /** Show controls */
  showControls?: boolean;
}

export interface PdfViewerRef {
  /** Programmatically jump to a specific page */
  jumpToPage: (page: number) => void;
  /** Get current page number */
  getCurrentPage: () => number;
}

export function PdfViewer({
  pdfUrl,
  initialPage = 1,
  onPageChange,
  highlightedPage,
  className = '',
  showControls = true,
}: PdfViewerProps) {
  const [numPages, setNumPages] = useState<number>(0);
  const [currentPage, setCurrentPage] = useState<number>(initialPage);
  const [zoom, setZoom] = useState<number>(1.0);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [pageWidth, setPageWidth] = useState<number>(595); // A4 width in px
  const containerRef = useRef<HTMLDivElement>(null);

  // Handle PDF load success
  const onDocumentLoadSuccess = useCallback(
    ({ numPages: nextNumPages }: { numPages: number }) => {
      setNumPages(nextNumPages);
      setIsLoading(false);
      setError(null);
    },
    []
  );

  // Handle PDF load error
  const onDocumentLoadError = useCallback((error: Error) => {
    console.error('PDF load error:', error);
    setError('Impossibile caricare il PDF. Verifica che il file esista.');
    setIsLoading(false);
  }, []);

  // Page navigation
  const goToPage = useCallback(
    (page: number) => {
      if (page >= 1 && page <= numPages) {
        setCurrentPage(page);
        onPageChange?.(page);
      }
    },
    [numPages, onPageChange]
  );

  const goToNextPage = useCallback(() => {
    goToPage(currentPage + 1);
  }, [currentPage, goToPage]);

  const goToPreviousPage = useCallback(() => {
    goToPage(currentPage - 1);
  }, [currentPage, goToPage]);

  // Zoom controls
  const zoomIn = useCallback(() => {
    setZoom(prev => Math.min(prev + 0.25, 2.0));
  }, []);

  const zoomOut = useCallback(() => {
    setZoom(prev => Math.max(prev - 0.25, 0.5));
  }, []);

  const zoomFitWidth = useCallback(() => {
    if (containerRef.current) {
      const containerWidth = containerRef.current.offsetWidth - 64; // padding
      setPageWidth(containerWidth);
      setZoom(1.0);
    }
  }, []);

  const resetZoom = useCallback(() => {
    setZoom(1.0);
    setPageWidth(595); // A4 default
  }, []);

  // Handle highlighted page from external trigger (chat reference)
  useEffect(() => {
    if (highlightedPage && highlightedPage !== currentPage) {
      goToPage(highlightedPage);
      // Add visual feedback (page turn animation handled by CSS)
    }
  }, [highlightedPage, currentPage, goToPage]);

  // Responsive page width
  useEffect(() => {
    const handleResize = () => {
      if (containerRef.current) {
        const containerWidth = containerRef.current.offsetWidth - 64;
        setPageWidth(Math.min(containerWidth, 595));
      }
    };

    handleResize();
    window.addEventListener('resize', handleResize);
    return () => window.removeEventListener('resize', handleResize);
  }, []);

  return (
    <div className={`flex flex-col h-full bg-gray-900 ${className}`}>
      {/* PDF Controls */}
      {showControls && (
        <PdfControls
          currentPage={currentPage}
          totalPages={numPages}
          zoom={zoom}
          onPageChange={goToPage}
          onNextPage={goToNextPage}
          onPreviousPage={goToPreviousPage}
          onZoomIn={zoomIn}
          onZoomOut={zoomOut}
          onZoomFitWidth={zoomFitWidth}
          onZoomReset={resetZoom}
          pdfUrl={pdfUrl}
          disabled={isLoading || !!error}
        />
      )}

      {/* PDF Document Container */}
      <div
        ref={containerRef}
        className="flex-1 overflow-auto bg-gray-800 p-8 flex items-start justify-center"
      >
        {error ? (
          <div className="bg-red-50 border border-red-200 rounded-lg p-6 max-w-md">
            <h3 className="font-semibold text-red-900 mb-2">Errore caricamento PDF</h3>
            <p className="text-sm text-red-700">{error}</p>
          </div>
        ) : (
          <Document
            file={pdfUrl}
            onLoadSuccess={onDocumentLoadSuccess}
            onLoadError={onDocumentLoadError}
            loading={
              <div className="bg-white shadow-2xl rounded-lg p-12 animate-pulse">
                <div className="h-[842px] w-[595px] bg-gray-200 rounded"></div>
              </div>
            }
            className="pdf-document"
          >
            <Page
              pageNumber={currentPage}
              width={pageWidth * zoom}
              className="pdf-page bg-white shadow-2xl transition-all duration-300"
              renderTextLayer={true}
              renderAnnotationLayer={true}
              loading={
                <div className="bg-white shadow-2xl rounded-lg p-12 animate-pulse">
                  <div className="h-[842px] bg-gray-200 rounded"></div>
                </div>
              }
            />
          </Document>
        )}
      </div>
    </div>
  );
}

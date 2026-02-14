/**
 * PDF Viewer Component (Issue #3155, #4133, #4252)
 *
 * Reusable PDF viewer with navigation, zoom, and interactive controls.
 * Supports jump-to-page from external triggers (chat references).
 *
 * Uses react-pdf with pdfjs-dist v5 for security (GHSA-wgrm-67xf-hhpq).
 */

'use client';

import { useState, forwardRef, useImperativeHandle, useCallback, useEffect } from 'react';

import { Document, Page, pdfjs } from 'react-pdf';
import 'react-pdf/dist/Page/AnnotationLayer.css';
import 'react-pdf/dist/Page/TextLayer.css';

import { PdfControls } from './PdfControls';

pdfjs.GlobalWorkerOptions.workerSrc = new URL(
  'pdfjs-dist/build/pdf.worker.min.mjs',
  import.meta.url,
).toString();

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

const DEFAULT_ZOOM = 1.0;
const MIN_ZOOM = 0.5;
const MAX_ZOOM = 2.0;
const ZOOM_STEP = 0.1;
const BASE_WIDTH = 800;

export const PdfViewer = forwardRef<PdfViewerRef, PdfViewerProps>(function PdfViewer(
  {
    pdfUrl,
    initialPage = 1,
    onPageChange,
    highlightedPage,
    className = '',
    showControls = true,
  },
  ref
) {
  const [currentPage, setCurrentPage] = useState<number>(initialPage);
  const [numPages, setNumPages] = useState<number>(0);
  const [zoom, setZoom] = useState<number>(DEFAULT_ZOOM);

  const onDocumentLoadSuccess = useCallback(({ numPages }: { numPages: number }) => {
    setNumPages(numPages);
  }, []);

  const goToPage = useCallback((page: number) => {
    const clamped = Math.max(1, Math.min(page, numPages));
    setCurrentPage(clamped);
    onPageChange?.(clamped);
  }, [numPages, onPageChange]);

  // Expose imperative handle for external control
  useImperativeHandle(ref, () => ({
    jumpToPage: (page: number) => goToPage(page),
    getCurrentPage: () => currentPage,
  }), [goToPage, currentPage]);

  // Jump to highlighted page from external trigger
  useEffect(() => {
    if (highlightedPage && highlightedPage !== currentPage) {
      goToPage(highlightedPage);
    }
  }, [highlightedPage, currentPage, goToPage]);

  // Jump to initial page on mount
  useEffect(() => {
    if (initialPage > 1) {
      setCurrentPage(initialPage);
    }
  }, [initialPage]);

  const handleZoomIn = useCallback(() => {
    setZoom(prev => Math.min(prev + ZOOM_STEP, MAX_ZOOM));
  }, []);

  const handleZoomOut = useCallback(() => {
    setZoom(prev => Math.max(prev - ZOOM_STEP, MIN_ZOOM));
  }, []);

  const handleZoomReset = useCallback(() => {
    setZoom(DEFAULT_ZOOM);
  }, []);

  const handleZoomFitWidth = useCallback(() => {
    setZoom(DEFAULT_ZOOM);
  }, []);

  return (
    <div className={`pdf-viewer-container flex flex-col h-full ${className}`}>
      {showControls && numPages > 0 && (
        <PdfControls
          currentPage={currentPage}
          totalPages={numPages}
          zoom={zoom}
          onPageChange={goToPage}
          onNextPage={() => goToPage(currentPage + 1)}
          onPreviousPage={() => goToPage(currentPage - 1)}
          onZoomIn={handleZoomIn}
          onZoomOut={handleZoomOut}
          onZoomFitWidth={handleZoomFitWidth}
          onZoomReset={handleZoomReset}
          pdfUrl={pdfUrl}
        />
      )}
      <div className="flex-1 overflow-auto flex justify-center">
        <Document file={pdfUrl} onLoadSuccess={onDocumentLoadSuccess}>
          <Page
            pageNumber={currentPage}
            width={BASE_WIDTH * zoom}
          />
        </Document>
      </div>
    </div>
  );
});

export default PdfViewer;

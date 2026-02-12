/**
 * PDF Viewer Component (Issue #3155, #4133)
 *
 * Reusable PDF viewer with navigation, zoom, and interactive controls.
 * Supports jump-to-page from external triggers (chat references).
 *
 * Migration: react-pdf → @react-pdf-viewer/core (Issue #4133)
 * - SSR-compatible with proper dynamic imports
 * - No DOMMatrix dependency issues
 * - Modern plugin-based architecture
 */

'use client';

import { useEffect, useState, forwardRef, useImperativeHandle } from 'react';

import { Viewer, Worker } from '@react-pdf-viewer/core';
import { defaultLayoutPlugin } from '@react-pdf-viewer/default-layout';
import { pageNavigationPlugin } from '@react-pdf-viewer/page-navigation';

import '@react-pdf-viewer/core/lib/styles/index.css';
import '@react-pdf-viewer/default-layout/lib/styles/index.css';

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

  // Page navigation plugin for programmatic control
  const pageNavigationPluginInstance = pageNavigationPlugin();
  const { jumpToPage } = pageNavigationPluginInstance;

  // Default layout plugin for full controls
  const defaultLayoutPluginInstance = defaultLayoutPlugin({
    sidebarTabs: () => [],
  });

  // Use appropriate plugin based on showControls
  const plugins = showControls
    ? [defaultLayoutPluginInstance]
    : [pageNavigationPluginInstance];

  // Expose imperative handle for external control
  useImperativeHandle(ref, () => ({
    jumpToPage: (page: number) => {
      jumpToPage(page - 1); // @react-pdf-viewer uses 0-indexed
    },
    getCurrentPage: () => currentPage,
  }));

  // Handle page change events
  const handlePageChange = (e: { currentPage: number }) => {
    const pageNum = e.currentPage + 1; // Convert to 1-indexed
    setCurrentPage(pageNum);
    onPageChange?.(pageNum);
  };

  // Jump to highlighted page from external trigger
  useEffect(() => {
    if (highlightedPage && highlightedPage !== currentPage) {
      jumpToPage(highlightedPage - 1); // 0-indexed
    }
  }, [highlightedPage, currentPage, jumpToPage]);

  // Jump to initial page on mount
  useEffect(() => {
    if (initialPage > 1) {
      const timer = setTimeout(() => {
        jumpToPage(initialPage - 1); // 0-indexed
      }, 100);
      return () => clearTimeout(timer);
    }
  }, [initialPage, jumpToPage]);

  return (
    <div className={`pdf-viewer-container flex flex-col h-full ${className}`}>
      <Worker workerUrl="https://unpkg.com/pdfjs-dist@3.0.279/build/pdf.worker.min.js">
        <Viewer fileUrl={pdfUrl} plugins={plugins} onPageChange={handlePageChange} />
      </Worker>
    </div>
  );
});

export default PdfViewer;


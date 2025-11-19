'use client';

/**
 * PdfViewerModal - Modal dialog for viewing PDF files (BGAI-074)
 *
 * Features:
 * - Loads PDF from URL with authentication (cookies)
 * - Jumps to specific page on load
 * - Full PDF navigation controls
 * - Responsive modal dialog
 *
 * Usage: Display PDF when user clicks citation to jump to specific page
 */

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Document, Page, pdfjs } from 'react-pdf';
import { List } from 'react-window';
import { cn } from '@/lib/utils';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';

// Configure PDF.js worker
if (typeof window !== 'undefined') {
  pdfjs.GlobalWorkerOptions.workerSrc = `//cdnjs.cloudflare.com/ajax/libs/pdf.js/${pdfjs.version}/pdf.worker.min.js`;
}

export interface PdfViewerModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  pdfUrl: string;
  initialPage?: number;
  documentName?: string;
}

const ZOOM_LEVELS = [0.25, 0.5, 1.0, 1.5, 2.0] as const;
type ZoomLevel = (typeof ZOOM_LEVELS)[number];

const THUMBNAIL_WIDTH = 120;
const THUMBNAIL_HEIGHT = 160;
const MOBILE_BREAKPOINT = 768;

export function PdfViewerModal({
  open,
  onOpenChange,
  pdfUrl,
  initialPage = 1,
  documentName = 'PDF Document',
}: PdfViewerModalProps) {
  const [numPages, setNumPages] = useState<number | null>(null);
  const [currentPage, setCurrentPage] = useState(initialPage);
  const [zoomLevel, setZoomLevel] = useState<ZoomLevel>(1.0);
  const [pageWidth, setPageWidth] = useState(600);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [jumpToPageInput, setJumpToPageInput] = useState('');
  const [isMobile, setIsMobile] = useState(false);
  const [showThumbnails, setShowThumbnails] = useState(true);

  const mainCanvasRef = useRef<HTMLDivElement>(null);
  const thumbnailListRef = useRef<any>(null);

  // Reset to initial page when modal opens or initialPage changes
  useEffect(() => {
    if (open) {
      setCurrentPage(initialPage);
    }
  }, [open, initialPage]);

  // Detect mobile viewport
  useEffect(() => {
    const checkMobile = () => {
      const mobile = window.innerWidth < MOBILE_BREAKPOINT;
      setIsMobile(mobile);
      setShowThumbnails(!mobile);
    };

    checkMobile();
    window.addEventListener('resize', checkMobile);
    return () => window.removeEventListener('resize', checkMobile);
  }, []);

  // Update page width based on container
  useEffect(() => {
    const updateWidth = () => {
      if (mainCanvasRef.current) {
        const containerWidth = mainCanvasRef.current.clientWidth;
        setPageWidth(containerWidth - 40);
      }
    };

    updateWidth();
    window.addEventListener('resize', updateWidth);
    return () => window.removeEventListener('resize', updateWidth);
  }, []);

  // Keyboard navigation
  useEffect(() => {
    if (!open) return;

    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.target instanceof HTMLInputElement) {
        return;
      }

      switch (e.key) {
        case 'ArrowLeft':
        case 'ArrowUp':
          e.preventDefault();
          goToPreviousPage();
          break;
        case 'ArrowRight':
        case 'ArrowDown':
          e.preventDefault();
          goToNextPage();
          break;
        case '+':
        case '=':
          e.preventDefault();
          zoomIn();
          break;
        case '-':
        case '_':
          e.preventDefault();
          zoomOut();
          break;
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [open, currentPage, numPages, zoomLevel]);

  const onDocumentLoadSuccess = useCallback(({ numPages: pages }: { numPages: number }) => {
    setNumPages(pages);
    setLoading(false);
    setError(null);
  }, []);

  const onDocumentLoadError = useCallback((err: Error) => {
    console.error('PDF load error:', err);
    setError('Failed to load PDF. Please try again.');
    setLoading(false);
  }, []);

  const goToPage = useCallback((page: number) => {
    if (numPages && page >= 1 && page <= numPages) {
      setCurrentPage(page);
      if (thumbnailListRef.current && showThumbnails && typeof thumbnailListRef.current.scrollToItem === 'function') {
        thumbnailListRef.current.scrollToItem(page - 1, 'center');
      }
    }
  }, [numPages, showThumbnails]);

  const goToNextPage = useCallback(() => {
    if (numPages && currentPage < numPages) {
      goToPage(currentPage + 1);
    }
  }, [currentPage, numPages, goToPage]);

  const goToPreviousPage = useCallback(() => {
    if (currentPage > 1) {
      goToPage(currentPage - 1);
    }
  }, [currentPage, goToPage]);

  const zoomIn = useCallback(() => {
    const currentIndex = ZOOM_LEVELS.indexOf(zoomLevel);
    if (currentIndex < ZOOM_LEVELS.length - 1) {
      setZoomLevel(ZOOM_LEVELS[currentIndex + 1]);
    }
  }, [zoomLevel]);

  const zoomOut = useCallback(() => {
    const currentIndex = ZOOM_LEVELS.indexOf(zoomLevel);
    if (currentIndex > 0) {
      setZoomLevel(ZOOM_LEVELS[currentIndex - 1]);
    }
  }, [zoomLevel]);

  const handleJumpToPage = useCallback((e: React.FormEvent) => {
    e.preventDefault();
    const page = parseInt(jumpToPageInput, 10);
    if (!isNaN(page)) {
      goToPage(page);
      setJumpToPageInput('');
    }
  }, [jumpToPageInput, goToPage]);

  const handleThumbnailClick = useCallback((page: number) => {
    goToPage(page);
  }, [goToPage]);

  const renderThumbnail = useCallback((props: { index: number; style: React.CSSProperties }) => {
    const { index, style } = props;
    const pageNumber = index + 1;
    const isActive = pageNumber === currentPage;

    return (
      <div
        style={{ ...style, padding: '8px', cursor: 'pointer' }}
        onClick={() => handleThumbnailClick(pageNumber)}
        data-testid={`thumbnail-${pageNumber}`}
      >
        <div
          className={cn(
            "rounded overflow-hidden bg-white transition-colors",
            isActive ? "border-[3px] border-blue-500" : "border border-gray-300"
          )}
        >
          <Document
            file={pdfUrl}
            options={{ withCredentials: true }}
            onLoadError={() => {}}
          >
            <Page
              pageNumber={pageNumber}
              width={THUMBNAIL_WIDTH}
              renderTextLayer={false}
              renderAnnotationLayer={false}
            />
          </Document>
          <div
            className={cn(
              "text-center p-1 text-xs",
              isActive ? "text-blue-500 font-semibold bg-blue-50" : "text-gray-600 font-normal bg-gray-50"
            )}
          >
            {pageNumber}
          </div>
        </div>
      </div>
    );
  }, [currentPage, pdfUrl, handleThumbnailClick]);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-6xl h-[90vh] p-0 flex flex-col">
        <DialogHeader className="px-6 py-4 border-b">
          <DialogTitle>{documentName}</DialogTitle>
        </DialogHeader>

        {error ? (
          <div
            data-testid="pdf-viewer-error"
            className="p-6 bg-red-50 border border-red-600 rounded text-red-600 m-6"
            role="alert"
            aria-live="assertive"
          >
            <p>{error}</p>
          </div>
        ) : (
          <div className="flex flex-col flex-1 overflow-hidden">
            {/* Toolbar */}
            <div
              className="flex flex-wrap gap-3 p-3 bg-white border-b border-gray-300 items-center justify-between"
              role="toolbar"
              aria-label="PDF viewer controls"
            >
              {/* Zoom controls */}
              <div className="flex gap-2 items-center flex-wrap">
                <button
                  onClick={zoomOut}
                  disabled={zoomLevel === ZOOM_LEVELS[0]}
                  data-testid="zoom-out"
                  aria-label="Zoom out"
                  className={cn(
                    "px-3 py-1.5 border border-gray-300 rounded font-medium text-sm",
                    zoomLevel === ZOOM_LEVELS[0]
                      ? "bg-gray-300 cursor-not-allowed"
                      : "bg-white cursor-pointer hover:bg-gray-50"
                  )}
                >
                  -
                </button>
                {ZOOM_LEVELS.map((level) => (
                  <button
                    key={level}
                    onClick={() => setZoomLevel(level)}
                    data-testid={`zoom-${level * 100}`}
                    aria-label={`Zoom ${level * 100}%`}
                    aria-pressed={zoomLevel === level}
                    className={cn(
                      "px-3 py-1.5 border border-gray-300 rounded cursor-pointer text-xs",
                      zoomLevel === level
                        ? "bg-blue-500 text-white font-semibold"
                        : "bg-white text-black font-normal hover:bg-gray-50"
                    )}
                  >
                    {level * 100}%
                  </button>
                ))}
                <button
                  onClick={zoomIn}
                  disabled={zoomLevel === ZOOM_LEVELS[ZOOM_LEVELS.length - 1]}
                  data-testid="zoom-in"
                  aria-label="Zoom in"
                  className={cn(
                    "px-3 py-1.5 border border-gray-300 rounded font-medium text-sm",
                    zoomLevel === ZOOM_LEVELS[ZOOM_LEVELS.length - 1]
                      ? "bg-gray-300 cursor-not-allowed"
                      : "bg-white cursor-pointer hover:bg-gray-50"
                  )}
                >
                  +
                </button>
              </div>

              {/* Thumbnail toggle (mobile) */}
              {isMobile && (
                <button
                  onClick={() => setShowThumbnails(!showThumbnails)}
                  data-testid="toggle-thumbnails"
                  aria-label={showThumbnails ? 'Hide thumbnails' : 'Show thumbnails'}
                  aria-pressed={showThumbnails}
                  className={cn(
                    "px-3 py-1.5 border border-gray-300 rounded cursor-pointer font-medium text-sm",
                    showThumbnails
                      ? "bg-blue-500 text-white"
                      : "bg-white text-black hover:bg-gray-50"
                  )}
                >
                  Pages
                </button>
              )}
            </div>

            {/* Main content area */}
            <div className="flex flex-1 overflow-hidden">
              {/* Thumbnail sidebar */}
              {showThumbnails && numPages && (
                <div
                  data-testid="thumbnail-sidebar"
                  className={cn(
                    "border-gray-300 bg-white overflow-hidden",
                    isMobile ? "w-full absolute z-10 h-full" : `w-[${THUMBNAIL_WIDTH + 32}px] relative z-[1] border-r`
                  )}
                  role="navigation"
                  aria-label="Page thumbnails"
                >
                  <List<{}>
                    listRef={thumbnailListRef}
                    defaultHeight={isMobile ? window.innerHeight - 120 : window.innerHeight - 200}
                    rowCount={numPages}
                    rowHeight={THUMBNAIL_HEIGHT + 16}
                    rowComponent={renderThumbnail}
                    rowProps={{}}
                    style={{ width: THUMBNAIL_WIDTH + 32 }}
                  />
                </div>
              )}

              {/* Main preview */}
              {(!showThumbnails || !isMobile) && (
                <div
                  ref={mainCanvasRef}
                  className="flex-1 flex flex-col overflow-auto bg-[#525659] p-5"
                >
                  {loading && (
                    <div
                      data-testid="pdf-loading"
                      className="flex-1 flex items-center justify-center text-white"
                      role="status"
                      aria-live="polite"
                    >
                      <div>Loading PDF...</div>
                    </div>
                  )}

                  <div className="flex justify-center" style={{ minHeight: loading ? 0 : 'auto' }}>
                    <Document
                      file={pdfUrl}
                      options={{ withCredentials: true }}
                      onLoadSuccess={onDocumentLoadSuccess}
                      onLoadError={onDocumentLoadError}
                      loading=""
                    >
                      <Page
                        pageNumber={currentPage}
                        width={pageWidth * zoomLevel}
                        renderTextLayer={true}
                        renderAnnotationLayer={true}
                        loading={
                          <div
                            data-testid="page-loading"
                            className="bg-white flex items-center justify-center rounded"
                            style={{
                              width: pageWidth * zoomLevel,
                              height: pageWidth * 1.4 * zoomLevel
                            }}
                            role="status"
                            aria-live="polite"
                          >
                            Loading page...
                          </div>
                        }
                      />
                    </Document>
                  </div>

                  {/* Page navigation (bottom) */}
                  {numPages && (
                    <div
                      className="flex gap-3 items-center justify-center mt-5 p-3 bg-white/95 rounded-md flex-wrap"
                      role="navigation"
                      aria-label="Page navigation"
                    >
                      <button
                        onClick={goToPreviousPage}
                        disabled={currentPage === 1}
                        data-testid="prev-page"
                        aria-label="Previous page"
                        className={cn(
                          "px-4 py-2 border-0 rounded font-medium text-sm",
                          currentPage === 1
                            ? "bg-gray-300 text-gray-600 cursor-not-allowed"
                            : "bg-blue-500 text-white cursor-pointer hover:bg-blue-600"
                        )}
                      >
                        Previous
                      </button>

                      <span
                        data-testid="current-page"
                        className="text-sm font-medium text-gray-800"
                        aria-live="polite"
                        aria-atomic="true"
                      >
                        Page {currentPage} of {numPages}
                      </span>

                      <button
                        onClick={goToNextPage}
                        disabled={currentPage === numPages}
                        data-testid="next-page"
                        aria-label="Next page"
                        className={cn(
                          "px-4 py-2 border-0 rounded font-medium text-sm",
                          currentPage === numPages
                            ? "bg-gray-300 text-gray-600 cursor-not-allowed"
                            : "bg-blue-500 text-white cursor-pointer hover:bg-blue-600"
                        )}
                      >
                        Next
                      </button>

                      <form onSubmit={handleJumpToPage} className="flex gap-2 items-center">
                        <label htmlFor="jump-to-page" className="text-sm text-gray-800">
                          Jump to:
                        </label>
                        <input
                          id="jump-to-page"
                          type="number"
                          min={1}
                          max={numPages}
                          value={jumpToPageInput}
                          onChange={(e) => setJumpToPageInput(e.target.value)}
                          data-testid="jump-to-page-input"
                          aria-label="Jump to page number"
                          placeholder="Page #"
                          className="w-[70px] px-2 py-1.5 border border-gray-300 rounded text-sm"
                        />
                        <button
                          type="submit"
                          data-testid="jump-to-page-button"
                          aria-label="Go to page"
                          className="px-3 py-1.5 bg-green-600 text-white border-0 rounded cursor-pointer font-medium text-sm"
                        >
                          Go
                        </button>
                      </form>
                    </div>
                  )}
                </div>
              )}
            </div>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}

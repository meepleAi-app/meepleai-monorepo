import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Document, Page, pdfjs } from 'react-pdf';
import { List } from 'react-window';
// Note: CSS imports commented out due to module resolution issues in Next.js 15.5.4
// import 'react-pdf/dist/esm/Page/AnnotationLayer.css';
// import 'react-pdf/dist/esm/Page/TextLayer.css';

// Configure PDF.js worker
if (typeof window !== 'undefined') {
  pdfjs.GlobalWorkerOptions.workerSrc = `//cdnjs.cloudflare.com/ajax/libs/pdf.js/${pdfjs.version}/pdf.worker.min.js`;
}

interface PdfPreviewProps {
  file: File;
  onClose?: () => void;
}

const ZOOM_LEVELS = [0.25, 0.5, 1.0, 1.5, 2.0] as const;
type ZoomLevel = (typeof ZOOM_LEVELS)[number];

const THUMBNAIL_WIDTH = 120;
const THUMBNAIL_HEIGHT = 160;
const MOBILE_BREAKPOINT = 768;

export function PdfPreview({ file, onClose }: PdfPreviewProps) {
  const [numPages, setNumPages] = useState<number | null>(null);
  const [currentPage, setCurrentPage] = useState(1);
  const [zoomLevel, setZoomLevel] = useState<ZoomLevel>(1.0);
  const [pageWidth, setPageWidth] = useState(600);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [jumpToPageInput, setJumpToPageInput] = useState('');
  const [isMobile, setIsMobile] = useState(false);
  const [showThumbnails, setShowThumbnails] = useState(true);

  const mainCanvasRef = useRef<HTMLDivElement>(null);
  const thumbnailListRef = useRef<any>(null);
  const fileUrl = useMemo(() => URL.createObjectURL(file), [file]);

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

  // Cleanup URL on unmount
  useEffect(() => {
    return () => {
      URL.revokeObjectURL(fileUrl);
    };
  }, [fileUrl]);

  // Update page width based on container
  useEffect(() => {
    const updateWidth = () => {
      if (mainCanvasRef.current) {
        const containerWidth = mainCanvasRef.current.clientWidth;
        setPageWidth(containerWidth - 40); // Subtract padding
      }
    };

    updateWidth();
    window.addEventListener('resize', updateWidth);
    return () => window.removeEventListener('resize', updateWidth);
  }, []);

  // Keyboard navigation
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.target instanceof HTMLInputElement) {
        return; // Don't handle if user is typing in input
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
        case 'Escape':
          if (onClose) {
            e.preventDefault();
            onClose();
          }
          break;
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [currentPage, numPages, zoomLevel, onClose]);

  const onDocumentLoadSuccess = useCallback(({ numPages: pages }: { numPages: number }) => {
    setNumPages(pages);
    setLoading(false);
    setError(null);
  }, []);

  const onDocumentLoadError = useCallback((err: Error) => {
    console.error('PDF load error:', err);
    setError('Failed to load PDF. The file may be corrupted or in an unsupported format.');
    setLoading(false);
  }, []);

  const goToPage = useCallback((page: number) => {
    if (numPages && page >= 1 && page <= numPages) {
      setCurrentPage(page);
      // Scroll thumbnail list to show current page
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
        style={{
          ...style,
          padding: '8px',
          cursor: 'pointer'
        }}
        onClick={() => handleThumbnailClick(pageNumber)}
        data-testid={`thumbnail-${pageNumber}`}
      >
        <div
          style={{
            border: isActive ? '3px solid #0070f3' : '1px solid #ddd',
            borderRadius: '4px',
            overflow: 'hidden',
            backgroundColor: '#fff',
            transition: 'border-color 0.2s'
          }}
        >
          <Document file={fileUrl} onLoadError={() => {}}>
            <Page
              pageNumber={pageNumber}
              width={THUMBNAIL_WIDTH}
              renderTextLayer={false}
              renderAnnotationLayer={false}
            />
          </Document>
          <div
            style={{
              textAlign: 'center',
              padding: '4px',
              fontSize: '12px',
              color: isActive ? '#0070f3' : '#666',
              fontWeight: isActive ? 600 : 400,
              backgroundColor: isActive ? '#f0f7ff' : '#f9fafb'
            }}
          >
            {pageNumber}
          </div>
        </div>
      </div>
    );
  }, [currentPage, fileUrl, handleThumbnailClick]);

  if (error) {
    return (
      <div
        data-testid="pdf-preview-error"
        style={{
          padding: '24px',
          backgroundColor: '#ffebee',
          border: '1px solid #d93025',
          borderRadius: '4px',
          color: '#d93025'
        }}
        role="alert"
        aria-live="assertive"
      >
        <h3 style={{ marginTop: 0 }}>Error loading PDF</h3>
        <p>{error}</p>
        {onClose && (
          <button
            onClick={onClose}
            style={{
              marginTop: '12px',
              padding: '8px 16px',
              backgroundColor: '#d93025',
              color: 'white',
              border: 'none',
              borderRadius: '4px',
              cursor: 'pointer',
              fontWeight: 500
            }}
          >
            Close Preview
          </button>
        )}
      </div>
    );
  }

  return (
    <div
      data-testid="pdf-preview"
      style={{
        display: 'flex',
        flexDirection: 'column',
        height: '600px',
        border: '1px solid #e0e0e0',
        borderRadius: '6px',
        backgroundColor: '#f9fafb',
        overflow: 'hidden'
      }}
    >
      {/* Toolbar */}
      <div
        style={{
          display: 'flex',
          flexWrap: 'wrap',
          gap: '12px',
          padding: '12px 16px',
          backgroundColor: '#fff',
          borderBottom: '1px solid #e0e0e0',
          alignItems: 'center',
          justifyContent: 'space-between'
        }}
        role="toolbar"
        aria-label="PDF preview controls"
      >
        {/* Zoom controls */}
        <div style={{ display: 'flex', gap: '8px', alignItems: 'center', flexWrap: 'wrap' }}>
          <button
            onClick={zoomOut}
            disabled={zoomLevel === ZOOM_LEVELS[0]}
            data-testid="zoom-out"
            aria-label="Zoom out"
            style={{
              padding: '6px 12px',
              backgroundColor: zoomLevel === ZOOM_LEVELS[0] ? '#e0e0e0' : '#fff',
              border: '1px solid #ddd',
              borderRadius: '4px',
              cursor: zoomLevel === ZOOM_LEVELS[0] ? 'not-allowed' : 'pointer',
              fontWeight: 500
            }}
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
              style={{
                padding: '6px 12px',
                backgroundColor: zoomLevel === level ? '#0070f3' : '#fff',
                color: zoomLevel === level ? '#fff' : '#000',
                border: '1px solid #ddd',
                borderRadius: '4px',
                cursor: 'pointer',
                fontWeight: zoomLevel === level ? 600 : 400,
                fontSize: '13px'
              }}
            >
              {level * 100}%
            </button>
          ))}
          <button
            onClick={zoomIn}
            disabled={zoomLevel === ZOOM_LEVELS[ZOOM_LEVELS.length - 1]}
            data-testid="zoom-in"
            aria-label="Zoom in"
            style={{
              padding: '6px 12px',
              backgroundColor: zoomLevel === ZOOM_LEVELS[ZOOM_LEVELS.length - 1] ? '#e0e0e0' : '#fff',
              border: '1px solid #ddd',
              borderRadius: '4px',
              cursor: zoomLevel === ZOOM_LEVELS[ZOOM_LEVELS.length - 1] ? 'not-allowed' : 'pointer',
              fontWeight: 500
            }}
          >
            +
          </button>
          <span data-testid="zoom-level" style={{ fontSize: '13px', color: '#666', marginLeft: '8px' }}>
            {zoomLevel * 100}%
          </span>
        </div>

        {/* Thumbnail toggle (mobile) */}
        {isMobile && (
          <button
            onClick={() => setShowThumbnails(!showThumbnails)}
            data-testid="toggle-thumbnails"
            aria-label={showThumbnails ? 'Hide thumbnails' : 'Show thumbnails'}
            aria-pressed={showThumbnails}
            style={{
              padding: '6px 12px',
              backgroundColor: showThumbnails ? '#0070f3' : '#fff',
              color: showThumbnails ? '#fff' : '#000',
              border: '1px solid #ddd',
              borderRadius: '4px',
              cursor: 'pointer',
              fontWeight: 500,
              fontSize: '13px'
            }}
          >
            Pages
          </button>
        )}

        {/* Close button */}
        {onClose && (
          <button
            onClick={onClose}
            data-testid="close-preview"
            aria-label="Close preview"
            style={{
              padding: '6px 12px',
              backgroundColor: '#666',
              color: 'white',
              border: 'none',
              borderRadius: '4px',
              cursor: 'pointer',
              fontWeight: 500,
              fontSize: '13px'
            }}
          >
            Close
          </button>
        )}
      </div>

      {/* Main content area */}
      <div style={{ display: 'flex', flex: 1, overflow: 'hidden' }}>
        {/* Thumbnail sidebar */}
        {showThumbnails && numPages && (
          <div
            data-testid="thumbnail-sidebar"
            style={{
              width: isMobile ? '100%' : `${THUMBNAIL_WIDTH + 32}px`,
              borderRight: isMobile ? 'none' : '1px solid #e0e0e0',
              backgroundColor: '#fff',
              overflow: 'hidden',
              position: isMobile ? 'absolute' : 'relative',
              zIndex: isMobile ? 10 : 1,
              height: isMobile ? '100%' : 'auto'
            }}
            role="navigation"
            aria-label="Page thumbnails"
          >
            <List<{}>
              listRef={thumbnailListRef}
              defaultHeight={isMobile ? window.innerHeight - 120 : 600 - 60}
              rowCount={numPages}
              rowHeight={THUMBNAIL_HEIGHT + 16}
              rowComponent={renderThumbnail}
              rowProps={{} as any}
            />
          </div>
        )}

        {/* Main preview */}
        {(!showThumbnails || !isMobile) && (
          <div
            ref={mainCanvasRef}
            style={{
              flex: 1,
              display: 'flex',
              flexDirection: 'column',
              overflow: 'auto',
              backgroundColor: '#525659',
              padding: '20px'
            }}
          >
            {loading && (
              <div
                data-testid="pdf-loading"
                style={{
                  flex: 1,
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  color: '#fff'
                }}
                role="status"
                aria-live="polite"
              >
                <div>Loading PDF...</div>
              </div>
            )}

            <div
              style={{
                display: 'flex',
                justifyContent: 'center',
                minHeight: loading ? 0 : 'auto'
              }}
            >
              <Document
                file={fileUrl}
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
                      style={{
                        width: pageWidth * zoomLevel,
                        height: pageWidth * 1.4 * zoomLevel,
                        backgroundColor: '#fff',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        borderRadius: '4px'
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
                style={{
                  display: 'flex',
                  gap: '12px',
                  alignItems: 'center',
                  justifyContent: 'center',
                  marginTop: '20px',
                  padding: '12px',
                  backgroundColor: 'rgba(255, 255, 255, 0.95)',
                  borderRadius: '6px',
                  flexWrap: 'wrap'
                }}
                role="navigation"
                aria-label="Page navigation"
              >
                <button
                  onClick={goToPreviousPage}
                  disabled={currentPage === 1}
                  data-testid="prev-page"
                  aria-label="Previous page"
                  style={{
                    padding: '8px 16px',
                    backgroundColor: currentPage === 1 ? '#e0e0e0' : '#0070f3',
                    color: currentPage === 1 ? '#999' : 'white',
                    border: 'none',
                    borderRadius: '4px',
                    cursor: currentPage === 1 ? 'not-allowed' : 'pointer',
                    fontWeight: 500
                  }}
                >
                  Previous
                </button>

                <span
                  data-testid="current-page"
                  style={{
                    fontSize: '14px',
                    fontWeight: 500,
                    color: '#333'
                  }}
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
                  style={{
                    padding: '8px 16px',
                    backgroundColor: currentPage === numPages ? '#e0e0e0' : '#0070f3',
                    color: currentPage === numPages ? '#999' : 'white',
                    border: 'none',
                    borderRadius: '4px',
                    cursor: currentPage === numPages ? 'not-allowed' : 'pointer',
                    fontWeight: 500
                  }}
                >
                  Next
                </button>

                <form onSubmit={handleJumpToPage} style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
                  <label
                    htmlFor="jump-to-page"
                    style={{
                      fontSize: '14px',
                      color: '#333'
                    }}
                  >
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
                    style={{
                      width: '70px',
                      padding: '6px 8px',
                      border: '1px solid #ddd',
                      borderRadius: '4px',
                      fontSize: '14px'
                    }}
                  />
                  <button
                    type="submit"
                    data-testid="jump-to-page-button"
                    aria-label="Go to page"
                    style={{
                      padding: '6px 12px',
                      backgroundColor: '#34a853',
                      color: 'white',
                      border: 'none',
                      borderRadius: '4px',
                      cursor: 'pointer',
                      fontWeight: 500,
                      fontSize: '14px'
                    }}
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
  );
}

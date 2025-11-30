/**
 * Test helpers for PdfPreview component tests
 * Shared mocks, utilities, and test setup
 */

import { vi } from 'vitest';

// Mock react-pdf
export const mockReactPdf = () => {
  vi.mock('react-pdf', () => {
    const React = require('react');

    const MockDocument = React.forwardRef(({ file, onLoadSuccess, onLoadError, children }: any, ref: any) => {
      // Simulate successful load after a short delay
      React.useEffect(() => {
        const timer = setTimeout(() => {
          if (file && onLoadSuccess) {
            onLoadSuccess({ numPages: 10 });
          }
        }, 100);
        return () => clearTimeout(timer);
      }, [file, onLoadSuccess]);

      return <div data-testid="pdf-document" ref={ref}>{children}</div>;
    });
    MockDocument.displayName = 'MockDocument';

    const MockPage = React.forwardRef(({ pageNumber, width, renderTextLayer, renderAnnotationLayer, loading }: any, ref: any) => (
      <div
        ref={ref}
        data-testid={`pdf-page-${pageNumber}`}
        data-page-number={pageNumber}
        data-width={width}
        data-render-text={renderTextLayer}
        data-render-annotation={renderAnnotationLayer}
      >
        {loading || `Page ${pageNumber}`}
      </div>
    ));
    MockPage.displayName = 'MockPage';

    return {
      Document: MockDocument,
      Page: MockPage,
      pdfjs: {
        version: '3.11.174',
        GlobalWorkerOptions: {
          workerSrc: ''
        }
      }
    };
  });
};

// Mock react-window
export const mockReactWindow = () => {
  vi.mock('react-window', () => {
    const React = require('react');

    const MockList = React.forwardRef(({ rowComponent, rowCount, rowHeight, defaultHeight, listRef }: any, ref: any) => {
      // Handle listRef callback
      React.useEffect(() => {
        if (listRef && typeof listRef === 'function') {
          const api = { scrollToItem: vi.fn() };
          listRef(api);
        } else if (listRef && typeof listRef === 'object') {
          listRef.current = { scrollToItem: vi.fn() };
        }
      }, [listRef]);

      return (
        <div
          ref={ref}
          data-testid="thumbnail-list"
          data-item-count={rowCount}
          data-item-size={rowHeight}
          data-height={defaultHeight}
        >
          {Array.from({ length: rowCount }).map((_, index) => {
            const RowComponent = rowComponent;
            return <div key={index}><RowComponent index={index} style={{}} /></div>;
          })}
        </div>
      );
    });
    MockList.displayName = 'MockList';

    return {
      List: MockList
    };
  });
};

// Setup global mocks
export const setupGlobalMocks = () => {
  // Mock URL.createObjectURL and URL.revokeObjectURL
  global.URL.createObjectURL = vi.fn(() => 'mock-url');
  global.URL.revokeObjectURL = vi.fn();

  // Mock window.innerWidth for mobile detection
  Object.defineProperty(window, 'innerWidth', {
    writable: true,
    configurable: true,
    value: 1024
  });
};

// Create mock PDF file
export const createMockPdfFile = (name = 'test.pdf', content = 'mock pdf content') => {
  return new File([content], name, { type: 'application/pdf' });
};

// Set viewport size
export const setViewportSize = (width: number) => {
  Object.defineProperty(window, 'innerWidth', {
    writable: true,
    configurable: true,
    value: width
  });
};

// Constants
export const DESKTOP_WIDTH = 1024;
export const MOBILE_WIDTH = 500;
export const DEFAULT_NUM_PAGES = 10;

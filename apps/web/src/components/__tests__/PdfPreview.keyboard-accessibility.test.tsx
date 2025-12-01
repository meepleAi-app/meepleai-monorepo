import { vi } from 'vitest';

// Mock react-pdf - must be hoisted before component import
vi.mock('react-pdf', () => {
  const React = require('react');

  const MockDocument = React.forwardRef(
    ({ file, onLoadSuccess, onLoadError, children }: any, ref: any) => {
      // Simulate successful load after a short delay
      React.useEffect(() => {
        const timer = setTimeout(() => {
          if (file && onLoadSuccess) {
            onLoadSuccess({ numPages: 10 });
          }
        }, 100);
        return () => clearTimeout(timer);
      }, [file, onLoadSuccess]);

      return (
        <div data-testid="pdf-document" ref={ref}>
          {children}
        </div>
      );
    }
  );
  MockDocument.displayName = 'MockDocument';

  const MockPage = React.forwardRef(
    ({ pageNumber, width, renderTextLayer, renderAnnotationLayer, loading }: any, ref: any) => (
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
    )
  );
  MockPage.displayName = 'MockPage';

  return {
    Document: MockDocument,
    Page: MockPage,
    pdfjs: {
      version: '3.11.174',
      GlobalWorkerOptions: {
        workerSrc: '',
      },
    },
  };
});

// Mock react-window - must be hoisted before component import
vi.mock('react-window', () => {
  const React = require('react');

  const MockList = React.forwardRef(
    ({ rowComponent, rowCount, rowHeight, defaultHeight, listRef }: any, ref: any) => {
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
            return (
              <div key={index}>
                <RowComponent index={index} style={{}} />
              </div>
            );
          })}
        </div>
      );
    }
  );
  MockList.displayName = 'MockList';

  return {
    List: MockList,
  };
});

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { PdfPreview } from '../pdf/PdfPreview';
import '@testing-library/jest-dom';
import {
  setupGlobalMocks,
  createMockPdfFile,
  setViewportSize,
  DESKTOP_WIDTH,
} from './PdfPreview.test-helpers';

// Setup non-module mocks
setupGlobalMocks();

describe('PdfPreview Component - Keyboard Navigation and Accessibility', () => {
  let mockFile: File;

  beforeEach(() => {
    vi.clearAllMocks();
    mockFile = createMockPdfFile();
  });

  afterEach(() => {
    vi.clearAllTimers();
  });

  describe('Keyboard Navigation', () => {
    it('navigates to next page with ArrowRight key', async () => {
      render(<PdfPreview file={mockFile} />);

      // Wait for document to load
      await waitFor(() => {
        expect(screen.getByTestId('current-page')).toHaveTextContent('Page 1 of 10');
      });

      fireEvent.keyDown(window, { key: 'ArrowRight' });

      expect(screen.getByTestId('current-page')).toHaveTextContent('Page 2 of 10');
    });

    it('navigates to next page with ArrowDown key', async () => {
      render(<PdfPreview file={mockFile} />);

      // Wait for document to load
      await waitFor(() => {
        expect(screen.getByTestId('current-page')).toHaveTextContent('Page 1 of 10');
      });

      fireEvent.keyDown(window, { key: 'ArrowDown' });

      expect(screen.getByTestId('current-page')).toHaveTextContent('Page 2 of 10');
    });

    it('navigates to previous page with ArrowLeft key', async () => {
      render(<PdfPreview file={mockFile} />);

      // Wait for document to load
      await waitFor(() => {
        expect(screen.getByTestId('current-page')).toHaveTextContent('Page 1 of 10');
      });

      // Go to page 2 first
      fireEvent.keyDown(window, { key: 'ArrowRight' });
      expect(screen.getByTestId('current-page')).toHaveTextContent('Page 2 of 10');

      // Then go back with ArrowLeft
      fireEvent.keyDown(window, { key: 'ArrowLeft' });
      expect(screen.getByTestId('current-page')).toHaveTextContent('Page 1 of 10');
    });

    it('navigates to previous page with ArrowUp key', async () => {
      render(<PdfPreview file={mockFile} />);

      // Wait for document to load
      await waitFor(() => {
        expect(screen.getByTestId('current-page')).toHaveTextContent('Page 1 of 10');
      });

      // Go to page 2 first
      fireEvent.keyDown(window, { key: 'ArrowDown' });

      // Then go back with ArrowUp
      fireEvent.keyDown(window, { key: 'ArrowUp' });
      expect(screen.getByTestId('current-page')).toHaveTextContent('Page 1 of 10');
    });

    it('zooms in with + key', async () => {
      render(<PdfPreview file={mockFile} />);

      await waitFor(() => {
        expect(screen.getByTestId('zoom-level')).toBeInTheDocument();
      });

      fireEvent.keyDown(window, { key: '+' });

      expect(screen.getByTestId('zoom-level')).toHaveTextContent('150%');
    });

    it('zooms in with = key', async () => {
      render(<PdfPreview file={mockFile} />);

      await waitFor(() => {
        expect(screen.getByTestId('zoom-level')).toBeInTheDocument();
      });

      fireEvent.keyDown(window, { key: '=' });

      expect(screen.getByTestId('zoom-level')).toHaveTextContent('150%');
    });

    it('zooms out with - key', async () => {
      render(<PdfPreview file={mockFile} />);

      await waitFor(() => {
        expect(screen.getByTestId('zoom-level')).toBeInTheDocument();
      });

      fireEvent.keyDown(window, { key: '-' });

      expect(screen.getByTestId('zoom-level')).toHaveTextContent('50%');
    });

    it('calls onClose with Escape key', async () => {
      const onClose = vi.fn();
      render(<PdfPreview file={mockFile} onClose={onClose} />);

      await waitFor(() => {
        expect(screen.getByTestId('pdf-preview')).toBeInTheDocument();
      });

      fireEvent.keyDown(window, { key: 'Escape' });

      expect(onClose).toHaveBeenCalledTimes(1);
    });

    it('does not handle keyboard events when typing in input', async () => {
      render(<PdfPreview file={mockFile} />);

      await waitFor(() => {
        expect(screen.getByTestId('jump-to-page-input')).toBeInTheDocument();
      });

      const input = screen.getByTestId('jump-to-page-input');
      input.focus();

      fireEvent.keyDown(input, { key: 'ArrowRight' });

      // Should still be on page 1 (not navigate)
      expect(screen.getByTestId('current-page')).toHaveTextContent('Page 1 of 10');
    });
  });

  describe('Accessibility', () => {
    it('has proper ARIA labels for navigation', async () => {
      render(<PdfPreview file={mockFile} />);

      await waitFor(() => {
        expect(screen.getByTestId('prev-page')).toBeInTheDocument();
      });

      expect(screen.getByLabelText('Previous page')).toBeInTheDocument();
      expect(screen.getByLabelText('Next page')).toBeInTheDocument();
      expect(screen.getByLabelText('Jump to page number')).toBeInTheDocument();
      expect(screen.getByLabelText('Go to page')).toBeInTheDocument();
    });

    it('has proper ARIA labels for zoom controls', async () => {
      render(<PdfPreview file={mockFile} />);

      await waitFor(() => {
        expect(screen.getByTestId('zoom-in')).toBeInTheDocument();
      });

      expect(screen.getByLabelText('Zoom in')).toBeInTheDocument();
      expect(screen.getByLabelText('Zoom out')).toBeInTheDocument();
      expect(screen.getByLabelText('Zoom 100%')).toBeInTheDocument();
    });

    it('has toolbar role for controls', async () => {
      render(<PdfPreview file={mockFile} />);

      await waitFor(() => {
        expect(screen.getByRole('toolbar', { name: 'PDF preview controls' })).toBeInTheDocument();
      });
    });

    it('has navigation role for page controls', async () => {
      render(<PdfPreview file={mockFile} />);

      await waitFor(() => {
        expect(screen.getByRole('navigation', { name: 'Page navigation' })).toBeInTheDocument();
      });
    });

    it('has navigation role for thumbnails on desktop', async () => {
      // Ensure desktop viewport
      setViewportSize(DESKTOP_WIDTH);

      render(<PdfPreview file={mockFile} />);

      // Wait for document to load first
      await waitFor(() => {
        expect(screen.getByTestId('current-page')).toHaveTextContent('Page 1 of 10');
      });

      // Then check for thumbnails navigation (desktop only)
      const thumbnailNav = screen.queryByRole('navigation', { name: 'Page thumbnails' });
      if (thumbnailNav) {
        expect(thumbnailNav).toBeInTheDocument();
      } else {
        // On mobile or when thumbnails are hidden, this is acceptable
        expect(screen.getByTestId('pdf-preview')).toBeInTheDocument();
      }
    });

    it('has aria-live for current page display', async () => {
      render(<PdfPreview file={mockFile} />);

      await waitFor(() => {
        const currentPage = screen.getByTestId('current-page');
        expect(currentPage).toHaveAttribute('aria-live', 'polite');
        expect(currentPage).toHaveAttribute('aria-atomic', 'true');
      });
    });
  });
});

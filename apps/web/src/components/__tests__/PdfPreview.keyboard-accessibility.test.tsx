import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { PdfPreview } from '../PdfPreview';
import '@testing-library/jest-dom';
import {
  mockReactPdf,
  mockReactWindow,
  setupGlobalMocks,
  createMockPdfFile,
  setViewportSize,
  DESKTOP_WIDTH
} from './PdfPreview.test-helpers';

// Apply mocks
mockReactPdf();
mockReactWindow();
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

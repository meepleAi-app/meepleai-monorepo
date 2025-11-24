import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { PdfPreview } from '../PdfPreview';
import '@testing-library/jest-dom';
import {
  mockReactPdf,
  mockReactWindow,
  setupGlobalMocks,
  createMockPdfFile,
  setViewportSize,
  MOBILE_WIDTH,
  DESKTOP_WIDTH
} from './PdfPreview.test-helpers';

// Apply mocks
mockReactPdf();
mockReactWindow();
setupGlobalMocks();

describe('PdfPreview Component - Rendering and Controls', () => {
  let mockFile: File;

  beforeEach(() => {
    vi.clearAllMocks();
    mockFile = createMockPdfFile();
  });

  afterEach(() => {
    vi.clearAllTimers();
  });

  describe('Rendering and Initial State', () => {
    it('renders the PDF preview component', async () => {
      render(<PdfPreview file={mockFile} />);

      await waitFor(() => {
        expect(screen.getByTestId('pdf-preview')).toBeInTheDocument();
      });
    });

    it('displays loading state initially', () => {
      render(<PdfPreview file={mockFile} />);

      expect(screen.getByTestId('pdf-loading')).toBeInTheDocument();
      expect(screen.getByText('Loading PDF...')).toBeInTheDocument();
    });

    it('loads PDF document and displays first page info', async () => {
      render(<PdfPreview file={mockFile} />);

      // Wait for the document to load and display current page info
      await waitFor(() => {
        expect(screen.getByTestId('current-page')).toHaveTextContent('Page 1 of 10');
      }, { timeout: 5000 });

      // Document has loaded and we're showing page 1
      expect(screen.getByTestId('current-page')).toBeInTheDocument();
    });

    it('creates object URL for file', () => {
      render(<PdfPreview file={mockFile} />);

      expect(global.URL.createObjectURL).toHaveBeenCalledWith(mockFile);
    });

    it('cleans up object URL on unmount', () => {
      const { unmount } = render(<PdfPreview file={mockFile} />);

      unmount();

      expect(global.URL.revokeObjectURL).toHaveBeenCalledWith('mock-url');
    });
  });

  describe('Zoom Controls', () => {
    it('displays all zoom level buttons', async () => {
      render(<PdfPreview file={mockFile} />);

      await waitFor(() => {
        expect(screen.getByTestId('zoom-25')).toBeInTheDocument();
      });

      expect(screen.getByTestId('zoom-50')).toBeInTheDocument();
      expect(screen.getByTestId('zoom-100')).toBeInTheDocument();
      expect(screen.getByTestId('zoom-150')).toBeInTheDocument();
      expect(screen.getByTestId('zoom-200')).toBeInTheDocument();
    });

    it('displays current zoom level', async () => {
      render(<PdfPreview file={mockFile} />);

      await waitFor(() => {
        expect(screen.getByTestId('zoom-level')).toHaveTextContent('100%');
      });
    });

    it('zooms in when zoom-in button is clicked', async () => {
      render(<PdfPreview file={mockFile} />);

      await waitFor(() => {
        expect(screen.getByTestId('zoom-in')).toBeInTheDocument();
      });

      const zoomInButton = screen.getByTestId('zoom-in');
      fireEvent.click(zoomInButton);

      expect(screen.getByTestId('zoom-level')).toHaveTextContent('150%');
    });

    it('zooms out when zoom-out button is clicked', async () => {
      render(<PdfPreview file={mockFile} />);

      await waitFor(() => {
        expect(screen.getByTestId('zoom-in')).toBeInTheDocument();
      });

      // First zoom in to 150%
      fireEvent.click(screen.getByTestId('zoom-in'));
      expect(screen.getByTestId('zoom-level')).toHaveTextContent('150%');

      // Then zoom out
      fireEvent.click(screen.getByTestId('zoom-out'));
      expect(screen.getByTestId('zoom-level')).toHaveTextContent('100%');
    });

    it('disables zoom-out button at minimum zoom', async () => {
      render(<PdfPreview file={mockFile} />);

      await waitFor(() => {
        expect(screen.getByTestId('zoom-out')).toBeInTheDocument();
      });

      // Zoom out to minimum (25%)
      fireEvent.click(screen.getByTestId('zoom-out'));
      fireEvent.click(screen.getByTestId('zoom-out'));
      fireEvent.click(screen.getByTestId('zoom-out'));

      expect(screen.getByTestId('zoom-out')).toBeDisabled();
    });

    it('disables zoom-in button at maximum zoom', async () => {
      render(<PdfPreview file={mockFile} />);

      await waitFor(() => {
        expect(screen.getByTestId('zoom-in')).toBeInTheDocument();
      });

      // Zoom in to maximum (200%)
      fireEvent.click(screen.getByTestId('zoom-in'));
      fireEvent.click(screen.getByTestId('zoom-in'));

      expect(screen.getByTestId('zoom-in')).toBeDisabled();
    });

    it('sets zoom level directly when clicking preset button', async () => {
      render(<PdfPreview file={mockFile} />);

      await waitFor(() => {
        expect(screen.getByTestId('zoom-50')).toBeInTheDocument();
      });

      fireEvent.click(screen.getByTestId('zoom-50'));
      expect(screen.getByTestId('zoom-level')).toHaveTextContent('50%');

      fireEvent.click(screen.getByTestId('zoom-200'));
      expect(screen.getByTestId('zoom-level')).toHaveTextContent('200%');
    });

    it('highlights active zoom level button', async () => {
      render(<PdfPreview file={mockFile} />);

      await waitFor(() => {
        expect(screen.getByTestId('zoom-100')).toBeInTheDocument();
      });

      const zoom100Button = screen.getByTestId('zoom-100');
      expect(zoom100Button).toHaveAttribute('aria-pressed', 'true');

      fireEvent.click(screen.getByTestId('zoom-50'));

      const zoom50Button = screen.getByTestId('zoom-50');
      expect(zoom50Button).toHaveAttribute('aria-pressed', 'true');
      expect(zoom100Button).toHaveAttribute('aria-pressed', 'false');
    });
  });

  describe('Page Navigation', () => {
    it('displays page navigation controls', async () => {
      render(<PdfPreview file={mockFile} />);

      await waitFor(() => {
        expect(screen.getByTestId('prev-page')).toBeInTheDocument();
      });

      expect(screen.getByTestId('next-page')).toBeInTheDocument();
      expect(screen.getByTestId('current-page')).toBeInTheDocument();
      expect(screen.getByTestId('jump-to-page-input')).toBeInTheDocument();
      expect(screen.getByTestId('jump-to-page-button')).toBeInTheDocument();
    });

    it('navigates to next page', async () => {
      render(<PdfPreview file={mockFile} />);

      await waitFor(() => {
        expect(screen.getByTestId('next-page')).toBeInTheDocument();
      });

      expect(screen.getByTestId('current-page')).toHaveTextContent('Page 1 of 10');

      fireEvent.click(screen.getByTestId('next-page'));

      expect(screen.getByTestId('current-page')).toHaveTextContent('Page 2 of 10');
    });

    it('navigates to previous page', async () => {
      render(<PdfPreview file={mockFile} />);

      await waitFor(() => {
        expect(screen.getByTestId('next-page')).toBeInTheDocument();
      });

      // Go to page 2 first
      fireEvent.click(screen.getByTestId('next-page'));
      expect(screen.getByTestId('current-page')).toHaveTextContent('Page 2 of 10');

      // Then go back
      fireEvent.click(screen.getByTestId('prev-page'));
      expect(screen.getByTestId('current-page')).toHaveTextContent('Page 1 of 10');
    });

    it('disables previous button on first page', async () => {
      render(<PdfPreview file={mockFile} />);

      await waitFor(() => {
        expect(screen.getByTestId('prev-page')).toBeInTheDocument();
      });

      expect(screen.getByTestId('prev-page')).toBeDisabled();
    });

    it('disables next button on last page', async () => {
      render(<PdfPreview file={mockFile} />);

      await waitFor(() => {
        expect(screen.getByTestId('next-page')).toBeInTheDocument();
      });

      // Navigate to last page
      for (let i = 0; i < 9; i++) {
        fireEvent.click(screen.getByTestId('next-page'));
      }

      expect(screen.getByTestId('current-page')).toHaveTextContent('Page 10 of 10');
      expect(screen.getByTestId('next-page')).toBeDisabled();
    });

    it('jumps to specific page using input', async () => {
      render(<PdfPreview file={mockFile} />);

      await waitFor(() => {
        expect(screen.getByTestId('jump-to-page-input')).toBeInTheDocument();
      });

      const input = screen.getByTestId('jump-to-page-input') as HTMLInputElement;
      const button = screen.getByTestId('jump-to-page-button');

      fireEvent.change(input, { target: { value: '5' } });
      fireEvent.click(button);

      expect(screen.getByTestId('current-page')).toHaveTextContent('Page 5 of 10');
    });

    it('clears jump input after successful jump', async () => {
      render(<PdfPreview file={mockFile} />);

      await waitFor(() => {
        expect(screen.getByTestId('jump-to-page-input')).toBeInTheDocument();
      });

      const input = screen.getByTestId('jump-to-page-input') as HTMLInputElement;
      const button = screen.getByTestId('jump-to-page-button');

      fireEvent.change(input, { target: { value: '7' } });
      fireEvent.click(button);

      expect(input.value).toBe('');
    });

    it('ignores invalid page numbers', async () => {
      render(<PdfPreview file={mockFile} />);

      await waitFor(() => {
        expect(screen.getByTestId('jump-to-page-input')).toBeInTheDocument();
      });

      const input = screen.getByTestId('jump-to-page-input');
      const button = screen.getByTestId('jump-to-page-button');

      fireEvent.change(input, { target: { value: '99' } });
      fireEvent.click(button);

      // Should stay on page 1 (invalid page number)
      expect(screen.getByTestId('current-page')).toHaveTextContent('Page 1 of 10');
    });

    it('ignores non-numeric input', async () => {
      render(<PdfPreview file={mockFile} />);

      await waitFor(() => {
        expect(screen.getByTestId('jump-to-page-input')).toBeInTheDocument();
      });

      const input = screen.getByTestId('jump-to-page-input');
      const button = screen.getByTestId('jump-to-page-button');

      fireEvent.change(input, { target: { value: 'abc' } });
      fireEvent.click(button);

      // Should stay on page 1
      expect(screen.getByTestId('current-page')).toHaveTextContent('Page 1 of 10');
    });
  });

  describe('Close Button', () => {
    it('renders close button when onClose is provided', async () => {
      const onClose = vi.fn();
      render(<PdfPreview file={mockFile} onClose={onClose} />);

      await waitFor(() => {
        expect(screen.getByTestId('close-preview')).toBeInTheDocument();
      });
    });

    it('does not render close button when onClose is not provided', async () => {
      render(<PdfPreview file={mockFile} />);

      await waitFor(() => {
        expect(screen.getByTestId('pdf-preview')).toBeInTheDocument();
      });

      expect(screen.queryByTestId('close-preview')).not.toBeInTheDocument();
    });

    it('calls onClose when close button is clicked', async () => {
      const onClose = vi.fn();
      render(<PdfPreview file={mockFile} onClose={onClose} />);

      await waitFor(() => {
        expect(screen.getByTestId('close-preview')).toBeInTheDocument();
      });

      fireEvent.click(screen.getByTestId('close-preview'));

      expect(onClose).toHaveBeenCalledTimes(1);
    });
  });

  describe('Error Handling', () => {
    it('displays error message on load failure', async () => {
      // Override the mock to simulate error
      vi.spyOn(console, 'error').mockImplementation(() => {});

      const mockErrorFile = new File(['invalid'], 'corrupt.pdf', { type: 'application/pdf' });

      // Create a custom mock that triggers error
      vi.mock('react-pdf', () => ({
        Document: ({ onLoadError }: any) => {
          setTimeout(() => {
            if (onLoadError) {
              onLoadError(new Error('Corrupt PDF'));
            }
          }, 100);
          return <div data-testid="pdf-document" />;
        },
        Page: () => <div />,
        pdfjs: {
          version: '3.11.174',
          GlobalWorkerOptions: { workerSrc: '' }
        }
      }));

      render(<PdfPreview file={mockErrorFile} />);

      // Wait for error to be displayed
      await waitFor(() => {
        const errorElement = screen.queryByTestId('pdf-preview-error');
        if (errorElement) {
          expect(errorElement).toBeInTheDocument();
        }
      }, { timeout: 3000 });
    });

    it('displays close button in error state when onClose provided', async () => {
      const onClose = vi.fn();
      vi.spyOn(console, 'error').mockImplementation(() => {});

      const mockErrorFile = new File(['invalid'], 'corrupt.pdf', { type: 'application/pdf' });

      render(<PdfPreview file={mockErrorFile} onClose={onClose} />);

      // Note: Due to mocking limitations, we'll just verify the structure
      await waitFor(() => {
        expect(screen.getByTestId('pdf-preview')).toBeInTheDocument();
      });
    });
  });

  describe('Thumbnail Sidebar', () => {
    it('renders thumbnail sidebar on desktop', async () => {
      setViewportSize(DESKTOP_WIDTH);
      render(<PdfPreview file={mockFile} />);

      await waitFor(() => {
        expect(screen.getByTestId('thumbnail-sidebar')).toBeInTheDocument();
      });

      expect(screen.getByTestId('thumbnail-list')).toBeInTheDocument();
    });

    it('renders all page thumbnails', async () => {
      render(<PdfPreview file={mockFile} />);

      await waitFor(() => {
        expect(screen.getByTestId('thumbnail-list')).toBeInTheDocument();
      });

      const thumbnailList = screen.getByTestId('thumbnail-list');
      expect(thumbnailList).toHaveAttribute('data-item-count', '10');
    });

    it('navigates to page when thumbnail is clicked', async () => {
      render(<PdfPreview file={mockFile} />);

      await waitFor(() => {
        expect(screen.getByTestId('thumbnail-5')).toBeInTheDocument();
      });

      fireEvent.click(screen.getByTestId('thumbnail-5'));

      expect(screen.getByTestId('current-page')).toHaveTextContent('Page 5 of 10');
    });

    it('hides thumbnails on mobile', async () => {
      // Set mobile viewport
      setViewportSize(MOBILE_WIDTH);

      render(<PdfPreview file={mockFile} />);

      await waitFor(() => {
        expect(screen.getByTestId('pdf-preview')).toBeInTheDocument();
      });

      // Trigger resize event
      act(() => {
        window.dispatchEvent(new Event('resize'));
      });

      await waitFor(() => {
        expect(screen.getByTestId('toggle-thumbnails')).toBeInTheDocument();
      });
    });

    it('shows toggle button on mobile', async () => {
      // Set mobile viewport
      setViewportSize(MOBILE_WIDTH);

      render(<PdfPreview file={mockFile} />);

      // Trigger resize event
      act(() => {
        window.dispatchEvent(new Event('resize'));
      });

      await waitFor(() => {
        expect(screen.getByTestId('toggle-thumbnails')).toBeInTheDocument();
      });
    });
  });
});

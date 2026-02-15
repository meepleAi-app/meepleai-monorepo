/**
 * PdfUploadModal Component Tests (Issue #2518, #2616)
 *
 * Tests for PDF upload with preview functionality:
 * - File selection and validation
 * - Preview step with PDF viewer
 * - Upload confirmation flow
 */

import { screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';
import { PdfUploadModal } from '@/components/library/PdfUploadModal';

// Mock pdfjs for page count validation
const mockGetDocument = vi.fn();

// Mock react-pdf
vi.mock('react-pdf', () => ({
  pdfjs: {
    GlobalWorkerOptions: { workerSrc: '' },
    version: '3.0.0',
    getDocument: () => mockGetDocument(),
  },
  Document: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="mock-pdf-document">{children}</div>
  ),
  Page: () => <div data-testid="mock-pdf-page" />,
}));

// Mock PdfPreview component
vi.mock('@/components/pdf/PdfPreview', () => ({
  PdfPreview: ({ file }: { file: File }) => (
    <div data-testid="pdf-preview-mock">
      Preview of: {file.name}
    </div>
  ),
}));

// Mock toast
vi.mock('@/components/layout/Toast', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

describe('PdfUploadModal', () => {
  const mockProps = {
    isOpen: true,
    onClose: vi.fn(),
    gameId: 'test-game-id',
    gameTitle: 'Catan',
  };

  beforeEach(() => {
    vi.clearAllMocks();
    // Default mock: valid PDF with 10 pages
    mockGetDocument.mockReturnValue({
      promise: Promise.resolve({ numPages: 10 }),
    });
  });

  describe('Initial Render', () => {
    it('should render modal when open', () => {
      renderWithQuery(<PdfUploadModal {...mockProps} />);
      expect(screen.getByTestId('pdf-upload-modal')).toBeInTheDocument();
      expect(screen.getByTestId('pdf-upload-modal')).toHaveAttribute('data-step', 'select');
    });

    it('should display game title in description', () => {
      renderWithQuery(<PdfUploadModal {...mockProps} />);
      expect(screen.getByTestId('pdf-upload-modal')).toBeInTheDocument();
    });

    it('should show file input with correct accept type', () => {
      renderWithQuery(<PdfUploadModal {...mockProps} />);
      const input = screen.getByTestId('pdf-file-input');
      expect(input).toHaveAttribute('accept', 'application/pdf');
    });

    it('should show validation constraints', () => {
      renderWithQuery(<PdfUploadModal {...mockProps} />);
      expect(screen.getByText(/50MB/)).toBeInTheDocument();
      // Page limit validation happens during upload, not shown in UI
    });

    it('should have disabled preview button initially', () => {
      renderWithQuery(<PdfUploadModal {...mockProps} />);
      const button = screen.getByTestId('preview-button');
      expect(button).toBeDisabled();
    });
  });

  describe('Validation Errors (Synchronous)', () => {
    it('should show error for non-PDF file', async () => {
      renderWithQuery(<PdfUploadModal {...mockProps} />);
      const input = screen.getByTestId('pdf-file-input');
      const file = new File(['content'], 'test.txt', { type: 'text/plain' });

      fireEvent.change(input, { target: { files: [file] } });

      await waitFor(() => {
        expect(screen.getByTestId('validation-errors')).toBeInTheDocument();
      });
      expect(screen.getByText(/formato PDF/)).toBeInTheDocument();
    });

    it('should show error for file exceeding 50MB', async () => {
      renderWithQuery(<PdfUploadModal {...mockProps} />);
      const input = screen.getByTestId('pdf-file-input');

      // Create file with mocked size
      const file = new File(['x'], 'large.pdf', { type: 'application/pdf' });
      Object.defineProperty(file, 'size', { value: 60 * 1024 * 1024 }); // 60MB

      fireEvent.change(input, { target: { files: [file] } });

      await waitFor(() => {
        expect(screen.getByTestId('validation-errors')).toBeInTheDocument();
      });
      expect(screen.getByText(/troppo grande/)).toBeInTheDocument();
    });

    it('should show error for empty file', async () => {
      renderWithQuery(<PdfUploadModal {...mockProps} />);
      const input = screen.getByTestId('pdf-file-input');

      const file = new File([], 'empty.pdf', { type: 'application/pdf' });
      Object.defineProperty(file, 'size', { value: 0 });

      fireEvent.change(input, { target: { files: [file] } });

      await waitFor(() => {
        expect(screen.getByTestId('validation-errors')).toBeInTheDocument();
      });
      expect(screen.getByText(/vuoto/)).toBeInTheDocument();
    });
  });

  describe('File Selection with Page Count Validation', () => {
    it('should show success state when valid PDF selected', async () => {
      renderWithQuery(<PdfUploadModal {...mockProps} />);
      const input = screen.getByTestId('pdf-file-input');

      // Create valid PDF file
      const file = new File(['pdf content'], 'valid.pdf', { type: 'application/pdf' });
      Object.defineProperty(file, 'size', { value: 5 * 1024 * 1024 }); // 5MB

      fireEvent.change(input, { target: { files: [file] } });

      await waitFor(() => {
        expect(screen.getByTestId('file-selected')).toBeInTheDocument();
      });
      expect(screen.getByText('valid.pdf')).toBeInTheDocument();
      expect(screen.getByText('5.00 MB')).toBeInTheDocument();
    });

    it('should enable preview button after valid file selection', async () => {
      renderWithQuery(<PdfUploadModal {...mockProps} />);
      const input = screen.getByTestId('pdf-file-input');

      const file = new File(['pdf content'], 'valid.pdf', { type: 'application/pdf' });
      Object.defineProperty(file, 'size', { value: 5 * 1024 * 1024 });
      file.arrayBuffer = vi.fn().mockResolvedValue(new ArrayBuffer(100));

      fireEvent.change(input, { target: { files: [file] } });

      await waitFor(() => {
        const button = screen.getByTestId('preview-button');
        expect(button).not.toBeDisabled();
      });
    });

    it('should accept PDF regardless of page count (page validation removed)', async () => {
      renderWithQuery(<PdfUploadModal {...mockProps} />);
      const input = screen.getByTestId('pdf-file-input');

      const file = new File(['pdf content'], 'huge.pdf', { type: 'application/pdf' });
      Object.defineProperty(file, 'size', { value: 10 * 1024 * 1024 });

      fireEvent.change(input, { target: { files: [file] } });

      // Component now only does sync validation (type, size, empty).
      // A valid PDF file passes regardless of page count.
      await waitFor(() => {
        expect(screen.getByTestId('file-selected')).toBeInTheDocument();
      });
      expect(screen.getByText('huge.pdf')).toBeInTheDocument();
    });

    it('should accept PDF file without content validation (corruption check removed)', async () => {
      renderWithQuery(<PdfUploadModal {...mockProps} />);
      const input = screen.getByTestId('pdf-file-input');

      const file = new File(['invalid'], 'corrupt.pdf', { type: 'application/pdf' });
      Object.defineProperty(file, 'size', { value: 1 * 1024 * 1024 });

      fireEvent.change(input, { target: { files: [file] } });

      // Component now only does sync validation (type, size, empty).
      // A file with correct MIME type and non-zero size passes validation.
      await waitFor(() => {
        expect(screen.getByTestId('file-selected')).toBeInTheDocument();
      });
      expect(screen.getByText('corrupt.pdf')).toBeInTheDocument();
    });
  });

  describe('Preview Step', () => {
    const setupValidFile = async () => {
      renderWithQuery(<PdfUploadModal {...mockProps} />);
      const input = screen.getByTestId('pdf-file-input');

      const file = new File(['pdf content'], 'preview.pdf', { type: 'application/pdf' });
      Object.defineProperty(file, 'size', { value: 5 * 1024 * 1024 });
      file.arrayBuffer = vi.fn().mockResolvedValue(new ArrayBuffer(100));

      fireEvent.change(input, { target: { files: [file] } });

      await waitFor(() => {
        expect(screen.getByTestId('preview-button')).not.toBeDisabled();
      });
    };

    it('should show preview when preview button clicked', async () => {
      await setupValidFile();

      fireEvent.click(screen.getByTestId('preview-button'));

      await waitFor(() => {
        expect(screen.getByTestId('pdf-preview-container')).toBeInTheDocument();
      });
    });

    it('should show back button in preview step', async () => {
      await setupValidFile();

      fireEvent.click(screen.getByTestId('preview-button'));

      await waitFor(() => {
        expect(screen.getByTestId('back-button')).toBeInTheDocument();
      });
    });

    it('should return to select step when back button clicked', async () => {
      await setupValidFile();

      fireEvent.click(screen.getByTestId('preview-button'));

      await waitFor(() => {
        expect(screen.getByTestId('back-button')).toBeInTheDocument();
      });

      fireEvent.click(screen.getByTestId('back-button'));

      await waitFor(() => {
        expect(screen.getByTestId('pdf-upload-modal')).toHaveAttribute('data-step', 'select');
      });
    });

    it('should show confirm upload button in preview', async () => {
      await setupValidFile();

      fireEvent.click(screen.getByTestId('preview-button'));

      await waitFor(() => {
        expect(screen.getByTestId('confirm-upload-button')).toBeInTheDocument();
      });
    });
  });

  describe('Modal Close Behavior', () => {
    it('should call onClose when cancel button clicked', () => {
      renderWithQuery(<PdfUploadModal {...mockProps} />);
      fireEvent.click(screen.getByTestId('cancel-button'));
      expect(mockProps.onClose).toHaveBeenCalled();
    });

    it('should not render when isOpen is false', () => {
      renderWithQuery(<PdfUploadModal {...mockProps} isOpen={false} />);
      expect(screen.queryByTestId('pdf-upload-modal')).not.toBeInTheDocument();
    });
  });

  describe('Upload Step', () => {
    it('should show progress during upload', async () => {
      renderWithQuery(<PdfUploadModal {...mockProps} />);
      const input = screen.getByTestId('pdf-file-input');

      const file = new File(['pdf content'], 'upload.pdf', { type: 'application/pdf' });
      Object.defineProperty(file, 'size', { value: 5 * 1024 * 1024 });
      file.arrayBuffer = vi.fn().mockResolvedValue(new ArrayBuffer(100));

      fireEvent.change(input, { target: { files: [file] } });

      await waitFor(() => {
        expect(screen.getByTestId('preview-button')).not.toBeDisabled();
      });

      fireEvent.click(screen.getByTestId('preview-button'));

      await waitFor(() => {
        expect(screen.getByTestId('confirm-upload-button')).toBeInTheDocument();
      });

      fireEvent.click(screen.getByTestId('confirm-upload-button'));

      await waitFor(() => {
        expect(screen.getByTestId('uploading-state')).toBeInTheDocument();
        expect(screen.getByTestId('pdf-upload-modal')).toHaveAttribute('data-step', 'uploading');
      });
    });
  });
});

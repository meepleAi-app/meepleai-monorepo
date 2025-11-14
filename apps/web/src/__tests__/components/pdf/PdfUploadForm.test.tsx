import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { PdfUploadForm } from '@/components/pdf/PdfUploadForm';

// Mock dependencies
jest.mock('@/lib/retryUtils', () => ({
  retryWithBackoff: jest.fn((fn) => fn()),
  isRetryableError: jest.fn(() => true)
}));

jest.mock('@/lib/errorUtils', () => ({
  categorizeError: jest.fn((error) => ({
    category: 'network',
    message: error.message,
    canRetry: true
  })),
  extractCorrelationId: jest.fn(() => 'test-correlation-id')
}));

jest.mock('@/lib/api', () => ({
  ApiError: class ApiError extends Error {
    constructor(message: string, public statusCode: number, public correlationId?: string, public response?: Response) {
      super(message);
    }
  }
}));

// Mock PdfPreview component
jest.mock('@/components/PdfPreview', () => ({
  PdfPreview: ({ file }: { file: File }) => <div data-testid="pdf-preview">{file.name}</div>
}));

// Helper to create a proper PDF file for testing with proper Blob interface
function createPdfFile(name: string, content: string = '%PDF-1.4 test content'): File {
  // Encode the content to bytes
  const encoder = new TextEncoder();
  const uint8Array = encoder.encode(content);

  const file = new File([uint8Array], name, { type: 'application/pdf' });

  // Mock slice method to return a blob with working arrayBuffer
  const originalSlice = file.slice;
  file.slice = function(start?: number, end?: number) {
    const sliceStart = start || 0;
    const sliceEnd = end || content.length;
    const slicedContent = content.substring(sliceStart, sliceEnd);
    const slicedBytes = encoder.encode(slicedContent);

    // Create a mock blob
    const mockBlob: any = {
      size: slicedBytes.length,
      type: 'application/pdf',
      arrayBuffer: async () => slicedBytes.buffer,
      slice: originalSlice,
      stream: () => { throw new Error('stream not implemented'); },
      text: async () => slicedContent
    };

    return mockBlob;
  };

  return file;
}

describe('PdfUploadForm', () => {
  const mockProps = {
    gameId: 'game-1',
    gameName: 'Gloomhaven',
    onUploadSuccess: jest.fn(),
    onUploadError: jest.fn()
  };

  beforeEach(() => {
    jest.clearAllMocks();
    global.fetch = jest.fn();
  });

  afterEach(() => {
    jest.restoreAllMocks();
  });

  describe('Rendering', () => {
    it('renders file input with correct label', () => {
      render(<PdfUploadForm {...mockProps} />);

      expect(screen.getByLabelText(/PDF File/i)).toBeInTheDocument();
      expect(screen.getByLabelText(/PDF File/i)).toHaveAttribute('type', 'file');
      expect(screen.getByLabelText(/PDF File/i)).toHaveAttribute('accept', 'application/pdf');
    });

    it('renders language selector with default value', () => {
      render(<PdfUploadForm {...mockProps} />);

      expect(screen.getByLabelText(/Document Language/i)).toBeInTheDocument();
    });

    it('renders upload button', () => {
      render(<PdfUploadForm {...mockProps} />);

      expect(screen.getByRole('button', { name: /Upload PDF/i })).toBeInTheDocument();
    });

    it('upload button is disabled initially', () => {
      render(<PdfUploadForm {...mockProps} />);

      const uploadButton = screen.getByRole('button', { name: /Upload PDF/i });
      expect(uploadButton).toBeDisabled();
    });
  });

  describe('File Selection', () => {
    it('validates and accepts valid PDF file', async () => {
      const user = userEvent.setup();
      const file = createPdfFile('test.pdf');

      render(<PdfUploadForm {...mockProps} />);

      const input = screen.getByLabelText(/PDF File/i);
      await user.upload(input, file);

      await waitFor(() => {
        expect(screen.getByText(/test.pdf/i)).toBeInTheDocument();
        expect(screen.getByText(/✓/i)).toBeInTheDocument();
      });
    });

    it('shows validation error for non-PDF file', async () => {
      const user = userEvent.setup();
      const file = new File(['content'], 'test.txt', { type: 'text/plain' });

      render(<PdfUploadForm {...mockProps} />);

      const input = screen.getByLabelText(/PDF File/i);
      await user.upload(input, file);

      await waitFor(() => {
        expect(screen.getByText(/Invalid file type/i)).toBeInTheDocument();
      });
    });

    it('shows validation error for file too large', async () => {
      const user = userEvent.setup();
      const largeContent = new ArrayBuffer(105 * 1024 * 1024); // 105 MB
      const file = new File([largeContent], 'large.pdf', { type: 'application/pdf' });

      render(<PdfUploadForm {...mockProps} />);

      const input = screen.getByLabelText(/PDF File/i);
      await user.upload(input, file);

      await waitFor(() => {
        expect(screen.getByText(/exceeds maximum/i)).toBeInTheDocument();
      });
    });

    it('shows validation error for empty file', async () => {
      const user = userEvent.setup();
      const file = new File([], 'empty.pdf', { type: 'application/pdf' });

      render(<PdfUploadForm {...mockProps} />);

      const input = screen.getByLabelText(/PDF File/i);
      await user.upload(input, file);

      await waitFor(() => {
        expect(screen.getByText(/empty/i)).toBeInTheDocument();
      });
    });

    it('displays PDF preview when valid file is selected', async () => {
      const user = userEvent.setup();
      const file = createPdfFile('test.pdf');

      render(<PdfUploadForm {...mockProps} />);

      const input = screen.getByLabelText(/PDF File/i);
      await user.upload(input, file);

      await waitFor(() => {
        expect(screen.getByTestId('pdf-preview')).toBeInTheDocument();
      });
    });
  });

  describe('Language Selection', () => {
    it('defaults to English', () => {
      render(<PdfUploadForm {...mockProps} />);

      const select = screen.getByRole('combobox', { name: /Document Language/i });
      expect(select).toHaveTextContent('English');
    });

    it('allows changing language', async () => {
      const user = userEvent.setup();
      render(<PdfUploadForm {...mockProps} />);

      const select = screen.getByRole('combobox', { name: /Document Language/i });
      await user.click(select);

      const italianOption = screen.getByRole('option', { name: 'Italiano' });
      await user.click(italianOption);

      expect(select).toHaveTextContent('Italiano');
    });

    it('includes all supported languages', async () => {
      const user = userEvent.setup();
      render(<PdfUploadForm {...mockProps} />);

      const select = screen.getByRole('combobox', { name: /Document Language/i });
      await user.click(select);

      expect(screen.getByRole('option', { name: 'English' })).toBeInTheDocument();
      expect(screen.getByRole('option', { name: 'Italiano' })).toBeInTheDocument();
      expect(screen.getByRole('option', { name: 'Deutsch' })).toBeInTheDocument();
      expect(screen.getByRole('option', { name: 'Français' })).toBeInTheDocument();
      expect(screen.getByRole('option', { name: 'Español' })).toBeInTheDocument();
    });
  });

  describe('File Upload', () => {
    it('uploads file successfully', async () => {
      const user = userEvent.setup();
      const file = createPdfFile('test.pdf');

      (global.fetch as jest.Mock).mockResolvedValue({
        ok: true,
        json: async () => ({ documentId: 'doc-123' })
      });

      render(<PdfUploadForm {...mockProps} />);

      const input = screen.getByLabelText(/PDF File/i);
      await user.upload(input, file);

      await waitFor(() => {
        expect(screen.getByText(/✓/i)).toBeInTheDocument();
      });

      const uploadButton = screen.getByRole('button', { name: /Upload PDF/i });
      await user.click(uploadButton);

      await waitFor(() => {
        expect(mockProps.onUploadSuccess).toHaveBeenCalledWith('doc-123');
      });
    });

    it('shows loading state during upload', async () => {
      const user = userEvent.setup();
      const file = createPdfFile('test.pdf');

      let resolveUpload: (value: any) => void;
      const uploadPromise = new Promise((resolve) => {
        resolveUpload = resolve;
      });
      (global.fetch as jest.Mock).mockReturnValue(uploadPromise);

      render(<PdfUploadForm {...mockProps} />);

      const input = screen.getByLabelText(/PDF File/i);
      await user.upload(input, file);

      await waitFor(() => {
        expect(screen.getByText(/✓/i)).toBeInTheDocument();
      });

      const uploadButton = screen.getByRole('button', { name: /Upload PDF/i });
      await user.click(uploadButton);

      expect(screen.getByText(/Uploading.../i)).toBeInTheDocument();

      resolveUpload!({
        ok: true,
        json: async () => ({ documentId: 'doc-123' })
      });
    });

    it('handles upload error', async () => {
      const user = userEvent.setup();
      const file = createPdfFile('test.pdf');

      (global.fetch as jest.Mock).mockRejectedValue(new Error('Network error'));

      render(<PdfUploadForm {...mockProps} />);

      const input = screen.getByLabelText(/PDF File/i);
      await user.upload(input, file);

      await waitFor(() => {
        expect(screen.getByText(/✓/i)).toBeInTheDocument();
      });

      const uploadButton = screen.getByRole('button', { name: /Upload PDF/i });
      await user.click(uploadButton);

      await waitFor(() => {
        expect(mockProps.onUploadError).toHaveBeenCalled();
      });
    });

    it('shows retry message during retries', async () => {
      const user = userEvent.setup();
      const file = createPdfFile('test.pdf');

      const { retryWithBackoff } = require('@/lib/retryUtils');
      retryWithBackoff.mockImplementation(async (fn, options) => {
        await options.onRetry(new Error('Failed'), 1, 2000);
        return fn();
      });

      (global.fetch as jest.Mock).mockResolvedValue({
        ok: true,
        json: async () => ({ documentId: 'doc-123' })
      });

      render(<PdfUploadForm {...mockProps} />);

      const input = screen.getByLabelText(/PDF File/i);
      await user.upload(input, file);

      await waitFor(() => {
        expect(screen.getByText(/✓/i)).toBeInTheDocument();
      });

      const uploadButton = screen.getByRole('button', { name: /Upload PDF/i });
      await user.click(uploadButton);

      await waitFor(() => {
        expect(mockProps.onUploadSuccess).toHaveBeenCalled();
      });
    });
  });

  describe('Accessibility', () => {
    it('has proper form structure', () => {
      render(<PdfUploadForm {...mockProps} />);

      const form = screen.getByRole('button', { name: /Upload PDF/i }).closest('form');
      expect(form).toBeInTheDocument();
    });

    it('associates labels with inputs', () => {
      render(<PdfUploadForm {...mockProps} />);

      const fileInput = screen.getByLabelText(/PDF File/i);
      const languageSelect = screen.getByLabelText(/Document Language/i);

      expect(fileInput).toHaveAttribute('id', 'pdf-file');
      expect(languageSelect).toBeInTheDocument();
    });
  });
});

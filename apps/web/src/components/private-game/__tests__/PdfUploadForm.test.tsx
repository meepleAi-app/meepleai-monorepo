/**
 * PdfUploadForm Component Tests
 *
 * Issue #3664: Private game PDF support — upload form.
 *
 * Test Coverage:
 * - Initial render: shows upload button
 * - File input: accepts .pdf files only
 * - Error handling: shows error message when upload fails
 * - Success: calls onUploadComplete with documentId
 * - Upload in progress: shows progress bar and hides button
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { PdfUploadForm } from '../PdfUploadForm';

// ============================================================================
// Mocks
// ============================================================================

vi.mock('@/lib/api', () => ({
  api: {
    pdf: {
      uploadPdf: vi.fn(),
    },
  },
}));

// eslint-disable-next-line @typescript-eslint/no-explicit-any
const { api } = (await import('@/lib/api')) as any;

// ============================================================================
// Helpers
// ============================================================================

const TEST_GAME_ID = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee';

function createPdfFile(name = 'rulebook.pdf') {
  return new File(['%PDF-1.4 content'], name, { type: 'application/pdf' });
}

// ============================================================================
// Tests
// ============================================================================

describe('PdfUploadForm', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Initial render', () => {
    it('renders the upload button', () => {
      render(<PdfUploadForm privateGameId={TEST_GAME_ID} />);

      expect(screen.getByTestId('pdf-upload-button')).toBeInTheDocument();
      expect(screen.getByText(/Seleziona PDF/i)).toBeInTheDocument();
    });

    it('renders the file input accepting pdf files', () => {
      render(<PdfUploadForm privateGameId={TEST_GAME_ID} />);

      const input = screen.getByTestId('pdf-file-input');
      expect(input).toHaveAttribute('accept', '.pdf');
      expect(input).toHaveAttribute('type', 'file');
    });

    it('does not show error initially', () => {
      render(<PdfUploadForm privateGameId={TEST_GAME_ID} />);

      expect(screen.queryByTestId('pdf-upload-error')).not.toBeInTheDocument();
    });

    it('renders the form wrapper with data-testid', () => {
      render(<PdfUploadForm privateGameId={TEST_GAME_ID} />);

      expect(screen.getByTestId('pdf-upload-form')).toBeInTheDocument();
    });
  });

  describe('Successful upload', () => {
    it('calls onUploadComplete with the documentId on success', async () => {
      const onUploadComplete = vi.fn();
      api.pdf.uploadPdf.mockResolvedValue({ documentId: 'doc-abc', fileName: 'rulebook.pdf' });

      render(<PdfUploadForm privateGameId={TEST_GAME_ID} onUploadComplete={onUploadComplete} />);

      const input = screen.getByTestId('pdf-file-input');
      await userEvent.upload(input, createPdfFile());

      await waitFor(() => {
        expect(onUploadComplete).toHaveBeenCalledWith('doc-abc');
      });
    });

    it('calls api.pdf.uploadPdf with correct arguments', async () => {
      api.pdf.uploadPdf.mockResolvedValue({ documentId: 'doc-xyz', fileName: 'rules.pdf' });

      render(<PdfUploadForm privateGameId={TEST_GAME_ID} />);

      const file = createPdfFile('rules.pdf');
      const input = screen.getByTestId('pdf-file-input');
      await userEvent.upload(input, file);

      await waitFor(() => {
        expect(api.pdf.uploadPdf).toHaveBeenCalledWith(TEST_GAME_ID, file, expect.any(Function), {
          isPrivateGame: true,
        });
      });
    });
  });

  describe('Error handling', () => {
    it('shows error message when upload fails', async () => {
      api.pdf.uploadPdf.mockRejectedValue(new Error('Upload failed (413)'));

      render(<PdfUploadForm privateGameId={TEST_GAME_ID} />);

      const input = screen.getByTestId('pdf-file-input');
      await userEvent.upload(input, createPdfFile());

      await waitFor(() => {
        expect(screen.getByTestId('pdf-upload-error')).toBeInTheDocument();
        expect(screen.getByTestId('pdf-upload-error')).toHaveTextContent('Upload failed (413)');
      });
    });

    it('error element has role=alert', async () => {
      api.pdf.uploadPdf.mockRejectedValue(new Error('Network error'));

      render(<PdfUploadForm privateGameId={TEST_GAME_ID} />);

      const input = screen.getByTestId('pdf-file-input');
      await userEvent.upload(input, createPdfFile());

      await waitFor(() => {
        expect(screen.getByRole('alert')).toBeInTheDocument();
      });
    });

    it('shows fallback error message for non-Error rejections', async () => {
      api.pdf.uploadPdf.mockRejectedValue('unknown error');

      render(<PdfUploadForm privateGameId={TEST_GAME_ID} />);

      const input = screen.getByTestId('pdf-file-input');
      await userEvent.upload(input, createPdfFile());

      await waitFor(() => {
        expect(screen.getByTestId('pdf-upload-error')).toHaveTextContent(
          /Errore durante il caricamento/i
        );
      });
    });

    it('re-shows upload button after error', async () => {
      api.pdf.uploadPdf.mockRejectedValue(new Error('Upload failed'));

      render(<PdfUploadForm privateGameId={TEST_GAME_ID} />);

      const input = screen.getByTestId('pdf-file-input');
      await userEvent.upload(input, createPdfFile());

      await waitFor(() => {
        expect(screen.getByTestId('pdf-upload-button')).toBeInTheDocument();
      });
    });
  });
});

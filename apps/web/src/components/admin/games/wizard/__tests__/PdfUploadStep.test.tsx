/**
 * PdfUploadStep Tests
 * Issue #4673: Step 3 of the admin wizard — upload PDF rulebook.
 */

import { render, screen, act } from '@testing-library/react';
import { vi, describe, it, expect, beforeEach } from 'vitest';

import { PdfUploadStep } from '../steps/PdfUploadStep';

// ─── Mocks ───────────────────────────────────────────────────────────────────

// Mock PdfUploadForm — expose callbacks for test control
let capturedOnSuccess: ((id: string) => void) | null = null;
let capturedOnError: ((err: { message: string; category?: string }) => void) | null = null;

vi.mock('@/components/pdf/PdfUploadForm', () => ({
  PdfUploadForm: ({
    gameId,
    gameName,
    onUploadSuccess,
    onUploadError,
  }: {
    gameId: string;
    gameName: string;
    onUploadSuccess: (id: string) => void;
    onUploadError: (err: { message: string; category?: string }) => void;
  }) => {
    capturedOnSuccess = onUploadSuccess;
    capturedOnError = onUploadError;
    return (
      <div data-testid="pdf-upload-form">
        <span data-testid="form-game-id">{gameId}</span>
        <span data-testid="form-game-name">{gameName}</span>
      </div>
    );
  },
}));

// ─── Tests ───────────────────────────────────────────────────────────────────

describe('PdfUploadStep', () => {
  const defaultProps = {
    gameId: 'game-uuid-1',
    gameTitle: 'Gloomhaven',
    onPdfUploaded: vi.fn(),
  };

  beforeEach(() => {
    vi.clearAllMocks();
    capturedOnSuccess = null;
    capturedOnError = null;
  });

  it('should render the PdfUploadForm component', () => {
    render(<PdfUploadStep {...defaultProps} />);
    expect(screen.getByTestId('pdf-upload-form')).toBeDefined();
  });

  it('should pass gameId and gameName to PdfUploadForm', () => {
    render(<PdfUploadStep {...defaultProps} />);
    expect(screen.getByTestId('form-game-id').textContent).toBe('game-uuid-1');
    expect(screen.getByTestId('form-game-name').textContent).toBe('Gloomhaven');
  });

  it('should display game title in the description', () => {
    render(<PdfUploadStep {...defaultProps} />);
    expect(screen.getByText('Gloomhaven', { selector: 'strong' })).toBeDefined();
  });

  it('should show success state after successful upload', () => {
    render(<PdfUploadStep {...defaultProps} />);

    act(() => {
      capturedOnSuccess?.('uploaded-pdf-doc-id');
    });

    expect(screen.getByText('PDF uploaded successfully')).toBeDefined();
    expect(screen.getByText('Document ID: uploaded-pdf-doc-id')).toBeDefined();
  });

  it('should call onPdfUploaded with the document ID on success', () => {
    const onPdfUploaded = vi.fn();
    render(<PdfUploadStep {...defaultProps} onPdfUploaded={onPdfUploaded} />);

    act(() => {
      capturedOnSuccess?.('pdf-doc-uuid');
    });

    expect(onPdfUploaded).toHaveBeenCalledWith('pdf-doc-uuid');
  });

  it('should hide upload form after successful upload', () => {
    render(<PdfUploadStep {...defaultProps} />);

    act(() => {
      capturedOnSuccess?.('pdf-doc-uuid');
    });

    expect(screen.queryByTestId('pdf-upload-form')).toBeNull();
  });

  it('should show error message when upload fails', () => {
    render(<PdfUploadStep {...defaultProps} />);

    act(() => {
      capturedOnError?.({ message: 'File too large', category: 'validation' });
    });

    expect(screen.getByText('Upload failed')).toBeDefined();
    expect(screen.getByText('File too large')).toBeDefined();
  });

  it('should keep upload form visible after error', () => {
    render(<PdfUploadStep {...defaultProps} />);

    act(() => {
      capturedOnError?.({ message: 'Network error' });
    });

    expect(screen.getByTestId('pdf-upload-form')).toBeDefined();
  });
});

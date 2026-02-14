/**
 * Step1PdfUpload Tests - Issue #4141
 */

import { render, screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { Step1PdfUpload } from '../Step1PdfUpload';

// Mock dependencies
vi.mock('@/lib/utils/upload', () => ({
  uploadChunks: vi.fn().mockResolvedValue('pdf-123'),
}));

vi.mock('@/lib/utils/extraction', () => ({
  pollExtractionStatus: vi.fn().mockResolvedValue({
    pdfDocumentId: 'pdf-123',
    status: 'completed',
    qualityScore: 0.85,
    extractedTitle: 'Catan',
  }),
}));

vi.mock('@/hooks/usePdfProgress', () => ({
  usePdfProgress: vi.fn().mockReturnValue({
    status: { progress: 50, state: 'Processing' },
    isConnected: true,
  }),
}));

vi.mock('@/lib/stores/pdf-wizard-store', () => ({
  usePdfWizardStore: vi.fn((selector) => {
    const mockStore = {
      qualityScore: 0.85,
      extractedTitle: 'Catan',
      setStep1Data: vi.fn(),
    };
    return selector ? selector(mockStore) : mockStore;
  }),
}));

describe('Step1PdfUpload', () => {
  const mockOnNext = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should render upload zone', () => {
    render(<Step1PdfUpload onNext={mockOnNext} />);

    expect(screen.getByLabelText('Upload PDF file')).toBeInTheDocument();
    expect(screen.getByText(/Drag & drop a PDF file here/i)).toBeInTheDocument();
  });

  it('should display file size and format restrictions', () => {
    render(<Step1PdfUpload onNext={mockOnNext} />);

    expect(screen.getByText(/Max size: 100MB/i)).toBeInTheDocument();
    expect(screen.getByText(/PDF format only/i)).toBeInTheDocument();
  });

  // TODO: Add integration tests for upload flow with proper async mock setup
});

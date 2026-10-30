/**
 * #4449: every PDF upload zone refuses a file over the limit the backend gives — before uploading
 * it, with the limit in the message.
 *
 * The zones carried their own constants (50, 150, 500 MB) while the backend accepted 100. The
 * limit here is 20 MB on purpose: no component ever carried that number, so a zone that still read
 * a constant of its own would let the 21 MB file through. Zones with a test file of their own
 * (PdfUploadSection, add-game-sheet PdfUploadZone, the import wizard's Step1UploadPdf, the admin KB
 * upload zone) are covered there, and so is the KB Processing Settings panel
 * (`components/admin/knowledge-base/__tests__/upload-settings.test.tsx`, #4446).
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { resetPdfUploadLimit, setPdfUploadLimit } from '@/__tests__/utils/pdf-upload-limits-mock';
import { renderWithQuery } from '@/__tests__/utils/query-test-utils';
import { PdfUploadStep } from '@/app/(authenticated)/admin/wizard/steps/PdfUploadStep';
import { UploadPrivatePDF } from '@/components/collection/wizard/steps/UploadPrivatePDF';
import { UploadRulesStep } from '@/components/game-night/steps/UploadRulesStep';
import { PdfUploadModal } from '@/components/library/PdfUploadModal';
import { PdfUploadSheet } from '@/components/library/PdfUploadSheet';

import { PdfUploadForm } from '../PdfUploadForm';

vi.mock(
  '@/hooks/queries/usePdfUploadLimits',
  () => import('@/__tests__/utils/pdf-upload-limits-mock')
);

const { toastError } = vi.hoisted(() => ({ toastError: vi.fn() }));
vi.mock('@/components/layout', () => ({
  toast: { error: toastError, success: vi.fn(), warning: vi.fn(), info: vi.fn() },
}));
vi.mock('@/components/layout/Toast', () => ({
  toast: { error: toastError, success: vi.fn(), warning: vi.fn(), info: vi.fn() },
}));

vi.mock('@/hooks/useAddGameWizard', () => ({
  useAddGameWizard: () => ({ setUploadedPdf: vi.fn(), goNext: vi.fn(), goBack: vi.fn() }),
}));

vi.mock('@/components/pdf/CopyrightDisclaimerModal', () => ({
  CopyrightDisclaimerModal: ({ open, onAccept }: { open: boolean; onAccept: () => void }) =>
    open ? (
      <button type="button" onClick={onAccept}>
        accept-disclaimer
      </button>
    ) : null,
}));

vi.mock('@/components/library/PdfProcessingStatus', () => ({
  PdfProcessingStatus: () => <div />,
}));

vi.mock('@/components/pdf', () => ({
  PdfPreview: () => <div />,
  PdfProgressBar: () => <div />,
  PdfErrorCard: () => <div />,
}));

vi.mock('@/hooks/useChunkedUpload', () => ({
  useChunkedUpload: () => ({
    progress: {
      status: 'idle',
      currentChunk: 0,
      totalChunks: 0,
      progressPercentage: 0,
      uploadedBytes: 0,
      totalBytes: 0,
    },
    uploadChunked: vi.fn(),
    shouldUseChunkedUpload: () => false,
    isUploading: false,
    reset: vi.fn(),
  }),
}));

const MB = 1024 * 1024;
const LIMIT = 20 * MB;

function pdfOfSize(bytes: number): File {
  const file = new File(['%PDF-1.7'], 'big.pdf', { type: 'application/pdf' });
  Object.defineProperty(file, 'size', { value: bytes });
  return file;
}

function choose(input: HTMLElement, file: File) {
  fireEvent.change(input, { target: { files: [file] } });
}

beforeEach(() => {
  vi.clearAllMocks();
  resetPdfUploadLimit();
  setPdfUploadLimit(LIMIT);
});

describe('PDF upload zones read the size limit from the backend (#4449)', () => {
  it('private game wizard step (PdfUploadStep)', () => {
    const { container } = render(<PdfUploadStep onComplete={vi.fn()} />);

    choose(container.querySelector('#pdf-file-input')!, pdfOfSize(LIMIT + MB));

    expect(toastError).toHaveBeenCalledWith('Il file supera il limite di 20 MB');
    expect(screen.getByText('PDF fino a 20 MB')).toBeInTheDocument();
  });

  it('collection wizard (UploadPrivatePDF)', () => {
    const { container } = render(<UploadPrivatePDF />);

    choose(container.querySelector('#pdf-file-input')!, pdfOfSize(LIMIT + MB));

    expect(toastError).toHaveBeenCalledWith('File exceeds the 20 MB limit');
  });

  it('game night rules step (UploadRulesStep)', () => {
    render(
      <UploadRulesStep gameId="game-1" gameTitle="Catan" onComplete={vi.fn()} onSkip={vi.fn()} />
    );
    fireEvent.click(screen.getByTestId('upload-rules-button'));
    fireEvent.click(screen.getByText('accept-disclaimer'));

    choose(screen.getByTestId('pdf-file-input'), pdfOfSize(LIMIT + MB));

    expect(screen.getByTestId('upload-error')).toHaveTextContent(
      'Il file supera il limite di 20 MB.'
    );
  });

  it('library upload modal (PdfUploadModal)', async () => {
    // The modal reads the library through React Query (#4756).
    renderWithQuery(<PdfUploadModal isOpen onClose={vi.fn()} gameId="game-1" gameTitle="Catan" />);

    choose(screen.getByTestId('pdf-file-input'), pdfOfSize(LIMIT + MB));

    expect(await screen.findByTestId('validation-errors')).toHaveTextContent(
      'Il file è troppo grande (max 20 MB)'
    );
  });

  it('library upload sheet (PdfUploadSheet)', () => {
    render(<PdfUploadSheet open onOpenChange={vi.fn()} gameId="game-1" />);

    choose(screen.getByTestId('pdf-sheet-file-input'), pdfOfSize(LIMIT + MB));

    expect(screen.getByTestId('upload-error')).toHaveTextContent(
      'Il file è troppo grande (max 20 MB)'
    );
  });

  it('PDF upload form (PdfUploadForm)', async () => {
    const { container } = render(
      <PdfUploadForm
        gameId="game-1"
        gameName="Catan"
        onUploadSuccess={vi.fn()}
        onUploadError={vi.fn()}
      />
    );

    choose(container.querySelector('#pdf-file')!, pdfOfSize(LIMIT + MB));

    expect(await screen.findByText(/exceeds maximum allowed \(20 MB\)/)).toBeInTheDocument();
  });

  it('a file at the limit passes the zone', () => {
    const { container } = render(<PdfUploadStep onComplete={vi.fn()} />);

    choose(container.querySelector('#pdf-file-input')!, pdfOfSize(LIMIT));

    expect(toastError).not.toHaveBeenCalled();
  });

  it('while the limits are unknown the zone leaves the check to the backend', () => {
    setPdfUploadLimit(undefined);
    const { container } = render(<PdfUploadStep onComplete={vi.fn()} />);

    choose(container.querySelector('#pdf-file-input')!, pdfOfSize(500 * MB));

    expect(toastError).not.toHaveBeenCalled();
  });
});

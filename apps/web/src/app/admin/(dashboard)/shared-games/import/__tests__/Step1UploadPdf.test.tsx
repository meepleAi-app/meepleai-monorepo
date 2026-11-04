/**
 * Step 1 of the import wizard — #4401: the game comes first.
 *
 * The wizard used to upload the rulebook with no game, and the API refused the init with 422
 * ("Either GameId or PrivateGameId must be provided."): the wizard never got past step 1. Now the
 * title creates a draft game and the PDF is uploaded for it.
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { resetPdfUploadLimit, setPdfUploadLimit } from '@/__tests__/utils/pdf-upload-limits-mock';
import { useGameImportWizardStore } from '@/stores/useGameImportWizardStore';

import { Step1UploadPdf } from '../steps/Step1UploadPdf';

const mockStartGameImport = vi.fn();
const mockWizardChunkedUpload = vi.fn();
vi.mock('@/lib/api', () => ({
  api: {
    sharedGames: {
      startGameImport: (...args: unknown[]) => mockStartGameImport(...args),
      wizardChunkedUpload: (...args: unknown[]) => mockWizardChunkedUpload(...args),
    },
  },
}));

// #4449: the size limit is the backend's, read through usePdfUploadLimits.
vi.mock(
  '@/hooks/queries/usePdfUploadLimits',
  () => import('@/__tests__/utils/pdf-upload-limits-mock')
);

vi.mock('@/components/layout', () => ({
  toast: { success: vi.fn(), error: vi.fn(), warning: vi.fn() },
}));

function pdfFile(): File {
  return new File(['%PDF-1.7 rulebook'], 'rules.pdf', { type: 'application/pdf' });
}

async function chooseTitleAndFile(user: ReturnType<typeof userEvent.setup>, title: string) {
  await user.type(screen.getByLabelText('Titolo del gioco'), title);
  await user.upload(screen.getByLabelText('Select PDF File'), pdfFile());
  await waitFor(() => expect(screen.getByText('rules.pdf')).toBeInTheDocument());
}

beforeEach(() => {
  vi.clearAllMocks();
  resetPdfUploadLimit();
  useGameImportWizardStore.setState({ importTitle: '', importGameId: null, uploadedPdf: null });
});

describe('Step1UploadPdf size limit (#4449)', () => {
  function pdfFileOfSize(bytes: number): File {
    const file = pdfFile();
    Object.defineProperty(file, 'size', { value: bytes });
    return file;
  }

  it('refuses a 150 MB PDF before uploading it, naming the backend limit', async () => {
    const user = userEvent.setup();
    render(<Step1UploadPdf />);

    await user.type(screen.getByLabelText('Titolo del gioco'), 'Catan');
    await user.upload(screen.getByLabelText('Select PDF File'), pdfFileOfSize(150 * 1024 * 1024));

    expect(await screen.findByText(/exceeds maximum allowed \(100 MB\)/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /upload pdf/i })).toBeDisabled();
    expect(mockStartGameImport).not.toHaveBeenCalled();
    expect(mockWizardChunkedUpload).not.toHaveBeenCalled();
  });

  it('uses the limit the backend gives, not one of its own', async () => {
    setPdfUploadLimit(20 * 1024 * 1024);
    const user = userEvent.setup();
    render(<Step1UploadPdf />);

    await user.upload(screen.getByLabelText('Select PDF File'), pdfFileOfSize(21 * 1024 * 1024));

    expect(await screen.findByText(/exceeds maximum allowed \(20 MB\)/)).toBeInTheDocument();
  });
});

describe('Step1UploadPdf (#4401)', () => {
  it('creates the draft game, then uploads the PDF for it', async () => {
    const user = userEvent.setup();
    const onUploadComplete = vi.fn();
    mockStartGameImport.mockResolvedValue({ gameId: 'game-1' });
    mockWizardChunkedUpload.mockResolvedValue({ pdfDocumentId: 'pdf-1', fileName: 'rules.pdf' });
    render(<Step1UploadPdf onUploadComplete={onUploadComplete} />);

    await chooseTitleAndFile(user, 'Catan');
    await user.click(screen.getByRole('button', { name: /upload pdf/i }));

    await waitFor(() => expect(onUploadComplete).toHaveBeenCalled());
    expect(mockStartGameImport).toHaveBeenCalledWith('Catan');
    expect(mockWizardChunkedUpload).toHaveBeenCalledWith(
      expect.any(File),
      'game-1',
      expect.any(Function)
    );
    expect(mockStartGameImport.mock.invocationCallOrder[0]).toBeLessThan(
      mockWizardChunkedUpload.mock.invocationCallOrder[0]
    );
    expect(onUploadComplete).toHaveBeenCalledWith(
      expect.objectContaining({ pdfDocumentId: 'pdf-1' })
    );
    expect(useGameImportWizardStore.getState().importGameId).toBe('game-1');
  });

  it('does not upload without a title', async () => {
    const user = userEvent.setup();
    render(<Step1UploadPdf />);

    await user.upload(screen.getByLabelText('Select PDF File'), pdfFile());
    await waitFor(() => expect(screen.getByText('rules.pdf')).toBeInTheDocument());

    expect(screen.getByRole('button', { name: /upload pdf/i })).toBeDisabled();
    expect(mockStartGameImport).not.toHaveBeenCalled();
  });

  it('a retry reuses the draft instead of creating a second game', async () => {
    const user = userEvent.setup();
    const onUploadComplete = vi.fn();
    mockStartGameImport.mockResolvedValue({ gameId: 'game-1' });
    mockWizardChunkedUpload
      .mockRejectedValueOnce(new Error('Chunk 1/1 upload failed'))
      .mockResolvedValueOnce({ pdfDocumentId: 'pdf-1', fileName: 'rules.pdf' });
    render(<Step1UploadPdf onUploadComplete={onUploadComplete} />);

    await chooseTitleAndFile(user, 'Catan');
    await user.click(screen.getByRole('button', { name: /upload pdf/i }));
    expect(await screen.findByText('Chunk 1/1 upload failed')).toBeInTheDocument();
    expect(screen.getByLabelText('Titolo del gioco')).toBeDisabled();

    await user.click(screen.getByRole('button', { name: /upload pdf/i }));

    await waitFor(() => expect(onUploadComplete).toHaveBeenCalled());
    expect(mockStartGameImport).toHaveBeenCalledTimes(1);
    expect(mockWizardChunkedUpload).toHaveBeenCalledTimes(2);
    expect(mockWizardChunkedUpload.mock.calls[1][1]).toBe('game-1');
  });
});

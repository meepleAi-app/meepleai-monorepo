/**
 * PdfUploadModal Component Tests
 *
 * #4756: after an upload the modal proposes the ownership declaration, because without it the
 * rulebook the user just uploaded stays closed to the assistant (RagAccessService). It proposes
 * and writes nothing: the declaration is the user's explicit act.
 */

import { useState } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { PdfUploadModal } from '../PdfUploadModal';

// ============================================================================
// Mock Setup
// ============================================================================

const mockUploadPdf = vi.fn();
const mockGetGameDetail = vi.fn();
const mockDeclareOwnership = vi.fn();

vi.mock('@/lib/api', () => ({
  api: {
    pdf: { uploadPdf: (...args: unknown[]) => mockUploadPdf(...args) },
    library: {
      getGameDetail: (...args: unknown[]) => mockGetGameDetail(...args),
      declareOwnership: (...args: unknown[]) => mockDeclareOwnership(...args),
    },
  },
}));

const mockToastSuccess = vi.fn();
const mockToastError = vi.fn();

vi.mock('@/components/layout/Toast', () => ({
  toast: {
    success: (...args: unknown[]) => mockToastSuccess(...args),
    error: (...args: unknown[]) => mockToastError(...args),
  },
}));

vi.mock('@/hooks/queries/usePdfUploadLimits', () => ({
  usePdfUploadLimits: () => ({ data: { maxFileSizeBytes: 50 * 1024 * 1024 } }),
}));

vi.mock('@/components/pdf', () => ({
  PdfPreview: () => <div data-testid="pdf-preview" />,
  PdfProgressBar: () => null,
  PdfErrorCard: ({ error }: { error: string }) => <div>{error}</div>,
}));

// ============================================================================
// Helpers
// ============================================================================

const GAME_ID = 'game-1';

function libraryEntry(overrides: { hasRagAccess: boolean; currentState: string }) {
  return { id: 'entry-1', gameId: GAME_ID, gameTitle: 'Catan', ...overrides };
}

const OWNERSHIP_RESULT = {
  gameState: 'Owned',
  ownershipDeclaredAt: '2026-10-29T10:00:00Z',
  hasRagAccess: true,
  kbCardCount: 0,
  isRagPublic: false,
};

function Harness({ onUploadSuccess }: { onUploadSuccess?: () => void }) {
  const [isOpen, setIsOpen] = useState(true);
  return (
    <PdfUploadModal
      isOpen={isOpen}
      onClose={() => setIsOpen(false)}
      gameId={GAME_ID}
      gameTitle="Catan"
      onUploadSuccess={onUploadSuccess}
    />
  );
}

function renderModal(onUploadSuccess?: () => void) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const invalidateQueries = vi.spyOn(queryClient, 'invalidateQueries');
  render(
    <QueryClientProvider client={queryClient}>
      <Harness onUploadSuccess={onUploadSuccess} />
    </QueryClientProvider>
  );
  return { invalidateQueries };
}

async function uploadAPdf() {
  const user = userEvent.setup();
  await user.upload(
    screen.getByTestId('pdf-file-input'),
    new File(['%PDF-1.4 rules'], 'regolamento.pdf', { type: 'application/pdf' })
  );
  await user.click(screen.getByTestId('preview-button'));
  await user.click(await screen.findByTestId('confirm-upload-button'));
  return user;
}

async function waitForTheUploadModalToClose() {
  await waitFor(() => expect(screen.queryByTestId('pdf-upload-modal')).not.toBeInTheDocument(), {
    timeout: 3000,
  });
}

// ============================================================================
// Tests
// ============================================================================

describe('PdfUploadModal — ownership proposal after upload (#4756)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockUploadPdf.mockResolvedValue(undefined);
    mockDeclareOwnership.mockResolvedValue(OWNERSHIP_RESULT);
  });

  it('proposes the declaration, and writes nothing, when the library entry cannot read the manual', async () => {
    mockGetGameDetail.mockResolvedValue(
      libraryEntry({ hasRagAccess: false, currentState: 'Nuovo' })
    );
    renderModal();

    await uploadAPdf();

    expect(await screen.findByRole('alertdialog', {}, { timeout: 3000 })).toBeInTheDocument();
    expect(screen.getByText(/Possiedi Catan\?/)).toBeInTheDocument();
    expect(mockDeclareOwnership).not.toHaveBeenCalled();
  });

  it.each([
    ['the entry already has access to the manual', { hasRagAccess: true, currentState: 'Owned' }],
    [
      'the game is on the wishlist, where the domain refuses the declaration',
      { hasRagAccess: false, currentState: 'Wishlist' },
    ],
  ])('does not propose it when %s', async (_label, entry) => {
    mockGetGameDetail.mockResolvedValue(libraryEntry(entry));
    const onUploadSuccess = vi.fn();
    renderModal(onUploadSuccess);

    await uploadAPdf();
    await waitForTheUploadModalToClose();

    expect(onUploadSuccess).toHaveBeenCalledTimes(1);
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
  });

  it('does not propose it, and the upload still succeeds, when the game is not in the library', async () => {
    mockGetGameDetail.mockRejectedValue(new Error('Game not found in library'));
    const onUploadSuccess = vi.fn();
    renderModal(onUploadSuccess);

    await uploadAPdf();
    await waitForTheUploadModalToClose();

    expect(onUploadSuccess).toHaveBeenCalledTimes(1);
    expect(mockToastError).not.toHaveBeenCalled();
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
  });

  it('does not look at the library entry, and proposes nothing, when the upload fails', async () => {
    mockUploadPdf.mockRejectedValue(new Error('Upload failed'));
    mockGetGameDetail.mockResolvedValue(
      libraryEntry({ hasRagAccess: false, currentState: 'Nuovo' })
    );
    renderModal();

    await uploadAPdf();

    await waitFor(() => expect(mockToastError).toHaveBeenCalledWith('Upload failed'));
    expect(mockGetGameDetail).not.toHaveBeenCalled();
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
  });

  it('declining leaves the upload in place and declares nothing', async () => {
    mockGetGameDetail.mockResolvedValue(
      libraryEntry({ hasRagAccess: false, currentState: 'Nuovo' })
    );
    renderModal();
    const user = await uploadAPdf();
    await screen.findByRole('alertdialog', {}, { timeout: 3000 });

    await user.click(screen.getByRole('button', { name: /Non ancora/i }));

    await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument());
    expect(mockDeclareOwnership).not.toHaveBeenCalled();
    expect(mockUploadPdf).toHaveBeenCalledTimes(1);
  });

  it('confirming declares ownership, refreshes the library, and does not say the manual is readable yet', async () => {
    mockGetGameDetail.mockResolvedValue(
      libraryEntry({ hasRagAccess: false, currentState: 'Nuovo' })
    );
    const { invalidateQueries } = renderModal();
    const user = await uploadAPdf();
    await screen.findByRole('alertdialog', {}, { timeout: 3000 });

    await user.click(screen.getByTestId('ownership-checkbox'));
    await user.click(screen.getByRole('button', { name: /Conferma Possesso/i }));

    await waitFor(() => expect(mockDeclareOwnership).toHaveBeenCalledWith(GAME_ID));
    await waitFor(() => expect(invalidateQueries).toHaveBeenCalledWith({ queryKey: ['library'] }));
    const message = mockToastSuccess.mock.calls
      .map(call => String(call[0]))
      .find(text => text.includes('Possesso'));
    expect(message).toMatch(/appena sarà elaborato/);
    expect(message).not.toMatch(/non c'è ancora|può consultare/);
  });
});

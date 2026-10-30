/**
 * PdfUploadModal — ownership proposal after an upload (#4756)
 *
 * After an upload the modal proposes the ownership declaration, because without it the rulebook
 * the user just uploaded stays closed to the assistant (RagAccessService). It proposes and writes
 * nothing: the declaration is the user's explicit act. Validation, preview and closing are in
 * `__tests__/components/library/PdfUploadModal.test.tsx`.
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

interface Game {
  gameId: string;
  gameTitle: string;
}

const CATAN: Game = { gameId: 'game-1', gameTitle: 'Catan' };
const AZUL: Game = { gameId: 'game-2', gameTitle: 'Azul' };

function libraryEntry(overrides: { hasRagAccess: boolean; currentState: string }) {
  return { id: 'entry-1', gameId: CATAN.gameId, gameTitle: CATAN.gameTitle, ...overrides };
}

const WITHOUT_ACCESS = { hasRagAccess: false, currentState: 'Nuovo' };

const OWNERSHIP_RESULT = {
  gameState: 'Owned',
  ownershipDeclaredAt: '2026-10-29T10:00:00Z',
  hasRagAccess: true,
  kbCardCount: 0,
  isRagPublic: false,
};

function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>(res => {
    resolve = res;
  });
  return { promise, resolve };
}

function Harness({ game, onUploadSuccess }: { game: Game; onUploadSuccess?: () => void }) {
  const [isOpen, setIsOpen] = useState(true);
  return (
    <PdfUploadModal
      isOpen={isOpen}
      onClose={() => setIsOpen(false)}
      gameId={game.gameId}
      gameTitle={game.gameTitle}
      onUploadSuccess={onUploadSuccess}
    />
  );
}

function renderModal(onUploadSuccess?: () => void) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const invalidateQueries = vi.spyOn(queryClient, 'invalidateQueries');
  const tree = (game: Game) => (
    <QueryClientProvider client={queryClient}>
      <Harness game={game} onUploadSuccess={onUploadSuccess} />
    </QueryClientProvider>
  );
  const { rerender } = render(tree(CATAN));
  return { invalidateQueries, switchGameTo: (game: Game) => rerender(tree(game)) };
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

const ownershipToasts = () =>
  mockToastSuccess.mock.calls
    .map(call => String(call[0]))
    .filter(text => text.includes('Possesso'));

// ============================================================================
// Tests
// ============================================================================

describe('PdfUploadModal — ownership proposal after upload (#4756)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockUploadPdf.mockResolvedValue(undefined);
    mockDeclareOwnership.mockResolvedValue(OWNERSHIP_RESULT);
  });

  it('proposes the declaration, after the upload modal is gone, and writes nothing', async () => {
    mockGetGameDetail.mockResolvedValue(libraryEntry(WITHOUT_ACCESS));
    renderModal();

    await uploadAPdf();

    expect(await screen.findByRole('alertdialog', {}, { timeout: 3000 })).toBeInTheDocument();
    expect(screen.getByText(/Possiedi Catan\?/)).toBeInTheDocument();
    expect(screen.queryByTestId('pdf-upload-modal')).not.toBeInTheDocument();
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
    mockGetGameDetail.mockResolvedValue(libraryEntry(WITHOUT_ACCESS));
    renderModal();

    await uploadAPdf();

    await waitFor(() => expect(mockToastError).toHaveBeenCalledWith('Upload failed'));
    expect(mockGetGameDetail).not.toHaveBeenCalled();
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
  });

  it('closes on time even when the library lookup never answers, and proposes nothing', async () => {
    mockGetGameDetail.mockReturnValue(new Promise(() => {}));
    const onUploadSuccess = vi.fn();
    renderModal(onUploadSuccess);

    await uploadAPdf();
    await waitForTheUploadModalToClose();

    expect(onUploadSuccess).toHaveBeenCalledTimes(1);
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
  });

  it('proposes for the game that was uploaded to, even if the props change before the lookup answers', async () => {
    const lookup = deferred<ReturnType<typeof libraryEntry>>();
    mockGetGameDetail.mockReturnValue(lookup.promise);
    const { switchGameTo } = renderModal();
    const user = await uploadAPdf();
    await waitForTheUploadModalToClose();

    switchGameTo(AZUL);
    lookup.resolve(libraryEntry(WITHOUT_ACCESS));

    expect(await screen.findByText(/Possiedi Catan\?/)).toBeInTheDocument();
    await user.click(screen.getByTestId('ownership-checkbox'));
    await user.click(screen.getByRole('button', { name: /Conferma Possesso/i }));
    await waitFor(() => expect(mockDeclareOwnership).toHaveBeenCalledWith(CATAN.gameId));
    expect(mockDeclareOwnership).not.toHaveBeenCalledWith(AZUL.gameId);
  });

  it('declining leaves the upload in place and declares nothing', async () => {
    mockGetGameDetail.mockResolvedValue(libraryEntry(WITHOUT_ACCESS));
    renderModal();
    const user = await uploadAPdf();
    await screen.findByRole('alertdialog', {}, { timeout: 3000 });

    await user.click(screen.getByRole('button', { name: /Non ancora/i }));

    await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument());
    expect(mockDeclareOwnership).not.toHaveBeenCalled();
    expect(mockUploadPdf).toHaveBeenCalledTimes(1);
  });

  it('confirming declares ownership, refreshes the library, and does not say the manual is readable yet', async () => {
    mockGetGameDetail.mockResolvedValue(libraryEntry(WITHOUT_ACCESS));
    const { invalidateQueries } = renderModal();
    const user = await uploadAPdf();
    await screen.findByRole('alertdialog', {}, { timeout: 3000 });

    await user.click(screen.getByTestId('ownership-checkbox'));
    await user.click(screen.getByRole('button', { name: /Conferma Possesso/i }));

    await waitFor(() => expect(mockDeclareOwnership).toHaveBeenCalledWith(CATAN.gameId));
    await waitFor(() => expect(invalidateQueries).toHaveBeenCalledWith({ queryKey: ['library'] }));
    expect(ownershipToasts()).toHaveLength(1);
    expect(ownershipToasts()[0]).toMatch(/Catan.*appena sarà elaborato/);
    expect(ownershipToasts()[0]).not.toMatch(/non c'è ancora|può consultare/);
  });
});

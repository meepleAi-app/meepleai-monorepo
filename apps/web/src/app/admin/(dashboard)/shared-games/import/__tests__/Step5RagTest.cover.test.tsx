/**
 * Step 5 of the import wizard — #4413: the PDF page chosen as cover at step 2 becomes the game's
 * cover once the PDF is ready. The wizard used to drop the choice: the preview was a blob: URL of
 * the browser tab, never sent to the server.
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { useGameImportWizardStore } from '@/stores/useGameImportWizardStore';

import { Step5RagTest } from '../steps/Step5RagTest';

const mockSetPdfCoverFromPage = vi.fn();
vi.mock('@/lib/api', () => ({
  api: {
    sharedGames: {
      setPdfCoverFromPage: (...args: unknown[]) => mockSetPdfCoverFromPage(...args),
      searchKnowledgeBase: vi.fn(),
    },
  },
}));

const indexing = { isReady: true };
vi.mock('@/hooks/useIndexingStatus', () => ({
  useIndexingStatus: () => ({
    isReady: indexing.isReady,
    isFailed: false,
    chunkCount: 10,
    isPolling: false,
    error: null,
  }),
}));

vi.mock('next/link', () => ({
  default: ({ children, href }: { children: React.ReactNode; href: string }) => (
    <a href={href}>{children}</a>
  ),
}));

function seedImportedGame(coverPage: number | null) {
  useGameImportWizardStore.setState({
    importResult: { gameId: 'game-1', pdfDocumentId: 'pdf-1', indexingStatus: 'pending' },
    reviewedMetadata: { title: 'Catan', description: 'Trade and build.' },
    coverImage:
      coverPage == null
        ? { mode: 'placeholder', imageUrl: null }
        : { mode: 'pdf-page', imageUrl: null, pdfPageNumber: coverPage },
    coverAppliedPage: null,
    isIndexingReady: false,
    ragTestHistory: [],
  });
}

beforeEach(() => {
  vi.clearAllMocks();
  indexing.isReady = true;
});

describe('Step5RagTest — cover from the chosen PDF page (#4413)', () => {
  it('applies the chosen page once the PDF is ready', async () => {
    mockSetPdfCoverFromPage.mockResolvedValue(undefined);
    seedImportedGame(3);

    render(<Step5RagTest />);

    expect(await screen.findByText(/copertina impostata dalla pagina/i)).toBeInTheDocument();
    expect(mockSetPdfCoverFromPage).toHaveBeenCalledTimes(1);
    expect(mockSetPdfCoverFromPage).toHaveBeenCalledWith('game-1', 'pdf-1', 3);
    expect(useGameImportWizardStore.getState().coverAppliedPage).toBe(3);
  });

  it('waits while the PDF is still being processed', () => {
    indexing.isReady = false;
    seedImportedGame(3);

    render(<Step5RagTest />);

    expect(mockSetPdfCoverFromPage).not.toHaveBeenCalled();
  });

  it('does nothing with the placeholder', async () => {
    seedImportedGame(null);

    render(<Step5RagTest />);

    await screen.findByText(/indicizzazione completata/i);
    expect(mockSetPdfCoverFromPage).not.toHaveBeenCalled();
  });

  it('does not apply the same page twice', async () => {
    seedImportedGame(3);
    useGameImportWizardStore.setState({ coverAppliedPage: 3 });

    render(<Step5RagTest />);

    await screen.findByText(/copertina impostata dalla pagina/i);
    expect(mockSetPdfCoverFromPage).not.toHaveBeenCalled();
  });

  it('shows the failure and lets the admin retry', async () => {
    const user = userEvent.setup();
    mockSetPdfCoverFromPage
      .mockRejectedValueOnce(new Error('La copertina non è al momento disponibile'))
      .mockResolvedValueOnce(undefined);
    seedImportedGame(3);

    render(<Step5RagTest />);

    expect(await screen.findByText(/non è al momento disponibile/i)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Riprova' }));

    await waitFor(() => expect(mockSetPdfCoverFromPage).toHaveBeenCalledTimes(2));
    expect(await screen.findByText(/copertina impostata dalla pagina/i)).toBeInTheDocument();
  });
});

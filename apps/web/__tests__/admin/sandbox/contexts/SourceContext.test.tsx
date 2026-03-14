import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SourceProvider, useSource } from '@/components/admin/sandbox/contexts/SourceContext';
import { describe, it, expect, vi } from 'vitest';

const mockGetDocumentsByGame = vi.fn().mockResolvedValue([]);
const mockDeletePdf = vi.fn().mockResolvedValue(undefined);

vi.mock('@/lib/api', () => ({
  api: {
    sandbox: {
      getDocumentsByGame: (...args: unknown[]) => mockGetDocumentsByGame(...args),
      deletePdf: (...args: unknown[]) => mockDeletePdf(...args),
    },
  },
}));

function TestConsumer() {
  const { selectedGame, setSelectedGame, documents } = useSource();
  return (
    <div>
      <span data-testid="game">{selectedGame?.title ?? 'none'}</span>
      <span data-testid="docs">{documents.length}</span>
      <button
        onClick={() =>
          setSelectedGame({
            id: '1',
            title: 'Catan',
            pdfCount: 2,
            chunkCount: 12,
            vectorCount: 12,
          })
        }
      >
        select
      </button>
    </div>
  );
}

function LoadingConsumer() {
  const { isLoadingDocuments, refreshDocuments, selectedGame, setSelectedGame } = useSource();
  return (
    <div>
      <span data-testid="loading">{String(isLoadingDocuments)}</span>
      <button
        onClick={() => {
          if (!selectedGame) {
            setSelectedGame({
              id: '1',
              title: 'Catan',
              pdfCount: 0,
              chunkCount: 0,
              vectorCount: 0,
            });
          }
        }}
      >
        selectFirst
      </button>
      <button onClick={refreshDocuments}>refresh</button>
    </div>
  );
}

function DocumentsConsumer() {
  const { documents, setDocuments } = useSource();
  return (
    <div>
      <span data-testid="doc-count">{documents.length}</span>
      <button
        onClick={() =>
          setDocuments([
            {
              id: 'doc-1',
              fileName: 'rules.pdf',
              fileSize: 1024,
              status: 'Completed',
              chunkCount: 5,
              createdAt: '2026-01-01T00:00:00Z',
              updatedAt: '2026-01-01T00:00:00Z',
            },
          ])
        }
      >
        add doc
      </button>
    </div>
  );
}

function OutsideConsumer() {
  useSource();
  return <div />;
}

describe('SourceContext', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetDocumentsByGame.mockResolvedValue([]);
  });

  it('provides default values — selectedGame is null, documents is empty array, isLoadingDocuments is false', () => {
    render(
      <SourceProvider>
        <TestConsumer />
      </SourceProvider>
    );

    expect(screen.getByTestId('game').textContent).toBe('none');
    expect(screen.getByTestId('docs').textContent).toBe('0');
  });

  it('updates selected game — setSelectedGame updates the displayed game title and fetches documents', async () => {
    const user = userEvent.setup();

    render(
      <SourceProvider>
        <TestConsumer />
      </SourceProvider>
    );

    expect(screen.getByTestId('game').textContent).toBe('none');

    await user.click(screen.getByRole('button', { name: 'select' }));

    expect(screen.getByTestId('game').textContent).toBe('Catan');
    await waitFor(() => {
      expect(mockGetDocumentsByGame).toHaveBeenCalledWith('1');
    });
  });

  it('updates documents list via setDocuments', async () => {
    const user = userEvent.setup();

    render(
      <SourceProvider>
        <DocumentsConsumer />
      </SourceProvider>
    );

    expect(screen.getByTestId('doc-count').textContent).toBe('0');

    await user.click(screen.getByRole('button', { name: 'add doc' }));

    expect(screen.getByTestId('doc-count').textContent).toBe('1');
  });

  it('clears selected game when setSelectedGame is called with null', async () => {
    const user = userEvent.setup();

    function ClearableConsumer() {
      const { selectedGame, setSelectedGame } = useSource();
      return (
        <div>
          <span data-testid="game">{selectedGame?.title ?? 'none'}</span>
          <button
            onClick={() =>
              setSelectedGame({
                id: '1',
                title: 'Catan',
                pdfCount: 2,
                chunkCount: 12,
                vectorCount: 12,
              })
            }
          >
            select
          </button>
          <button onClick={() => setSelectedGame(null)}>clear</button>
        </div>
      );
    }

    render(
      <SourceProvider>
        <ClearableConsumer />
      </SourceProvider>
    );

    await user.click(screen.getByRole('button', { name: 'select' }));
    expect(screen.getByTestId('game').textContent).toBe('Catan');

    await user.click(screen.getByRole('button', { name: 'clear' }));
    expect(screen.getByTestId('game').textContent).toBe('none');
  });

  it('throws error when used outside provider', () => {
    const consoleError = console.error;
    console.error = () => {};

    expect(() => render(<OutsideConsumer />)).toThrow(
      'useSource must be used within SourceProvider'
    );

    console.error = consoleError;
  });
});

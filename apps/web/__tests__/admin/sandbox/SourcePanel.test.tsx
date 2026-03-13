import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import { GameSearchCombobox } from '@/components/admin/sandbox/GameSearchCombobox';
import { DocumentRow } from '@/components/admin/sandbox/DocumentRow';
import { DocumentList } from '@/components/admin/sandbox/DocumentList';
import { SourcePanel } from '@/components/admin/sandbox/SourcePanel';
import { SourceProvider } from '@/components/admin/sandbox/contexts/SourceContext';
import type {
  SharedGameSummary,
  PdfDocumentSummary,
} from '@/components/admin/sandbox/contexts/SourceContext';

// ---------------------------------------------------------------------------
// Fixtures
// ---------------------------------------------------------------------------

const mockGame: SharedGameSummary = {
  id: 'game-1',
  title: 'Catan',
  publisher: 'Kosmos',
  pdfCount: 3,
  chunkCount: 42,
  vectorCount: 42,
};

const mockGame2: SharedGameSummary = {
  id: 'game-2',
  title: 'Wingspan',
  publisher: 'Stonemaier Games',
  pdfCount: 1,
  chunkCount: 18,
  vectorCount: 18,
};

const completedDoc: PdfDocumentSummary = {
  id: 'doc-1',
  fileName: 'catan-rules.pdf',
  fileSize: 2_400_000,
  status: 'Completed',
  chunkCount: 12,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
};

const extractingDoc: PdfDocumentSummary = {
  id: 'doc-2',
  fileName: 'catan-faq.pdf',
  fileSize: 512_000,
  status: 'Extracting',
  chunkCount: 0,
  createdAt: '2026-01-02T00:00:00Z',
  updatedAt: '2026-01-02T00:00:00Z',
};

const failedDoc: PdfDocumentSummary = {
  id: 'doc-3',
  fileName: 'corrupted.pdf',
  fileSize: 100,
  status: 'Failed',
  chunkCount: 0,
  createdAt: '2026-01-03T00:00:00Z',
  updatedAt: '2026-01-03T00:00:00Z',
};

// ---------------------------------------------------------------------------
// GameSearchCombobox
// ---------------------------------------------------------------------------

describe('GameSearchCombobox', () => {
  it('renders the search trigger button', () => {
    render(
      <GameSearchCombobox games={[]} isLoading={false} onSearch={vi.fn()} onSelect={vi.fn()} />
    );

    expect(screen.getByRole('combobox')).toBeInTheDocument();
    expect(screen.getByText('Cerca un gioco...')).toBeInTheDocument();
  });

  it('shows game results in the dropdown when opened', async () => {
    const user = userEvent.setup();

    render(
      <GameSearchCombobox
        games={[mockGame, mockGame2]}
        isLoading={false}
        onSearch={vi.fn()}
        onSelect={vi.fn()}
      />
    );

    await user.click(screen.getByRole('combobox'));

    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Wingspan')).toBeInTheDocument();
    expect(screen.getByText(/Kosmos/)).toBeInTheDocument();
    expect(screen.getByText(/3 PDF/)).toBeInTheDocument();
  });

  it('calls onSelect when a game is clicked', async () => {
    const user = userEvent.setup();
    const onSelect = vi.fn();

    render(
      <GameSearchCombobox
        games={[mockGame]}
        isLoading={false}
        onSearch={vi.fn()}
        onSelect={onSelect}
      />
    );

    await user.click(screen.getByRole('combobox'));
    await user.click(screen.getByText('Catan'));

    expect(onSelect).toHaveBeenCalledWith(mockGame);
  });

  it('shows empty message when no games match', async () => {
    const user = userEvent.setup();

    render(
      <GameSearchCombobox games={[]} isLoading={false} onSearch={vi.fn()} onSelect={vi.fn()} />
    );

    await user.click(screen.getByRole('combobox'));

    expect(screen.getByText('Nessun gioco trovato')).toBeInTheDocument();
  });
});

// ---------------------------------------------------------------------------
// DocumentRow
// ---------------------------------------------------------------------------

describe('DocumentRow', () => {
  it('renders filename, status badge, chunk count, and file size', () => {
    render(<DocumentRow doc={completedDoc} onReindex={vi.fn()} onDelete={vi.fn()} />);

    expect(screen.getByText('catan-rules.pdf')).toBeInTheDocument();
    expect(screen.getByText('Completato')).toBeInTheDocument();
    expect(screen.getByText(/12 chunk/)).toBeInTheDocument();
    expect(screen.getByText(/2\.3 MB/)).toBeInTheDocument();
  });

  it('shows spinner for processing statuses', () => {
    const { container } = render(
      <DocumentRow doc={extractingDoc} onReindex={vi.fn()} onDelete={vi.fn()} />
    );

    expect(screen.getByText('Estrazione')).toBeInTheDocument();
    expect(container.querySelector('.animate-spin')).toBeInTheDocument();
  });

  it('disables reindex button during processing', () => {
    render(<DocumentRow doc={extractingDoc} onReindex={vi.fn()} onDelete={vi.fn()} />);

    expect(screen.getByLabelText('Reindicizza')).toBeDisabled();
  });

  it('enables reindex button when completed', () => {
    render(<DocumentRow doc={completedDoc} onReindex={vi.fn()} onDelete={vi.fn()} />);

    expect(screen.getByLabelText('Reindicizza')).toBeEnabled();
  });

  it('shows delete confirmation dialog when delete is clicked', async () => {
    const user = userEvent.setup();

    render(<DocumentRow doc={completedDoc} onReindex={vi.fn()} onDelete={vi.fn()} />);

    await user.click(screen.getByLabelText('Elimina'));

    expect(screen.getByText('Elimina documento')).toBeInTheDocument();
    expect(screen.getByText(/Sei sicuro/)).toBeInTheDocument();
    expect(screen.getByText('Annulla')).toBeInTheDocument();
  });

  it('calls onDelete when delete is confirmed', async () => {
    const user = userEvent.setup();
    const onDelete = vi.fn();

    render(<DocumentRow doc={completedDoc} onReindex={vi.fn()} onDelete={onDelete} />);

    await user.click(screen.getByLabelText('Elimina'));

    // Click the confirm "Elimina" button inside the dialog
    const dialog = screen.getByRole('alertdialog');
    await user.click(within(dialog).getByRole('button', { name: 'Elimina' }));

    expect(onDelete).toHaveBeenCalledWith('doc-1');
  });

  it('renders failed status with red styling', () => {
    render(<DocumentRow doc={failedDoc} onReindex={vi.fn()} onDelete={vi.fn()} />);

    expect(screen.getByText('Fallito')).toBeInTheDocument();
  });
});

// ---------------------------------------------------------------------------
// DocumentList
// ---------------------------------------------------------------------------

describe('DocumentList', () => {
  it('renders document rows for each document', () => {
    render(
      <DocumentList
        documents={[completedDoc, extractingDoc]}
        onReindex={vi.fn()}
        onDelete={vi.fn()}
      />
    );

    expect(screen.getByText('catan-rules.pdf')).toBeInTheDocument();
    expect(screen.getByText('catan-faq.pdf')).toBeInTheDocument();
  });

  it('shows empty state when no documents', () => {
    render(<DocumentList documents={[]} onReindex={vi.fn()} onDelete={vi.fn()} />);

    expect(screen.getByText('Nessun documento caricato')).toBeInTheDocument();
  });
});

// ---------------------------------------------------------------------------
// SourcePanel (integration)
// ---------------------------------------------------------------------------

describe('SourcePanel', () => {
  it('renders panel title and search combobox', () => {
    render(
      <SourceProvider>
        <SourcePanel />
      </SourceProvider>
    );

    expect(screen.getByText('Source')).toBeInTheDocument();
    expect(screen.getByRole('combobox')).toBeInTheDocument();
  });

  it('does not show document list or upload zone when no game selected', () => {
    render(
      <SourceProvider>
        <SourcePanel />
      </SourceProvider>
    );

    expect(screen.queryByText('Documenti')).not.toBeInTheDocument();
    expect(screen.queryByText('Trascina un PDF o clicca per caricare')).not.toBeInTheDocument();
  });
});

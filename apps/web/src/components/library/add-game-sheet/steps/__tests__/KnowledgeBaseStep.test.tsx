/**
 * Tests for KnowledgeBaseStep
 * Issue #4820: Step 2 Knowledge Base & PDF Management
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, expect, it, vi, beforeEach } from 'vitest';

import { useAddGameWizardStore } from '@/lib/stores/add-game-wizard-store';

// Mock the API module
vi.mock('@/lib/api', () => ({
  api: {
    documents: {
      getDocumentsByGame: vi.fn(),
      acceptDisclaimer: vi.fn().mockResolvedValue({ success: true, message: 'ok' }),
    },
    pdf: {
      uploadPdf: vi.fn(),
    },
  },
}));

// Mock RagReadyIndicator (complex component with polling)
vi.mock('@/components/pdf/RagReadyIndicator', () => ({
  RagReadyIndicator: ({
    gameId,
    'data-testid': testId,
  }: {
    gameId: string;
    'data-testid'?: string;
  }) => <div data-testid={testId ?? 'rag-ready-indicator'}>Embedding status for {gameId}</div>,
}));

// Mock CopyrightDisclaimerModal: renders a button that triggers onAccept when open,
// so existing upload tests can click through the disclaimer without the real modal.
vi.mock('@/components/pdf/CopyrightDisclaimerModal', () => ({
  CopyrightDisclaimerModal: ({
    open,
    onAccept,
  }: {
    open: boolean;
    onAccept: () => void;
    onCancel: () => void;
  }) =>
    open ? (
      <button data-testid="mock-disclaimer-accept" onClick={onAccept}>
        accept
      </button>
    ) : null,
}));

import { api } from '@/lib/api';
import { KnowledgeBaseStep } from '../KnowledgeBaseStep';

const mockApi = api as {
  documents: {
    getDocumentsByGame: ReturnType<typeof vi.fn>;
    acceptDisclaimer: ReturnType<typeof vi.fn>;
  };
  pdf: { uploadPdf: ReturnType<typeof vi.fn> };
};

const mockDocs = [
  {
    id: 'doc-1',
    gameId: 'game-abc',
    fileName: 'Catan-Rules.pdf',
    filePath: '/uploads/catan.pdf',
    fileSizeBytes: 2_000_000,
    processingStatus: 'Completed',
    uploadedAt: '2026-02-19T10:00:00Z',
    processedAt: '2026-02-19T10:05:00Z',
    pageCount: 12,
    documentType: 'base',
    isPublic: true,
  },
];

function initializeStoreWithGame() {
  useAddGameWizardStore.getState().initialize(
    { type: 'fromGameCard', sharedGameId: 'shared-1' },
    {
      gameId: 'game-abc',
      title: 'Catan',
      source: 'catalog',
    }
  );
}

describe('KnowledgeBaseStep', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useAddGameWizardStore.getState().reset();
    mockApi.documents.getDocumentsByGame.mockResolvedValue([]);
  });

  // --- Rendering ---

  it('renders step header', () => {
    initializeStoreWithGame();
    render(<KnowledgeBaseStep />);

    expect(screen.getByTestId('knowledge-base-step')).toBeInTheDocument();
    expect(screen.getByText('Knowledge Base')).toBeInTheDocument();
  });

  it('renders skip info text', () => {
    initializeStoreWithGame();
    render(<KnowledgeBaseStep />);

    expect(screen.getByTestId('skip-info')).toBeInTheDocument();
    expect(screen.getByText(/Questo passaggio è opzionale/)).toBeInTheDocument();
  });

  // --- Existing PDFs ---

  it('loads and displays existing PDFs', async () => {
    initializeStoreWithGame();
    mockApi.documents.getDocumentsByGame.mockResolvedValueOnce(mockDocs);

    render(<KnowledgeBaseStep />);

    // Should show loading initially
    expect(screen.getByText('Caricamento documenti...')).toBeInTheDocument();

    // Then show the documents
    await waitFor(() => {
      expect(screen.getByText('Catan-Rules.pdf')).toBeInTheDocument();
    });

    expect(mockApi.documents.getDocumentsByGame).toHaveBeenCalledWith('game-abc');
  });

  it('shows empty state when no PDFs', async () => {
    initializeStoreWithGame();
    mockApi.documents.getDocumentsByGame.mockResolvedValueOnce([]);

    render(<KnowledgeBaseStep />);

    // Loading resolves to empty - should not show the "PDF disponibili" section
    await waitFor(() => {
      expect(screen.queryByText('Caricamento documenti...')).not.toBeInTheDocument();
    });

    // Upload button should be visible
    expect(screen.getByTestId('show-upload-button')).toBeInTheDocument();
  });

  it('handles API error gracefully', async () => {
    initializeStoreWithGame();
    mockApi.documents.getDocumentsByGame.mockRejectedValueOnce(new Error('Network error'));

    render(<KnowledgeBaseStep />);

    // Should recover gracefully (no crash)
    await waitFor(() => {
      expect(screen.queryByText('Caricamento documenti...')).not.toBeInTheDocument();
    });
  });

  // --- Upload toggle ---

  it('shows upload button by default', async () => {
    initializeStoreWithGame();
    render(<KnowledgeBaseStep />);

    await waitFor(() => {
      expect(screen.getByTestId('show-upload-button')).toBeInTheDocument();
    });
  });

  it('shows upload zone when upload button clicked', async () => {
    initializeStoreWithGame();
    render(<KnowledgeBaseStep />);

    await waitFor(() => {
      expect(screen.getByTestId('show-upload-button')).toBeInTheDocument();
    });

    // Click upload button → shows disclaimer modal (mocked), accept it → shows upload zone
    fireEvent.click(screen.getByTestId('show-upload-button'));
    await waitFor(() => {
      expect(screen.getByTestId('mock-disclaimer-accept')).toBeInTheDocument();
    });
    fireEvent.click(screen.getByTestId('mock-disclaimer-accept'));
    expect(screen.getByTestId('pdf-upload-zone')).toBeInTheDocument();
  });

  // --- Upload completion ---

  it('shows success message after upload', async () => {
    initializeStoreWithGame();
    mockApi.pdf.uploadPdf.mockResolvedValueOnce({
      documentId: 'doc-new',
      fileName: 'custom-rules.pdf',
    });

    render(<KnowledgeBaseStep />);

    await waitFor(() => {
      expect(screen.getByTestId('show-upload-button')).toBeInTheDocument();
    });

    // Open upload zone via disclaimer flow
    fireEvent.click(screen.getByTestId('show-upload-button'));
    await waitFor(() => {
      expect(screen.getByTestId('mock-disclaimer-accept')).toBeInTheDocument();
    });
    fireEvent.click(screen.getByTestId('mock-disclaimer-accept'));

    // Select a file
    const input = screen.getByTestId('pdf-file-input');
    const file = new File(['pdf content'], 'custom-rules.pdf', { type: 'application/pdf' });
    fireEvent.change(input, { target: { files: [file] } });

    // Upload
    fireEvent.click(screen.getByTestId('pdf-upload-button'));

    await waitFor(() => {
      expect(screen.getByTestId('upload-success')).toBeInTheDocument();
      expect(screen.getByText('PDF caricato con successo')).toBeInTheDocument();
    });

    // Wizard store should be marked dirty
    expect(useAddGameWizardStore.getState().customPdfUploaded).toBe(true);
  });

  // --- Embedding status ---

  it('shows embedding status after upload', async () => {
    initializeStoreWithGame();
    mockApi.pdf.uploadPdf.mockResolvedValueOnce({
      documentId: 'doc-new',
      fileName: 'rules.pdf',
    });

    render(<KnowledgeBaseStep />);

    await waitFor(() => {
      expect(screen.getByTestId('show-upload-button')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByTestId('show-upload-button'));
    await waitFor(() => {
      expect(screen.getByTestId('mock-disclaimer-accept')).toBeInTheDocument();
    });
    fireEvent.click(screen.getByTestId('mock-disclaimer-accept'));

    const input = screen.getByTestId('pdf-file-input');
    fireEvent.change(input, {
      target: { files: [new File(['data'], 'rules.pdf', { type: 'application/pdf' })] },
    });
    fireEvent.click(screen.getByTestId('pdf-upload-button'));

    await waitFor(() => {
      expect(screen.getByTestId('kb-embedding-status')).toBeInTheDocument();
    });
  });

  // --- No game selected ---

  it('renders without crashing when no game selected', () => {
    // Store is reset, no game selected
    render(<KnowledgeBaseStep />);
    expect(screen.getByTestId('knowledge-base-step')).toBeInTheDocument();
  });

  it('does not call API when no game selected', async () => {
    // Store is reset, gameId is null
    render(<KnowledgeBaseStep />);

    // Wait a tick so any pending effects run
    await waitFor(() => {
      expect(mockApi.documents.getDocumentsByGame).not.toHaveBeenCalled();
    });
  });

  it('shows upload error when no game and upload attempted', async () => {
    // Store is reset, gameId is null
    render(<KnowledgeBaseStep />);

    // Open upload zone via disclaimer flow
    fireEvent.click(screen.getByTestId('show-upload-button'));
    await waitFor(() => {
      expect(screen.getByTestId('mock-disclaimer-accept')).toBeInTheDocument();
    });
    fireEvent.click(screen.getByTestId('mock-disclaimer-accept'));

    // Select a file
    const input = screen.getByTestId('pdf-file-input');
    fireEvent.change(input, {
      target: { files: [new File(['data'], 'test.pdf', { type: 'application/pdf' })] },
    });

    // Attempt upload - should fail with "Nessun gioco selezionato"
    fireEvent.click(screen.getByTestId('pdf-upload-button'));

    await waitFor(() => {
      expect(screen.getByTestId('pdf-upload-error')).toBeInTheDocument();
      expect(screen.getByText('Nessun gioco selezionato')).toBeInTheDocument();
    });
  });
});

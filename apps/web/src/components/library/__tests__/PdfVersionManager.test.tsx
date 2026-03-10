/**
 * PdfVersionManager Tests
 * Task 6: PDF Version Manager with replace/keep flow
 *
 * Test Coverage:
 * - Lists existing PDFs with version labels and status
 * - Shows upload form when clicking new version button
 * - Toggles RAG status via api.documents.setActiveForRag
 * - Shows empty state when no PDFs
 * - Shows "replace or keep both?" dialog when game already has PDF
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { PdfVersionManager } from '../PdfVersionManager';

// ============================================================================
// Mocks
// ============================================================================

vi.mock('@tanstack/react-query', async importOriginal => {
  const actual = await importOriginal<typeof import('@tanstack/react-query')>();
  return {
    ...actual,
    useQuery: vi.fn(),
    useMutation: vi.fn(),
    useQueryClient: vi.fn(),
  };
});

vi.mock('@/lib/api', () => ({
  api: {
    documents: {
      getDocumentsByGame: vi.fn(),
      setActiveForRag: vi.fn(),
    },
  },
}));

// PdfUploadZone is a complex component — stub it out
vi.mock('@/components/library/add-game-sheet/steps/PdfUploadZone', () => ({
  PdfUploadZone: ({
    onUploadComplete,
  }: {
    onUploadComplete: (documentId: string, fileName: string, fileSizeBytes: number) => void;
  }) => (
    <div data-testid="pdf-upload-zone">
      <button
        data-testid="simulate-upload-complete"
        onClick={() => onUploadComplete('doc-new', 'new-rulebook.pdf', 1024 * 1024)}
      >
        Simula Upload
      </button>
    </div>
  ),
}));

// ============================================================================
// Test Data
// ============================================================================

import type { PdfDocumentDto } from '@/lib/api/schemas/pdf.schemas';

const makePdf = (overrides: Partial<PdfDocumentDto> = {}): PdfDocumentDto => ({
  id: 'doc-1',
  gameId: 'game-1',
  fileName: 'rulebook.pdf',
  filePath: '/uploads/rulebook.pdf',
  fileSizeBytes: 2048000,
  processingStatus: 'Completed',
  uploadedAt: '2026-01-01T10:00:00.000Z',
  processedAt: '2026-01-01T10:05:00.000Z',
  pageCount: 48,
  documentType: 'base',
  isPublic: false,
  processingState: 'Ready',
  progressPercentage: 100,
  retryCount: 0,
  maxRetries: 3,
  canRetry: false,
  errorCategory: null,
  processingError: null,
  documentCategory: 'Rulebook',
  baseDocumentId: null,
  isActiveForRag: true,
  hasAcceptedDisclaimer: true,
  versionLabel: '1a Edizione',
  ...overrides,
});

// ============================================================================
// Helpers
// ============================================================================

let mockUseQuery: ReturnType<typeof vi.fn>;
let mockUseMutation: ReturnType<typeof vi.fn>;
let mockUseQueryClient: ReturnType<typeof vi.fn>;
let mockInvalidateQueries: ReturnType<typeof vi.fn>;
let mockMutateAsync: ReturnType<typeof vi.fn>;

const setup = (pdfs: PdfDocumentDto[] = [makePdf()]) => {
  mockInvalidateQueries = vi.fn().mockResolvedValue(undefined);
  mockUseQueryClient.mockReturnValue({ invalidateQueries: mockInvalidateQueries });

  mockUseQuery.mockReturnValue({
    data: pdfs,
    isLoading: false,
    isError: false,
    error: null,
  });

  mockMutateAsync = vi.fn().mockResolvedValue({ success: true, isActive: true, message: 'OK' });
  mockUseMutation.mockReturnValue({
    mutateAsync: mockMutateAsync,
    isPending: false,
  });

  return render(<PdfVersionManager gameId="game-1" gameName="Catan" />);
};

// ============================================================================
// Test Setup
// ============================================================================

beforeEach(async () => {
  vi.clearAllMocks();

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const queryModule = (await import('@tanstack/react-query')) as any;
  mockUseQuery = queryModule.useQuery;
  mockUseMutation = queryModule.useMutation;
  mockUseQueryClient = queryModule.useQueryClient;
});

// ============================================================================
// Rendering Tests
// ============================================================================

describe('PdfVersionManager - Rendering', () => {
  it('lists existing PDFs with version labels', async () => {
    setup([makePdf({ versionLabel: '1a Edizione', isActiveForRag: true })]);

    await waitFor(() => {
      expect(screen.getByText('rulebook.pdf')).toBeInTheDocument();
    });

    expect(screen.getByText('1a Edizione')).toBeInTheDocument();
    expect(screen.getByText('Attivo')).toBeInTheDocument();
  });

  it('shows empty state when no PDFs', async () => {
    setup([]);

    await waitFor(() => {
      expect(screen.getByTestId('pdf-version-manager-empty')).toBeInTheDocument();
    });

    expect(screen.getByText('Nessun regolamento caricato')).toBeInTheDocument();
  });

  it('shows loading state while fetching', async () => {
    mockInvalidateQueries = vi.fn().mockResolvedValue(undefined);
    mockUseQueryClient.mockReturnValue({ invalidateQueries: mockInvalidateQueries });
    mockUseQuery.mockReturnValue({ data: undefined, isLoading: true, isError: false, error: null });
    mockMutateAsync = vi.fn();
    mockUseMutation.mockReturnValue({ mutateAsync: mockMutateAsync, isPending: false });

    render(<PdfVersionManager gameId="game-1" gameName="Catan" />);

    expect(screen.getByTestId('pdf-version-manager-loading')).toBeInTheDocument();
  });

  it('shows category badge for each PDF', async () => {
    setup([makePdf({ documentCategory: 'Rulebook' })]);

    await waitFor(() => {
      // The badge shows the Italian label "Regolamento" for Rulebook category
      expect(screen.getByText('Regolamento')).toBeInTheDocument();
    });
  });

  it('shows version label dash when no label set', async () => {
    setup([makePdf({ versionLabel: null })]);

    await waitFor(() => {
      expect(screen.getByText('rulebook.pdf')).toBeInTheDocument();
    });

    // Should show a dash or placeholder
    expect(screen.getByTestId('version-label-doc-1')).toHaveTextContent('—');
  });

  it('shows inactive status for non-active PDF', async () => {
    setup([makePdf({ isActiveForRag: false })]);

    await waitFor(() => {
      expect(screen.getByText('Inattivo')).toBeInTheDocument();
    });
  });
});

// ============================================================================
// Upload Form Tests
// ============================================================================

describe('PdfVersionManager - Upload Form', () => {
  it('shows upload form when clicking new version button', async () => {
    const user = userEvent.setup();
    setup();

    const addButton = screen.getByRole('button', { name: /Carica nuova versione/i });
    await user.click(addButton);

    await waitFor(() => {
      expect(screen.getByTestId('upload-form')).toBeInTheDocument();
    });

    expect(screen.getByLabelText(/Etichetta versione/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/Categoria/i)).toBeInTheDocument();
    expect(screen.getByTestId('pdf-upload-zone')).toBeInTheDocument();
  });

  it('hides upload form when cancel is clicked', async () => {
    const user = userEvent.setup();
    setup();

    await user.click(screen.getByRole('button', { name: /Carica nuova versione/i }));

    await waitFor(() => {
      expect(screen.getByTestId('upload-form')).toBeInTheDocument();
    });

    const cancelButton = screen.getByRole('button', { name: /Annulla/i });
    await user.click(cancelButton);

    await waitFor(() => {
      expect(screen.queryByTestId('upload-form')).not.toBeInTheDocument();
    });
  });

  it('allows entering a version label', async () => {
    const user = userEvent.setup();
    setup();

    await user.click(screen.getByRole('button', { name: /Carica nuova versione/i }));

    const labelInput = screen.getByLabelText(/Etichetta versione/i);
    await user.type(labelInput, '2a Edizione');

    expect(labelInput).toHaveValue('2a Edizione');
  });
});

// ============================================================================
// RAG Toggle Tests
// ============================================================================

describe('PdfVersionManager - RAG Toggle', () => {
  it('toggles RAG status when toggle is clicked', async () => {
    const user = userEvent.setup();
    setup([makePdf({ isActiveForRag: true })]);

    await waitFor(() => {
      expect(screen.getByText('rulebook.pdf')).toBeInTheDocument();
    });

    const toggle = screen.getByRole('button', { name: /Attivo/i });
    await user.click(toggle);

    await waitFor(() => {
      expect(mockMutateAsync).toHaveBeenCalledWith({ pdfId: 'doc-1', isActive: false });
    });
  });

  it('calls setActiveForRag with true when activating an inactive PDF', async () => {
    const user = userEvent.setup();
    setup([makePdf({ isActiveForRag: false })]);

    await waitFor(() => {
      expect(screen.getByText('rulebook.pdf')).toBeInTheDocument();
    });

    const toggle = screen.getByRole('button', { name: /Inattivo/i });
    await user.click(toggle);

    await waitFor(() => {
      expect(mockMutateAsync).toHaveBeenCalledWith({ pdfId: 'doc-1', isActive: true });
    });
  });
});

// ============================================================================
// Replace or Keep Both Dialog Tests
// ============================================================================

describe('PdfVersionManager - Replace or Keep Both', () => {
  it('shows replace/keep dialog after upload when existing PDFs are present', async () => {
    const user = userEvent.setup();
    setup([makePdf({ isActiveForRag: true })]);

    await user.click(screen.getByRole('button', { name: /Carica nuova versione/i }));

    await waitFor(() => {
      expect(screen.getByTestId('upload-form')).toBeInTheDocument();
    });

    // Simulate upload completion
    await user.click(screen.getByTestId('simulate-upload-complete'));

    await waitFor(() => {
      expect(screen.getByTestId('replace-keep-dialog')).toBeInTheDocument();
    });

    expect(screen.getByText(/Sostituisci/i)).toBeInTheDocument();
    expect(screen.getByText(/Tieni entrambi/i)).toBeInTheDocument();
  });

  it('deactivates old PDFs when "Sostituisci" is clicked', async () => {
    const user = userEvent.setup();
    setup([makePdf({ id: 'doc-old', isActiveForRag: true })]);

    await user.click(screen.getByRole('button', { name: /Carica nuova versione/i }));

    await waitFor(() => {
      expect(screen.getByTestId('upload-form')).toBeInTheDocument();
    });

    await user.click(screen.getByTestId('simulate-upload-complete'));

    await waitFor(() => {
      expect(screen.getByTestId('replace-keep-dialog')).toBeInTheDocument();
    });

    const replaceButton = screen.getByRole('button', { name: /Sostituisci/i });
    await user.click(replaceButton);

    await waitFor(() => {
      expect(mockMutateAsync).toHaveBeenCalledWith({ pdfId: 'doc-old', isActive: false });
    });
  });

  it('closes dialog without deactivating when "Tieni entrambi" is clicked', async () => {
    const user = userEvent.setup();
    setup([makePdf({ isActiveForRag: true })]);

    await user.click(screen.getByRole('button', { name: /Carica nuova versione/i }));
    await user.click(screen.getByTestId('simulate-upload-complete'));

    await waitFor(() => {
      expect(screen.getByTestId('replace-keep-dialog')).toBeInTheDocument();
    });

    const keepBothButton = screen.getByRole('button', { name: /Tieni entrambi/i });
    await user.click(keepBothButton);

    await waitFor(() => {
      expect(screen.queryByTestId('replace-keep-dialog')).not.toBeInTheDocument();
    });

    // Should NOT deactivate any PDFs
    expect(mockMutateAsync).not.toHaveBeenCalled();
  });

  it('does not show dialog when no existing PDFs', async () => {
    const user = userEvent.setup();
    setup([]);

    await user.click(screen.getByRole('button', { name: /Carica nuova versione/i }));
    await user.click(screen.getByTestId('simulate-upload-complete'));

    await waitFor(() => {
      expect(screen.queryByTestId('replace-keep-dialog')).not.toBeInTheDocument();
    });
  });
});

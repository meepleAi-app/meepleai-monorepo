/**
 * KbDocActions unit tests — Issue #1653 F3-FU-4 Task 6.
 *
 * Mocks:
 *  - @/hooks/queries/useKbDocActions  (useDeleteKbDoc, useReindexDoc)
 *  - @/hooks/queries/useKbDocConsumingAgents (lazy used-by count)
 *  - @/lib/api  (api.pdf.getPdfDownloadUrl, api.pdf.exportDocChunks)
 *  - ../utils/downloadAsFile
 *  - sonner
 *  - next/link
 *  - next/navigation
 */
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';

// ── next/link mock ─────────────────────────────────────────────────────────────
vi.mock('next/link', () => ({
  default: ({
    href,
    children,
    ...rest
  }: {
    href: string;
    children: ReactNode;
    [k: string]: unknown;
  }) => (
    <a href={href} {...rest}>
      {children}
    </a>
  ),
}));

// ── next/navigation mock ───────────────────────────────────────────────────────
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
  useSearchParams: () => new URLSearchParams(''),
  usePathname: () => '/admin/knowledge-base',
}));

// ── sonner mock ────────────────────────────────────────────────────────────────
const mockToastSuccess = vi.fn();
const mockToastError = vi.fn();
vi.mock('sonner', () => ({
  toast: {
    success: (...args: unknown[]) => mockToastSuccess(...args),
    error: (...args: unknown[]) => mockToastError(...args),
  },
}));

// ── useKbDocActions mock ───────────────────────────────────────────────────────
const mockReindexMutate = vi.fn();
const mockDeleteMutate = vi.fn();
const mockUseReindexDoc = vi.fn();
const mockUseDeleteKbDoc = vi.fn();

vi.mock('@/hooks/queries/useKbDocActions', () => ({
  useReindexDoc: (docId: string) => mockUseReindexDoc(docId),
  useDeleteKbDoc: (gameId: string | null) => mockUseDeleteKbDoc(gameId),
}));

// ── useKbDocConsumingAgents mock ───────────────────────────────────────────────
const mockUseKbDocConsumingAgents = vi.fn();
vi.mock('@/hooks/queries/useKbDocConsumingAgents', () => ({
  useKbDocConsumingAgents: (opts: unknown) => mockUseKbDocConsumingAgents(opts),
}));

// ── api.pdf mock ───────────────────────────────────────────────────────────────
const mockExportDocChunks = vi.fn();
const mockGetPdfDownloadUrl = vi.fn();
vi.mock('@/lib/api', () => ({
  api: {
    pdf: {
      exportDocChunks: (...args: unknown[]) => mockExportDocChunks(...args),
      getPdfDownloadUrl: (...args: unknown[]) => mockGetPdfDownloadUrl(...args),
    },
  },
}));

// ── downloadAsFile mock ────────────────────────────────────────────────────────
// The component lives in actions/ and imports from ../utils/downloadAsFile
// which resolves to explorer/utils/downloadAsFile. From the __tests__/ subdir
// the equivalent relative path would be ../../utils/downloadAsFile, but using
// the module ID that Vite resolves for the component import is more robust.
const mockDownloadAsFile = vi.fn();
vi.mock('../../utils/downloadAsFile', () => ({
  downloadAsFile: (...args: unknown[]) => mockDownloadAsFile(...args),
}));

// ── Import after mocks ─────────────────────────────────────────────────────────
import { KbDocActions } from '../KbDocActions';

// ── Helpers ────────────────────────────────────────────────────────────────────

const BASE_PROPS = {
  docId: 'doc-123',
  fileName: 'wingspan-rules.pdf',
  gameId: 'game-456',
  processingStatus: 'ready' as const,
};

function makeReindexMutation(overrides: Record<string, unknown> = {}) {
  return {
    mutate: mockReindexMutate,
    isPending: false,
    isError: false,
    ...overrides,
  };
}

function makeDeleteMutation(overrides: Record<string, unknown> = {}) {
  return {
    mutate: mockDeleteMutate,
    isPending: false,
    isError: false,
    ...overrides,
  };
}

function makeConsumingAgentsQuery(overrides: Record<string, unknown> = {}) {
  return {
    isLoading: false,
    isError: false,
    data: [],
    ...overrides,
  };
}

describe('KbDocActions', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockUseReindexDoc.mockReturnValue(makeReindexMutation());
    mockUseDeleteKbDoc.mockReturnValue(makeDeleteMutation());
    mockUseKbDocConsumingAgents.mockReturnValue(makeConsumingAgentsQuery());
    mockGetPdfDownloadUrl.mockReturnValue('http://api/download/doc-123');
    mockExportDocChunks.mockResolvedValue([
      { id: 'c1', chunkIndex: 0, pageNumber: 1, heading: 'Intro', content: 'text' },
    ]);
  });

  // ── Re-index ─────────────────────────────────────────────────────────────────

  it('renders the Re-index button', () => {
    render(<KbDocActions {...BASE_PROPS} />);
    expect(screen.getByRole('button', { name: /re-?index/i })).toBeInTheDocument();
  });

  it('disables Re-index while processingStatus is "processing"', () => {
    render(<KbDocActions {...BASE_PROPS} processingStatus="processing" />);
    expect(screen.getByRole('button', { name: /re-?index/i })).toBeDisabled();
  });

  it('disables Re-index while processingStatus is "queued"', () => {
    render(<KbDocActions {...BASE_PROPS} processingStatus="queued" />);
    expect(screen.getByRole('button', { name: /re-?index/i })).toBeDisabled();
  });

  it('disables Re-index while mutation isPending', () => {
    mockUseReindexDoc.mockReturnValue(makeReindexMutation({ isPending: true }));
    render(<KbDocActions {...BASE_PROPS} processingStatus="ready" />);
    expect(screen.getByRole('button', { name: /re-?index/i })).toBeDisabled();
  });

  it('Re-index ready → calls mutate + shows success toast', async () => {
    mockReindexMutate.mockImplementation(
      (_: void, options?: { onSuccess?: () => void; onError?: (e: Error) => void }) => {
        options?.onSuccess?.();
      }
    );
    render(<KbDocActions {...BASE_PROPS} processingStatus="ready" />);
    fireEvent.click(screen.getByRole('button', { name: /re-?index/i }));
    expect(mockReindexMutate).toHaveBeenCalled();
    await waitFor(() => expect(mockToastSuccess).toHaveBeenCalledWith('Re-index avviato'));
  });

  it('Re-index shows error toast on failure', async () => {
    mockReindexMutate.mockImplementation(
      (_: void, options?: { onSuccess?: () => void; onError?: (e: Error) => void }) => {
        options?.onError?.(new Error('network error'));
      }
    );
    render(<KbDocActions {...BASE_PROPS} processingStatus="ready" />);
    fireEvent.click(screen.getByRole('button', { name: /re-?index/i }));
    await waitFor(() => expect(mockToastError).toHaveBeenCalled());
  });

  // ── Download ──────────────────────────────────────────────────────────────────

  it('renders the Download anchor with correct href', () => {
    render(<KbDocActions {...BASE_PROPS} />);
    const link = screen.getByRole('link', { name: /download/i });
    expect(link).toBeInTheDocument();
    expect(link).toHaveAttribute('href', 'http://api/download/doc-123');
    expect(link).toHaveAttribute('download');
  });

  // ── Delete ────────────────────────────────────────────────────────────────────

  it('renders the Delete button', () => {
    render(<KbDocActions {...BASE_PROPS} />);
    expect(screen.getByRole('button', { name: /elimina/i })).toBeInTheDocument();
  });

  it('Delete button opens the confirmation dialog', async () => {
    render(<KbDocActions {...BASE_PROPS} />);
    fireEvent.click(screen.getByRole('button', { name: /elimina/i }));
    // Dialog title should appear
    await waitFor(() =>
      expect(screen.getByRole('heading', { name: /elimina documento/i })).toBeInTheDocument()
    );
  });

  it('Delete opens typed-confirm dialog with fileName as confirmPhrase', async () => {
    render(<KbDocActions {...BASE_PROPS} />);
    fireEvent.click(screen.getByRole('button', { name: /elimina/i }));
    await waitFor(() =>
      expect(screen.getByRole('heading', { name: /elimina documento/i })).toBeInTheDocument()
    );
    // The confirmPhrase (fileName) should appear in the dialog
    expect(screen.getByText(BASE_PROPS.fileName)).toBeInTheDocument();
  });

  it('Delete calls mutate(docId) after typing fileName and confirming', async () => {
    mockDeleteMutate.mockImplementation(
      (_: string, options?: { onSuccess?: () => void; onError?: (e: Error) => void }) => {
        options?.onSuccess?.();
      }
    );
    render(<KbDocActions {...BASE_PROPS} />);
    // Open dialog
    fireEvent.click(screen.getByRole('button', { name: /elimina/i }));
    await waitFor(() =>
      expect(screen.getByRole('heading', { name: /elimina documento/i })).toBeInTheDocument()
    );
    // Type the confirmPhrase
    const input = screen.getByRole('textbox');
    fireEvent.change(input, { target: { value: BASE_PROPS.fileName } });
    // Confirm
    const confirmBtn = screen.getByRole('button', { name: /conferma azione critica/i });
    fireEvent.click(confirmBtn);
    await waitFor(() => {
      expect(mockDeleteMutate).toHaveBeenCalledWith(
        BASE_PROPS.docId,
        expect.objectContaining({ onSuccess: expect.any(Function) })
      );
    });
  });

  it('Delete shows success toast after confirmed deletion', async () => {
    mockDeleteMutate.mockImplementation(
      (_: string, options?: { onSuccess?: () => void; onError?: (e: Error) => void }) => {
        options?.onSuccess?.();
      }
    );
    render(<KbDocActions {...BASE_PROPS} />);
    fireEvent.click(screen.getByRole('button', { name: /elimina/i }));
    await waitFor(() =>
      expect(screen.getByRole('heading', { name: /elimina documento/i })).toBeInTheDocument()
    );
    const input = screen.getByRole('textbox');
    fireEvent.change(input, { target: { value: BASE_PROPS.fileName } });
    fireEvent.click(screen.getByRole('button', { name: /conferma azione critica/i }));
    await waitFor(() => expect(mockToastSuccess).toHaveBeenCalled());
  });

  it('Delete lazy-fetches used-by only when dialog is open', async () => {
    // Before opening the dialog, consuming agents should NOT be fetched
    // (enabled: false for deleteOpen=false)
    render(<KbDocActions {...BASE_PROPS} />);
    // Initially the consuming agents hook is called with enabled: false
    expect(mockUseKbDocConsumingAgents).toHaveBeenCalledWith(
      expect.objectContaining({ enabled: false })
    );
    // Open dialog
    fireEvent.click(screen.getByRole('button', { name: /elimina/i }));
    await waitFor(() =>
      expect(screen.getByRole('heading', { name: /elimina documento/i })).toBeInTheDocument()
    );
    // Now the hook should be called with enabled: true
    expect(mockUseKbDocConsumingAgents).toHaveBeenCalledWith(
      expect.objectContaining({ enabled: true })
    );
  });

  it('Delete shows agent-count warning when agents are loaded', async () => {
    // Return 3 agents when delete dialog is open
    mockUseKbDocConsumingAgents.mockImplementation(({ enabled }: { enabled: boolean }) => {
      if (!enabled) return makeConsumingAgentsQuery({ data: [] });
      return makeConsumingAgentsQuery({
        data: [
          { id: 'a1', name: 'Agent Alpha' },
          { id: 'a2', name: 'Agent Beta' },
          { id: 'a3', name: 'Agent Gamma' },
        ],
      });
    });
    render(<KbDocActions {...BASE_PROPS} />);
    fireEvent.click(screen.getByRole('button', { name: /elimina/i }));
    await waitFor(() =>
      expect(screen.getByRole('heading', { name: /elimina documento/i })).toBeInTheDocument()
    );
    // Warning should mention the agent count
    expect(screen.getByText(/3 agent/i)).toBeInTheDocument();
  });

  // ── Export chunks JSON ────────────────────────────────────────────────────────

  it('renders the Export chunks button', () => {
    render(<KbDocActions {...BASE_PROPS} />);
    expect(screen.getByRole('button', { name: /export.*chunk/i })).toBeInTheDocument();
  });

  it('disables Export chunks when processingStatus is not "ready"', () => {
    render(<KbDocActions {...BASE_PROPS} processingStatus="failed" />);
    expect(screen.getByRole('button', { name: /export.*chunk/i })).toBeDisabled();
  });

  it('Export chunks downloads JSON via exportDocChunks + downloadAsFile', async () => {
    const chunks = [{ id: 'c1', chunkIndex: 0, pageNumber: 1, heading: 'Intro', content: 'text' }];
    mockExportDocChunks.mockResolvedValue(chunks);
    render(<KbDocActions {...BASE_PROPS} />);
    fireEvent.click(screen.getByRole('button', { name: /export.*chunk/i }));
    await waitFor(() => expect(mockExportDocChunks).toHaveBeenCalledWith(BASE_PROPS.docId));
    await waitFor(() =>
      expect(mockDownloadAsFile).toHaveBeenCalledWith(
        JSON.stringify(chunks, null, 2),
        `${BASE_PROPS.fileName}-chunks.json`
      )
    );
  });

  it('Export chunks shows error toast on failure', async () => {
    mockExportDocChunks.mockRejectedValue(new Error('export failed'));
    render(<KbDocActions {...BASE_PROPS} />);
    fireEvent.click(screen.getByRole('button', { name: /export.*chunk/i }));
    await waitFor(() => expect(mockToastError).toHaveBeenCalled());
  });

  // ── Used-by link ──────────────────────────────────────────────────────────────

  it('renders the Used-by link pointing to the correct URL', () => {
    render(<KbDocActions {...BASE_PROPS} />);
    const link = screen.getByRole('link', { name: /used.?by|agent/i });
    expect(link).toHaveAttribute(
      'href',
      `/admin/knowledge-base?docId=${BASE_PROPS.docId}&tab=used-by`
    );
  });
});

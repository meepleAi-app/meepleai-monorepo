/**
 * KbChunkSearch unit tests — Issue #1653 F3-FU-4 Task 9.
 *
 * Mocks:
 *  - @/hooks/queries/useKbDocActions (useDocChunkSearch)
 *  - sonner
 */
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// ── sonner mock ────────────────────────────────────────────────────────────────
const mockToastError = vi.fn();
vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: (...args: unknown[]) => mockToastError(...args),
  },
}));

// ── useDocChunkSearch mock ─────────────────────────────────────────────────────
const mockMutateAsync = vi.fn();
const mockUseDocChunkSearch = vi.fn();

vi.mock('@/hooks/queries/useKbDocActions', () => ({
  useDocChunkSearch: (docId: string) => mockUseDocChunkSearch(docId),
}));

// ── Import after mocks ─────────────────────────────────────────────────────────
import { KbChunkSearch } from '../KbChunkSearch';

// ── Fixtures ───────────────────────────────────────────────────────────────────

function makeMutation(overrides: Record<string, unknown> = {}) {
  return {
    mutateAsync: mockMutateAsync,
    isPending: false,
    isError: false,
    data: undefined,
    ...overrides,
  };
}

const hitHigh = {
  chunkIndex: 218,
  pageNumber: 22,
  score: 0.84,
  snippet:
    "Quando più uccelli predatori sono attivati nello stesso turno, l'attivazione segue l'ordine…",
};

const hitMid = {
  chunkIndex: 142,
  pageNumber: 14,
  score: 0.58,
  snippet: 'Power di tipo predator si attiva su trigger when activated…',
};

describe('KbChunkSearch', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockUseDocChunkSearch.mockReturnValue(makeMutation());
  });

  // ── Idle state ─────────────────────────────────────────────────────────────

  it('renders the search input with correct placeholder', () => {
    render(<KbChunkSearch docId="doc-1" chunkCount={412} />);
    const input = screen.getByRole('searchbox');
    expect(input).toBeInTheDocument();
    expect(input).toHaveAttribute('placeholder', expect.stringMatching(/cerca.*chunk/i));
  });

  it('renders the search input enabled when chunkCount > 0', () => {
    render(<KbChunkSearch docId="doc-1" chunkCount={412} />);
    expect(screen.getByRole('searchbox')).not.toBeDisabled();
  });

  it('shows the idle hint when no query has been submitted yet', () => {
    render(<KbChunkSearch docId="doc-1" chunkCount={412} />);
    // Hint text should be visible before any search
    expect(screen.getByTestId('kb-chunk-search-idle')).toBeInTheDocument();
  });

  // ── Disabled state (chunkCount === 0) ──────────────────────────────────────

  it('disables the input when chunkCount is 0', () => {
    render(<KbChunkSearch docId="d" chunkCount={0} />);
    expect(screen.getByRole('searchbox')).toBeDisabled();
  });

  it('shows a hint about no chunks when chunkCount is 0', () => {
    render(<KbChunkSearch docId="d" chunkCount={0} />);
    expect(screen.getByTestId('kb-chunk-search-no-chunks')).toBeInTheDocument();
  });

  // ── Submit + scored results ────────────────────────────────────────────────

  it('submitting a query renders scored result rows sorted desc', async () => {
    // Results returned out of order — component must sort desc
    mockMutateAsync.mockResolvedValue({
      results: [hitMid, hitHigh], // mid first, high second
      errorMessage: null,
    });

    render(<KbChunkSearch docId="doc-1" chunkCount={412} />);

    // Lower threshold to 0.5 so both 0.84 and 0.58 are visible
    const thresholdSelect = screen.getByLabelText(/score/i);
    fireEvent.change(thresholdSelect, { target: { value: '0.5' } });

    const input = screen.getByRole('searchbox');
    await userEvent.type(input, 'predator activation');
    fireEvent.submit(input.closest('form')!);

    await waitFor(() => {
      expect(screen.getByTestId('kb-chunk-search-results')).toBeInTheDocument();
    });

    const rows = screen.getAllByTestId('kb-chunk-search-hit');
    expect(rows).toHaveLength(2);

    // First row should have the higher score
    expect(rows[0]).toHaveTextContent('0.84');
    // Second row should have the lower score
    expect(rows[1]).toHaveTextContent('0.58');
  });

  it('result rows contain chunkIndex, pageNumber, score badge and snippet', async () => {
    mockMutateAsync.mockResolvedValue({ results: [hitHigh], errorMessage: null });

    render(<KbChunkSearch docId="doc-1" chunkCount={412} />);
    const input = screen.getByRole('searchbox');
    await userEvent.type(input, 'predator');
    fireEvent.submit(input.closest('form')!);

    await waitFor(() => screen.getByTestId('kb-chunk-search-hit'));

    const row = screen.getByTestId('kb-chunk-search-hit');
    expect(row).toHaveTextContent('c-0218');
    expect(row).toHaveTextContent('p.22');
    expect(row).toHaveTextContent('0.84');
    expect(row).toHaveTextContent('Quando più uccelli predatori');
  });

  it('renders null pageNumber gracefully (no "p." entry)', async () => {
    mockMutateAsync.mockResolvedValue({
      results: [{ ...hitHigh, pageNumber: null }],
      errorMessage: null,
    });

    render(<KbChunkSearch docId="doc-1" chunkCount={412} />);
    const input = screen.getByRole('searchbox');
    await userEvent.type(input, 'predator');
    fireEvent.submit(input.closest('form')!);

    await waitFor(() => screen.getByTestId('kb-chunk-search-hit'));
    // Should not show "p." when pageNumber is null
    expect(screen.queryByText(/p\.\d/)).not.toBeInTheDocument();
  });

  // ── Threshold filter ───────────────────────────────────────────────────────

  it('threshold filter hides results below the threshold', async () => {
    mockMutateAsync.mockResolvedValue({
      results: [hitHigh, hitMid], // 0.84 and 0.58
      errorMessage: null,
    });

    render(<KbChunkSearch docId="doc-1" chunkCount={412} />);

    // Change threshold to 0.7 (default is 0.7; hitMid 0.58 should be hidden)
    const thresholdSelect = screen.getByLabelText(/score/i);
    fireEvent.change(thresholdSelect, { target: { value: '0.7' } });

    const input = screen.getByRole('searchbox');
    await userEvent.type(input, 'predator');
    fireEvent.submit(input.closest('form')!);

    await waitFor(() => screen.getByTestId('kb-chunk-search-results'));

    // With threshold 0.7, only 0.84 should be visible (0.58 hidden)
    expect(screen.getByText('0.84')).toBeInTheDocument();
    expect(screen.queryByText('0.58')).not.toBeInTheDocument();
  });

  it('lowering threshold shows previously hidden results without refetch', async () => {
    mockMutateAsync.mockResolvedValue({
      results: [hitHigh, hitMid], // 0.84 and 0.58
      errorMessage: null,
    });

    render(<KbChunkSearch docId="doc-1" chunkCount={412} />);

    const input = screen.getByRole('searchbox');
    await userEvent.type(input, 'predator');
    fireEvent.submit(input.closest('form')!);
    await waitFor(() => screen.getByTestId('kb-chunk-search-results'));

    // Initially threshold 0.7 — 0.58 hidden
    expect(screen.queryByText('0.58')).not.toBeInTheDocument();

    // Lower threshold to 0.5 — 0.58 should appear without another mutateAsync call
    const initialCallCount = mockMutateAsync.mock.calls.length;
    const thresholdSelect = screen.getByLabelText(/score/i);
    fireEvent.change(thresholdSelect, { target: { value: '0.5' } });

    await waitFor(() => expect(screen.getByText('0.58')).toBeInTheDocument());
    // No additional API call
    expect(mockMutateAsync.mock.calls.length).toBe(initialCallCount);
  });

  // ── Empty state ────────────────────────────────────────────────────────────

  it('empty results show empty state message', async () => {
    mockMutateAsync.mockResolvedValue({ results: [], errorMessage: null });

    render(<KbChunkSearch docId="doc-1" chunkCount={412} />);
    const input = screen.getByRole('searchbox');
    await userEvent.type(input, 'something obscure');
    fireEvent.submit(input.closest('form')!);

    await waitFor(() => {
      expect(screen.getByTestId('kb-chunk-search-empty')).toBeInTheDocument();
    });
    expect(screen.getByText(/nessun risultato/i)).toBeInTheDocument();
  });

  // ── Error states ───────────────────────────────────────────────────────────

  it('errorMessage triggers an error toast and shows inline error message', async () => {
    mockMutateAsync.mockResolvedValue({
      results: [],
      errorMessage: 'Document not indexed',
    });

    render(<KbChunkSearch docId="doc-1" chunkCount={412} />);
    const input = screen.getByRole('searchbox');
    await userEvent.type(input, 'predator');
    fireEvent.submit(input.closest('form')!);

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalledWith(expect.stringContaining('Document not indexed'));
    });
    expect(screen.getByTestId('kb-chunk-search-error')).toBeInTheDocument();
    expect(screen.getByText(/Document not indexed/)).toBeInTheDocument();
  });

  it('mutation rejection triggers an error toast and shows inline error message', async () => {
    mockMutateAsync.mockRejectedValue(new Error('network error'));

    render(<KbChunkSearch docId="doc-1" chunkCount={412} />);
    const input = screen.getByRole('searchbox');
    await userEvent.type(input, 'predator');
    fireEvent.submit(input.closest('form')!);

    await waitFor(() => {
      expect(mockToastError).toHaveBeenCalled();
    });
    expect(screen.getByTestId('kb-chunk-search-error')).toBeInTheDocument();
  });

  // ── Loading state ──────────────────────────────────────────────────────────

  it('shows a loading indicator while the mutation is pending', async () => {
    // Never resolves — stays pending
    mockMutateAsync.mockReturnValue(new Promise(() => {}));
    mockUseDocChunkSearch.mockReturnValue(makeMutation({ isPending: true }));

    render(<KbChunkSearch docId="doc-1" chunkCount={412} />);
    const input = screen.getByRole('searchbox');
    await userEvent.type(input, 'predator');
    fireEvent.submit(input.closest('form')!);

    expect(screen.getByTestId('kb-chunk-search-loading')).toBeInTheDocument();
  });

  // ── Does not submit empty query ────────────────────────────────────────────

  it('does not call mutateAsync when the query is empty', () => {
    render(<KbChunkSearch docId="doc-1" chunkCount={412} />);
    const input = screen.getByRole('searchbox');
    fireEvent.submit(input.closest('form')!);
    expect(mockMutateAsync).not.toHaveBeenCalled();
  });
});

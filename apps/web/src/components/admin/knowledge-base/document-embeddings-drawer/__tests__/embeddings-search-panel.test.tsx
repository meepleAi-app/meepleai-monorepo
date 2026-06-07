import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi, beforeEach } from 'vitest';

const mutate = vi.fn();
const mockState = {
  mutate,
  isPending: false,
  isError: false,
  isSuccess: false,
  data: null as unknown,
  error: null as unknown,
};

vi.mock('@/hooks/admin/use-search-document-chunks', () => ({
  useSearchDocumentChunks: () => mockState,
}));

import { EmbeddingsSearchPanel } from '../embeddings-search-panel';

describe('EmbeddingsSearchPanel', () => {
  beforeEach(() => {
    mutate.mockReset();
    mockState.isPending = false;
    mockState.isError = false;
    mockState.isSuccess = false;
    mockState.data = null;
    mockState.error = null;
  });

  it('disables submit button when query is empty', () => {
    render(<EmbeddingsSearchPanel docId="abc" />);
    expect(screen.getByRole('button', { name: 'Cerca' })).toBeDisabled();
  });

  it('submits via Cerca button click with current query + topK', () => {
    render(<EmbeddingsSearchPanel docId="abc" />);
    fireEvent.change(screen.getByLabelText(/Query semantica/i), {
      target: { value: 'predator' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Cerca' }));
    expect(mutate).toHaveBeenCalledWith({ query: 'predator', topK: 10 });
  });

  it('shows skeleton while pending', () => {
    mockState.isPending = true;
    render(<EmbeddingsSearchPanel docId="abc" />);
    expect(screen.getByTestId('search-skeleton')).toBeInTheDocument();
  });

  it('renders results count + rows when search succeeds', () => {
    mockState.isSuccess = true;
    mockState.data = {
      results: [
        { chunkIndex: 1, pageNumber: 22, snippet: 'first chunk', score: 0.9 },
        { chunkIndex: 2, pageNumber: 23, snippet: 'second chunk', score: 0.7 },
      ],
      errorMessage: null,
    };
    render(<EmbeddingsSearchPanel docId="abc" />);
    expect(screen.getByText(/2 risultati trovati/)).toBeInTheDocument();
    expect(screen.getByText('first chunk')).toBeInTheDocument();
  });

  it('renders "Nessun chunk corrisponde" empty state on 0 results', () => {
    mockState.isSuccess = true;
    mockState.data = { results: [], errorMessage: null };
    render(<EmbeddingsSearchPanel docId="abc" />);
    expect(screen.getByText(/Nessun chunk corrisponde/i)).toBeInTheDocument();
  });

  it('shows server error from data.errorMessage', () => {
    mockState.isSuccess = true;
    mockState.data = { results: [], errorMessage: 'Document not indexed' };
    render(<EmbeddingsSearchPanel docId="abc" />);
    expect(screen.getByText('Document not indexed')).toBeInTheDocument();
  });
});

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { EmbeddingsResultRow, type ScoredChunkDto } from '../embeddings-result-row';

const FIXTURE: ScoredChunkDto = {
  chunkIndex: 218,
  pageNumber: 22,
  snippet: 'predator activation order',
  score: 0.912,
};

describe('EmbeddingsResultRow', () => {
  it('renders collapsed by default (no VecThumb visible)', () => {
    render(<EmbeddingsResultRow chunk={FIXTURE} />);
    expect(screen.queryByText(/768d · float32/)).not.toBeInTheDocument();
    expect(screen.getByText('p.22')).toBeInTheDocument();
    expect(screen.getByText('#218')).toBeInTheDocument();
    expect(screen.getByText('0.912')).toBeInTheDocument();
  });

  it('expands and shows VecThumb + meta on click', () => {
    render(<EmbeddingsResultRow chunk={FIXTURE} />);
    fireEvent.click(screen.getByRole('button', { name: /espandi|collassa/i }));
    expect(screen.getByText(/768d · float32/)).toBeInTheDocument();
    expect(screen.getByText('0.9120 (cosine)')).toBeInTheDocument();
  });

  it('renders "—" when pageNumber is null', () => {
    const noPage: ScoredChunkDto = { ...FIXTURE, pageNumber: null };
    render(<EmbeddingsResultRow chunk={noPage} />);
    expect(screen.getByText('—')).toBeInTheDocument();
  });
});

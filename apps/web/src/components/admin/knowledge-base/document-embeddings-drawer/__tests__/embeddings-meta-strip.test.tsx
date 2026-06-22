import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import type { DocumentEmbeddingsMetaDto } from '@/lib/api/schemas/admin-kb-embeddings.schemas';

import { EmbeddingsMetaStrip } from '../embeddings-meta-strip';

const FIXTURE: DocumentEmbeddingsMetaDto = {
  docId: 'a3f7c218-4d11-4b9e-9d2a-7e5f1c8a0b6e',
  model: 'bge-base-en-v1.5',
  dimensions: 768,
  totalChunks: 412,
  indexedAt: '2026-05-28T14:22:14Z',
  language: 'en',
};

describe('EmbeddingsMetaStrip', () => {
  it('renders 4 KPI cards on success state', () => {
    render(<EmbeddingsMetaStrip state={{ status: 'success', data: FIXTURE }} />);
    expect(screen.getByText('bge-base-en-v1.5')).toBeInTheDocument();
    expect(screen.getByText('768')).toBeInTheDocument();
    expect(screen.getByText('412')).toBeInTheDocument();
    // "Indexed at" label visible (date format locale-dependent so just check label)
    expect(screen.getByText('Indexed at')).toBeInTheDocument();
  });

  it('renders 4 skeletons while loading', () => {
    render(<EmbeddingsMetaStrip state={{ status: 'loading' }} />);
    expect(screen.getAllByTestId('meta-skeleton')).toHaveLength(4);
  });

  it('renders "Documento non indicizzato" empty state on 404', () => {
    render(<EmbeddingsMetaStrip state={{ status: 'not-indexed' }} />);
    expect(screen.getByText(/Documento non indicizzato/i)).toBeInTheDocument();
  });

  it('renders error banner on error state', () => {
    render(<EmbeddingsMetaStrip state={{ status: 'error', message: 'boom' }} />);
    expect(screen.getByText(/Impossibile caricare metadati embeddings/i)).toBeInTheDocument();
    expect(screen.getByText(/boom/)).toBeInTheDocument();
  });

  it('renders "—" when IndexedAt is null (FIX-5)', () => {
    const nullable: DocumentEmbeddingsMetaDto = { ...FIXTURE, indexedAt: null };
    render(<EmbeddingsMetaStrip state={{ status: 'success', data: nullable }} />);
    expect(screen.getByText('—')).toBeInTheDocument();
  });
});

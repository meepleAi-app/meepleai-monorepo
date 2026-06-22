import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi, beforeEach } from 'vitest';

import type { DocumentEmbeddingsMetaDto } from '@/lib/api/schemas/admin-kb-embeddings.schemas';

const metaState = {
  data: null as DocumentEmbeddingsMetaDto | null,
  isPending: false,
  isError: false,
  error: null as unknown,
};

vi.mock('@/hooks/admin/use-document-embeddings-meta', () => ({
  documentEmbeddingsKeys: {
    all: ['admin', 'kb', 'embeddings'],
    meta: (docId: string) => ['admin', 'kb', 'docs', docId, 'embeddings', 'meta'],
  },
  useDocumentEmbeddingsMeta: () => metaState,
}));

vi.mock('@/hooks/admin/use-search-document-chunks', () => ({
  useSearchDocumentChunks: () => ({
    mutate: vi.fn(),
    isPending: false,
    isError: false,
    isSuccess: false,
    data: null,
    error: null,
  }),
}));

import { DocumentEmbeddingsDrawer } from '../document-embeddings-drawer';

const META_FIXTURE: DocumentEmbeddingsMetaDto = {
  docId: 'abc',
  model: 'bge-base-en-v1.5',
  dimensions: 768,
  totalChunks: 412,
  indexedAt: '2026-05-28T14:22:14Z',
  language: 'en',
};

describe('DocumentEmbeddingsDrawer', () => {
  beforeEach(() => {
    metaState.data = null;
    metaState.isPending = false;
    metaState.isError = false;
    metaState.error = null;
  });

  it('renders nothing when docId is null', () => {
    const { container } = render(
      <DocumentEmbeddingsDrawer open onOpenChange={() => {}} docId={null} docFileName={null} />
    );
    expect(container).toBeEmptyDOMElement();
  });

  it('renders title when open with success meta', () => {
    metaState.data = META_FIXTURE;
    render(
      <DocumentEmbeddingsDrawer
        open
        onOpenChange={() => {}}
        docId="abc"
        docFileName="Wingspan.pdf"
      />
    );
    expect(screen.getByText(/Embeddings · Wingspan\.pdf/)).toBeInTheDocument();
  });

  it('renders 4 KPI meta strip on success', () => {
    metaState.data = META_FIXTURE;
    render(
      <DocumentEmbeddingsDrawer
        open
        onOpenChange={() => {}}
        docId="abc"
        docFileName="Wingspan.pdf"
      />
    );
    expect(screen.getByText('bge-base-en-v1.5')).toBeInTheDocument();
    expect(screen.getByText('768')).toBeInTheDocument();
    expect(screen.getByText('412')).toBeInTheDocument();
  });

  it('renders not-indexed state when meta returns 404', () => {
    metaState.isError = true;
    metaState.error = new Error('404 Not Found: Document not indexed');
    render(
      <DocumentEmbeddingsDrawer
        open
        onOpenChange={() => {}}
        docId="abc"
        docFileName="Wingspan.pdf"
      />
    );
    expect(screen.getByText(/Documento non indicizzato/i)).toBeInTheDocument();
  });

  it('export anchor points to correct chunks/export endpoint', () => {
    metaState.data = META_FIXTURE;
    render(
      <DocumentEmbeddingsDrawer
        open
        onOpenChange={() => {}}
        docId="abc"
        docFileName="Wingspan.pdf"
      />
    );
    const link = screen.getByRole('link', { name: /Export chunks JSON/i });
    expect(link).toHaveAttribute('href', '/api/v1/admin/kb/docs/abc/chunks/export');
  });
});

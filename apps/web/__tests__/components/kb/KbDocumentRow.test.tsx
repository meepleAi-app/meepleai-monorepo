import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { KbDocumentRow } from '@/components/kb/KbDocumentRow';
import type { GameDocument } from '@/lib/api/schemas/game-documents.schemas';

const indexedDoc: GameDocument = {
  id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
  title: 'Rules v2',
  status: 'indexed',
  pageCount: 45,
  createdAt: '2026-01-01T00:00:00Z',
  category: 'Rulebook',
  versionLabel: '2nd Edition',
};

const processingDoc: GameDocument = {
  id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
  title: 'Errata v1.2',
  status: 'processing',
  pageCount: 0,
  createdAt: '2026-01-01T00:00:00Z',
  category: 'Errata',
  versionLabel: 'v1.2',
};

const failedDoc: GameDocument = {
  id: 'cccccccc-cccc-cccc-cccc-cccccccccccc',
  title: 'Bad PDF',
  status: 'failed',
  pageCount: 0,
  createdAt: '2026-01-01T00:00:00Z',
  category: 'Other',
  versionLabel: null,
};

describe('KbDocumentRow', () => {
  it('renders indexed document with title and version', () => {
    render(<KbDocumentRow document={indexedDoc} />);
    expect(screen.getByText('Rules v2')).toBeInTheDocument();
    expect(screen.getByText('2nd Edition')).toBeInTheDocument();
    expect(screen.getByText('45 pagine')).toBeInTheDocument();
  });

  it('renders category label in Italian', () => {
    render(<KbDocumentRow document={indexedDoc} />);
    expect(screen.getByText('Regolamento')).toBeInTheDocument();
  });

  it('renders processing document with spinner testid', () => {
    render(<KbDocumentRow document={processingDoc} />);
    expect(screen.getByText('Errata v1.2')).toBeInTheDocument();
    expect(screen.getByTestId('status-processing')).toBeInTheDocument();
  });

  it('renders status indicator testid for indexed', () => {
    render(<KbDocumentRow document={indexedDoc} />);
    expect(screen.getByTestId('status-indexed')).toBeInTheDocument();
  });

  it('renders failed document with error testid', () => {
    render(<KbDocumentRow document={failedDoc} />);
    expect(screen.getByTestId('status-failed')).toBeInTheDocument();
  });

  it('does not show page count for processing docs', () => {
    render(<KbDocumentRow document={processingDoc} />);
    expect(screen.queryByText(/pagine/)).not.toBeInTheDocument();
  });

  it('does not render version label when null', () => {
    render(<KbDocumentRow document={failedDoc} />);
    // No version badge should appear
    expect(screen.queryByText('2nd Edition')).not.toBeInTheDocument();
  });
});

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import type { SharedGameDocument } from '@/lib/api';

// The real status polls the API; the row only has to hand it the right id.
vi.mock('@/components/admin/shared-games/PdfIndexingStatus', () => ({
  PdfIndexingStatus: ({ pdfId }: { pdfId: string }) => (
    <div data-testid="indexing-status" data-pdf-id={pdfId} />
  ),
}));

import { DocumentItem } from '../client';

const LINK_ID = '189c5d57-2dae-46bd-85e9-c55b2e8a0e83';
const PDF_ID = 'cc216014-19a3-4a59-bdba-d1219c18e422';

const rulebook: SharedGameDocument = {
  id: LINK_ID,
  sharedGameId: '91e1ba0a-098c-582f-e6bc-cefbf9eb60d8',
  pdfDocumentId: PDF_ID,
  documentType: 0,
  version: '1.0',
  isActive: true,
  tags: [],
  createdAt: '2026-11-21T22:10:52.580869Z',
  createdBy: '95b7547b-f790-443b-94f1-c8b572e20a62',
};

describe('DocumentItem (#5103)', () => {
  it("mostra lo stato d'indicizzazione del PDF, non del collegamento fra gioco e PDF", () => {
    render(
      <DocumentItem
        document={rulebook}
        onDelete={vi.fn()}
        isDeleting={false}
        onSetCover={vi.fn()}
      />
    );

    expect(screen.getByTestId('indexing-status')).toHaveAttribute('data-pdf-id', PDF_ID);
  });
});

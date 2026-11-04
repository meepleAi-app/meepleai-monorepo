/**
 * MeepleKbCard — il chip «Reindex» segue lo stato del documento (#4360).
 *
 * `mapDocumentStatus` mandava `Pending` a `'none'`, mentre il mapper condiviso
 * (`lib/kb/processing-status.ts`) lo manda a `processing`: un PDF in coda per l'indicizzazione
 * lasciava a un admin il chip «Reindex» attivo, quando la matrice dell'issue #5001 dice che
 * dev'essere disattivato finché il documento è in elaborazione.
 *
 * Il chip è la superficie osservabile: un chip senza handler è reso `disabled` (come «Chunks»).
 */
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import type { PdfDocumentDto } from '@/lib/api/schemas/pdf.schemas';

import { MeepleKbCard } from '../MeepleKbCard';

function doc(processingState: string): PdfDocumentDto {
  return {
    id: '33333333-3333-3333-3333-333333333333',
    gameId: '99999999-9999-9999-9999-999999999999',
    fileName: 'rulebook.pdf',
    filePath: '/pdfs/rulebook.pdf',
    fileSizeBytes: 2048,
    uploadedAt: '2024-01-01T00:00:00Z',
    processedAt: null,
    pageCount: 12,
    documentType: 'base',
    isPublic: false,
    processingState,
    progressPercentage: 0,
    retryCount: 0,
    maxRetries: 3,
  } as PdfDocumentDto;
}

describe('MeepleKbCard — chip Reindex', () => {
  it.each(['Pending', 'Uploading', 'Extracting', 'Chunking', 'Embedding', 'Indexing'])(
    'è disattivato mentre il documento è %s',
    state => {
      render(<MeepleKbCard document={doc(state)} isAdmin />);

      expect(screen.getByRole('button', { name: 'Reindex' })).toBeDisabled();
    }
  );

  it.each(['Ready', 'Failed'])('è attivo quando il documento è %s', state => {
    render(<MeepleKbCard document={doc(state)} isAdmin />);

    expect(screen.getByRole('button', { name: 'Reindex' })).toBeEnabled();
  });

  it('non è attivo per chi non è admin, qualunque sia lo stato', () => {
    render(<MeepleKbCard document={doc('Ready')} />);

    expect(screen.getByRole('button', { name: 'Reindex' })).toBeDisabled();
  });
});

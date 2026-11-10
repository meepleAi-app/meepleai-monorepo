/**
 * pdfClient.uploadPdf: the PDF is already stored (#4893).
 *
 * `POST /api/v1/ingest/pdf` answers 200 with `{ existingKbFound, existingKb, message }` and no `documentId`
 * when the same content is already stored for the game. `uploadPdf` copied `data.documentId` into its result,
 * so its callers (library upload sheet/modal, private game hub, KB step, admin KB upload zone, PDF version
 * manager) got `documentId: undefined` typed as `string`. The bodies are the endpoint's
 * (`@/__tests__/fixtures/pdf-ingest-responses`).
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import {
  EXISTING_PDF_DOCUMENT_ID,
  INGEST_EXISTING_SHARED_BODY,
  INGEST_EXISTING_USER_BODY,
  INGEST_UPLOADED_BODY,
  NEW_DOCUMENT_ID,
} from '@/__tests__/fixtures/pdf-ingest-responses';
import { FakeUploadXhr } from '@/__tests__/utils/fake-upload-xhr';

import { createPdfClient } from '../pdfClient';

vi.mock('../../core/httpClient', async () => ({
  ...(await vi.importActual('../../core/httpClient')),
  getApiBase: vi.fn(() => 'http://localhost:8080'),
}));

const file = new File(['%PDF-1.7'], 'catan_en_rulebook.pdf', { type: 'application/pdf' });

function client() {
  return createPdfClient({ httpClient: {} as never });
}

beforeEach(() => {
  FakeUploadXhr.reset();
  vi.stubGlobal('XMLHttpRequest', FakeUploadXhr);
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('pdfClient.uploadPdf: the answer is an existing KB (#4893)', () => {
  it('returns the EXISTING document as the documentId, and says it was already there', async () => {
    FakeUploadXhr.respondWith(200, JSON.stringify(INGEST_EXISTING_USER_BODY));

    const result = await client().uploadPdf('game-1', file);

    expect(result.documentId).toBe(EXISTING_PDF_DOCUMENT_ID);
    expect(result.fileName).toBe('catan_en_rulebook.pdf');
    expect(result.existingKb).toMatchObject({
      pdfDocumentId: EXISTING_PDF_DOCUMENT_ID,
      processingState: 'Pending',
    });
  });

  it('reads the catalog variant too', async () => {
    FakeUploadXhr.respondWith(200, JSON.stringify(INGEST_EXISTING_SHARED_BODY));

    const result = await client().uploadPdf('game-1', file);

    expect(result.documentId).toBe(EXISTING_PDF_DOCUMENT_ID);
    expect(result.existingKb?.source).toBe('shared');
  });

  it('rejects a JSON body that is neither an upload nor an existing KB, instead of resolving an empty id', async () => {
    FakeUploadXhr.respondWith(200, JSON.stringify({ existingKbFound: true, existingKb: {} }));

    await expect(client().uploadPdf('game-1', file)).rejects.toThrow(
      'Risposta del server non valida'
    );
  });

  it('keeps resolving an empty id for a 2xx that is not JSON at all (the old behaviour)', async () => {
    FakeUploadXhr.respondWith(200, '');

    await expect(client().uploadPdf('game-1', file)).resolves.toStrictEqual({
      documentId: '',
      fileName: 'catan_en_rulebook.pdf',
    });
  });

  it('control: a PDF stored now keeps its documentId and has no existingKb', async () => {
    FakeUploadXhr.respondWith(200, JSON.stringify(INGEST_UPLOADED_BODY));

    const result = await client().uploadPdf('game-1', file);

    expect(result).toStrictEqual({
      documentId: NEW_DOCUMENT_ID,
      fileName: 'catan_en_rulebook.pdf',
    });
  });
});

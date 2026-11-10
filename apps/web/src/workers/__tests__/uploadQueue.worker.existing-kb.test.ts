/**
 * Upload queue worker: the PDF is already stored (#4893).
 *
 * `POST /api/v1/ingest/pdf` answers 200 with `{ existingKbFound, existingKb, message }` and no `documentId`
 * for a file whose content is already stored for the game. The worker read `data.documentId` from a cast,
 * so the item went to `success` with `pdfId` undefined and the main thread got `UPLOAD_SUCCESS` for no
 * document. The bodies are the endpoint's (`@/__tests__/fixtures/pdf-ingest-responses`).
 *
 * The worker is a module with side effects (it installs `self.onmessage` and posts `WORKER_READY`), so each
 * test loads a fresh copy and talks to it the way `UploadQueueStore` does: a message in, `postMessage` out.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import {
  EXISTING_PDF_DOCUMENT_ID,
  INGEST_EXISTING_USER_BODY,
  INGEST_UPLOADED_BODY,
  NEW_DOCUMENT_ID,
} from '@/__tests__/fixtures/pdf-ingest-responses';

interface Posted {
  type: string;
  payload?: Record<string, unknown>;
}

let posted: Posted[];

async function startWorker(): Promise<(message: unknown) => void> {
  vi.resetModules();
  await import('../uploadQueue.worker');
  const onmessage = (self as unknown as { onmessage: (event: MessageEvent) => void }).onmessage;
  return message => onmessage({ data: message } as MessageEvent);
}

function uploadOneFile(send: (message: unknown) => void) {
  send({
    type: 'ADD_FILES',
    payload: {
      gameId: 'game-1',
      language: 'it',
      files: [
        {
          name: 'catan_en_rulebook.pdf',
          size: 8,
          type: 'application/pdf',
          lastModified: 0,
          arrayBuffer: new TextEncoder().encode('%PDF-1.7').buffer,
        },
      ],
    },
  });
}

async function firstUploadSuccess(): Promise<Posted> {
  await vi.waitFor(() => expect(posted.some(m => m.type === 'UPLOAD_SUCCESS')).toBe(true), {
    timeout: 4000,
  });
  return posted.find(m => m.type === 'UPLOAD_SUCCESS')!;
}

beforeEach(() => {
  posted = [];
  vi.spyOn(window, 'postMessage').mockImplementation(((message: Posted) => {
    posted.push(message);
  }) as typeof window.postMessage);
});

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

describe('uploadQueue worker: the upload answers with an existing KB (#4893)', () => {
  it('reports the EXISTING document as the pdfId of the item', async () => {
    vi.stubGlobal(
      'fetch',
      vi
        .fn()
        .mockResolvedValue({ ok: true, status: 200, json: async () => INGEST_EXISTING_USER_BODY })
    );
    const send = await startWorker();

    uploadOneFile(send);

    const success = await firstUploadSuccess();
    expect(success.payload?.pdfId).toBe(EXISTING_PDF_DOCUMENT_ID);
    const lastState = [...posted].reverse().find(m => m.type === 'STATE_UPDATED')!;
    const items = (lastState.payload as { items: Array<{ status: string; pdfId?: string }> }).items;
    expect(items[0]).toMatchObject({ status: 'success', pdfId: EXISTING_PDF_DOCUMENT_ID });
  });

  it('control: a PDF stored now is reported with its documentId', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({ ok: true, status: 200, json: async () => INGEST_UPLOADED_BODY })
    );
    const send = await startWorker();

    uploadOneFile(send);

    expect((await firstUploadSuccess()).payload?.pdfId).toBe(NEW_DOCUMENT_ID);
  });

  it('fails the item on a body that is neither an upload nor an existing KB', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({ ok: true, status: 200, json: async () => ({}) })
    );
    const send = await startWorker();

    uploadOneFile(send);

    await vi.waitFor(() => expect(posted.some(m => m.type === 'UPLOAD_FAILED')).toBe(true), {
      timeout: 4000,
    });
    expect(posted.some(m => m.type === 'UPLOAD_SUCCESS')).toBe(false);
  });
});

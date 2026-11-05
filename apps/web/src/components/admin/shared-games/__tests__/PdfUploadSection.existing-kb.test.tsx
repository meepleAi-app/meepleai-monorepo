/**
 * PdfUploadSection: a PDF that is already there (#4893).
 *
 * `POST /api/v1/ingest/pdf` answers 200 with `{ existingKbFound, existingKb, message }` and NO `documentId`
 * when the same content is already stored for the game (it deduplicates by content hash). The section used to
 * read `result.documentId` anyway: `linkPdfToGame` sent a body without `pdfDocumentId` and the backend answered
 * 422 `PdfDocumentId is required`, while the admin was told the PDF had been uploaded.
 *
 * The bodies are the endpoint's (see `@/__tests__/fixtures/pdf-ingest-responses`), not written from memory.
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import {
  EXISTING_PDF_DOCUMENT_ID,
  INGEST_EXISTING_SHARED_BODY,
  INGEST_EXISTING_USER_BODY,
  INGEST_UPLOADED_BODY,
  NEW_DOCUMENT_ID,
} from '@/__tests__/fixtures/pdf-ingest-responses';
import { FakeUploadXhr } from '@/__tests__/utils/fake-upload-xhr';
import { resetPdfUploadLimit } from '@/__tests__/utils/pdf-upload-limits-mock';

import { PdfUploadSection } from '../PdfUploadSection';

vi.mock(
  '@/hooks/queries/usePdfUploadLimits',
  () => import('@/__tests__/utils/pdf-upload-limits-mock')
);

const { toastSuccess, toastError, toastInfo } = vi.hoisted(() => ({
  toastSuccess: vi.fn(),
  toastError: vi.fn(),
  toastInfo: vi.fn(),
}));
vi.mock('sonner', () => ({
  toast: { success: toastSuccess, error: toastError, info: toastInfo, warning: vi.fn() },
}));

vi.mock('@/components/shared-games/CoverPagePicker', () => ({
  CoverPagePicker: () => <div data-testid="cover-page-picker" />,
}));

const GAME_ID = 'game-123';

// ----- the rest of the API (fetch) -----

interface ApiState {
  /** What `GET /admin/shared-games/{id}/documents` answers: the PDFs already linked to the game. */
  linkedPdfDocumentIds: string[];
}

let api: ApiState;
const fetchMock = vi.fn();

function jsonResponse(status: number, body: unknown) {
  return { ok: status >= 200 && status < 300, status, json: async () => body };
}

function linkedDocument(pdfDocumentId: string) {
  return {
    id: '7a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d',
    sharedGameId: GAME_ID,
    pdfDocumentId,
    documentType: 0,
    version: '1.0',
    isActive: true,
    tags: [],
    createdAt: '2026-11-04T10:00:00.000Z',
    createdBy: '9d8c7b6a-5f4e-4d3c-b2a1-0f9e8d7c6b5a',
  };
}

fetchMock.mockImplementation(async (input: RequestInfo | URL, init?: RequestInit) => {
  const url = String(input);
  const method = init?.method ?? 'GET';
  if (url.endsWith(`/api/v1/admin/shared-games/${GAME_ID}/documents`)) {
    if (method === 'POST') return jsonResponse(201, '2f4e6a8c-1b3d-4f5a-9c7e-0a2b4c6d8e1f');
    return jsonResponse(200, api.linkedPdfDocumentIds.map(linkedDocument));
  }
  if (url.includes(`/api/v1/games/${GAME_ID}/pdfs`)) return jsonResponse(200, []);
  return jsonResponse(404, {});
});

function linkRequests(): Array<Record<string, unknown>> {
  return fetchMock.mock.calls
    .filter(
      ([url, init]) =>
        String(url).endsWith(`/api/v1/admin/shared-games/${GAME_ID}/documents`) &&
        (init as RequestInit | undefined)?.method === 'POST'
    )
    .map(([, init]) => JSON.parse(String((init as RequestInit).body)));
}

// ----- rendering -----

function renderSection(props: Partial<React.ComponentProps<typeof PdfUploadSection>> = {}) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 0 } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <PdfUploadSection gameId={GAME_ID} {...props} />
    </QueryClientProvider>
  );
}

async function uploadRulebook(user: ReturnType<typeof userEvent.setup>) {
  const input = document.getElementById('admin-pdf-input') as HTMLInputElement;
  await user.upload(
    input,
    new File(['%PDF-1.7'], 'catan_en_rulebook.pdf', { type: 'application/pdf' })
  );
  await user.click(await screen.findByText('Carica PDF'));
}

beforeEach(() => {
  vi.clearAllMocks();
  resetPdfUploadLimit();
  api = { linkedPdfDocumentIds: [] };
  FakeUploadXhr.reset();
  FakeUploadXhr.respondWith(200, JSON.stringify(INGEST_UPLOADED_BODY));
  vi.stubGlobal('XMLHttpRequest', FakeUploadXhr);
  vi.stubGlobal('fetch', fetchMock);
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('PdfUploadSection: the upload answers with an existing KB (#4893)', () => {
  beforeEach(() => {
    FakeUploadXhr.respondWith(200, JSON.stringify(INGEST_EXISTING_USER_BODY));
  });

  it('links the EXISTING document to the game, with existingKb.pdfDocumentId', async () => {
    const user = userEvent.setup();
    renderSection();

    await uploadRulebook(user);

    await waitFor(() => expect(linkRequests()).toHaveLength(1));
    expect(linkRequests()[0]).toMatchObject({
      pdfDocumentId: EXISTING_PDF_DOCUMENT_ID,
      documentType: 0,
      setAsActive: true,
    });
    await waitFor(() => expect(toastSuccess).toHaveBeenCalledWith('PDF collegato al gioco'));
    expect(toastError).not.toHaveBeenCalled();
  });

  it('says the file was already there, with its state, and does not announce an upload', async () => {
    const user = userEvent.setup();
    renderSection();

    await uploadRulebook(user);

    const notice = await screen.findByTestId('pdf-existing-notice');
    expect(notice).toHaveTextContent('Questo PDF era già presente');
    expect(notice).toHaveTextContent(INGEST_EXISTING_USER_BODY.existingKb.fileName);
    expect(notice).toHaveTextContent(INGEST_EXISTING_USER_BODY.existingKb.processingState);
    expect(toastSuccess).not.toHaveBeenCalledWith('PDF caricato con successo!');
  });

  it('hands the existing document to the parent as the PDF of the game', async () => {
    const user = userEvent.setup();
    const onPdfUploaded = vi.fn();
    renderSection({ onPdfUploaded });

    await uploadRulebook(user);

    await waitFor(() => expect(onPdfUploaded).toHaveBeenCalledTimes(1));
    expect(onPdfUploaded).toHaveBeenCalledWith(
      expect.objectContaining({ id: EXISTING_PDF_DOCUMENT_ID })
    );
  });

  it('does not link it again when the game already has it: no request that would answer 400', async () => {
    api.linkedPdfDocumentIds = [EXISTING_PDF_DOCUMENT_ID];
    const user = userEvent.setup();
    renderSection();

    await uploadRulebook(user);

    const notice = await screen.findByTestId('pdf-existing-notice');
    await waitFor(() => expect(notice).toHaveTextContent('già collegato'));
    expect(linkRequests()).toHaveLength(0);
    expect(toastError).not.toHaveBeenCalled();
    expect(toastSuccess).not.toHaveBeenCalledWith('PDF collegato al gioco');
  });

  it('links it when the game has OTHER documents but not this one', async () => {
    api.linkedPdfDocumentIds = ['e1d2c3b4-a596-4877-8695-a4b3c2d1e0f9'];
    const user = userEvent.setup();
    renderSection();

    await uploadRulebook(user);

    await waitFor(() => expect(linkRequests()).toHaveLength(1));
    expect(linkRequests()[0]).toMatchObject({ pdfDocumentId: EXISTING_PDF_DOCUMENT_ID });
  });

  it('reads the catalog variant too (another user uploaded it, already indexed)', async () => {
    FakeUploadXhr.respondWith(200, JSON.stringify(INGEST_EXISTING_SHARED_BODY));
    const user = userEvent.setup();
    renderSection();

    await uploadRulebook(user);

    await waitFor(() => expect(linkRequests()).toHaveLength(1));
    expect(linkRequests()[0]).toMatchObject({ pdfDocumentId: EXISTING_PDF_DOCUMENT_ID });
    expect(await screen.findByTestId('pdf-existing-notice')).toHaveTextContent('Ready');
  });
});

describe('PdfUploadSection: the upload stored a new PDF (control)', () => {
  it('still links the new document and announces the upload', async () => {
    const user = userEvent.setup();
    renderSection();

    await uploadRulebook(user);

    await waitFor(() => expect(linkRequests()).toHaveLength(1));
    expect(linkRequests()[0]).toMatchObject({ pdfDocumentId: NEW_DOCUMENT_ID });
    expect(toastSuccess).toHaveBeenCalledWith('PDF caricato con successo!');
    expect(screen.queryByTestId('pdf-existing-notice')).not.toBeInTheDocument();
  });

  it('answers an invalid body with the error the section already showed', async () => {
    FakeUploadXhr.respondWith(200, JSON.stringify({}));
    const user = userEvent.setup();
    renderSection();

    await uploadRulebook(user);

    await waitFor(() => expect(toastError).toHaveBeenCalled());
    expect(linkRequests()).toHaveLength(0);
  });
});

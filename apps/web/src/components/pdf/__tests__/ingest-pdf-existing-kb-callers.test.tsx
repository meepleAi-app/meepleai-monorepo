/**
 * #4893: the upload zones that post to `POST /api/v1/ingest/pdf` meet the answer for a PDF that is
 * already stored — `{ existingKbFound, existingKb, message }`, no `documentId`.
 *
 * Each zone hands the id of the PDF on to its flow (wizard step, store, parent callback). With the old
 * cast (`as { documentId: string }`) that id was `undefined` for the existing body: the flow went on with
 * no document. The right id is `existingKb.pdfDocumentId`. The bodies are the endpoint's, from
 * `@/__tests__/fixtures/pdf-ingest-responses`.
 *
 * `PdfUploadSection` has its own file (`admin/shared-games/__tests__/PdfUploadSection.existing-kb.test.tsx`).
 */

import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import {
  EXISTING_PDF_DOCUMENT_ID,
  INGEST_EXISTING_USER_BODY,
  INGEST_UPLOADED_BODY,
  NEW_DOCUMENT_ID,
} from '@/__tests__/fixtures/pdf-ingest-responses';
import { FakeUploadXhr } from '@/__tests__/utils/fake-upload-xhr';
import { resetPdfUploadLimit } from '@/__tests__/utils/pdf-upload-limits-mock';
import { PdfUploadStep } from '@/app/(authenticated)/admin/wizard/steps/PdfUploadStep';
import { UploadPrivatePDF } from '@/components/collection/wizard/steps/UploadPrivatePDF';
import { UploadRulesStep } from '@/components/game-night/steps/UploadRulesStep';

import { PdfUploadForm } from '../PdfUploadForm';

vi.mock(
  '@/hooks/queries/usePdfUploadLimits',
  () => import('@/__tests__/utils/pdf-upload-limits-mock')
);

const { toastSuccess, toastInfo, toastError } = vi.hoisted(() => ({
  toastSuccess: vi.fn(),
  toastInfo: vi.fn(),
  toastError: vi.fn(),
}));
vi.mock('@/components/layout', () => ({
  toast: { error: toastError, success: toastSuccess, warning: vi.fn(), info: toastInfo },
}));
vi.mock('@/components/layout/Toast', () => ({
  toast: { error: toastError, success: toastSuccess, warning: vi.fn(), info: toastInfo },
}));

const { setUploadedPdf, goNext } = vi.hoisted(() => ({ setUploadedPdf: vi.fn(), goNext: vi.fn() }));
vi.mock('@/hooks/useAddGameWizard', () => ({
  useAddGameWizard: () => ({ setUploadedPdf, goNext, goBack: vi.fn() }),
}));

vi.mock('@/components/pdf/CopyrightDisclaimerModal', () => ({
  CopyrightDisclaimerModal: ({ open, onAccept }: { open: boolean; onAccept: () => void }) =>
    open ? (
      <button type="button" onClick={onAccept}>
        accept-disclaimer
      </button>
    ) : null,
}));

vi.mock('@/components/library/PdfProcessingStatus', () => ({
  PdfProcessingStatus: ({ onContinue }: { onContinue: () => void }) => (
    <button type="button" onClick={onContinue}>
      continue-after-processing
    </button>
  ),
}));

vi.mock('@/components/pdf/PdfPreview', () => ({ PdfPreview: () => <div /> }));

vi.mock('@/hooks/useChunkedUpload', () => ({
  useChunkedUpload: () => ({
    progress: {
      status: 'idle',
      currentChunk: 0,
      totalChunks: 0,
      progressPercentage: 0,
      uploadedBytes: 0,
      totalBytes: 0,
    },
    uploadChunked: vi.fn(),
    shouldUseChunkedUpload: () => false,
    isUploading: false,
    reset: vi.fn(),
  }),
}));

const FILE_NAME = 'catan_en_rulebook.pdf';

function rulebook(): File {
  return new File(['%PDF-1.7'], FILE_NAME, { type: 'application/pdf' });
}

function choose(input: HTMLElement) {
  fireEvent.change(input, { target: { files: [rulebook()] } });
}

function answerWith(body: unknown) {
  FakeUploadXhr.respondWith(200, JSON.stringify(body));
}

beforeEach(() => {
  vi.clearAllMocks();
  resetPdfUploadLimit();
  FakeUploadXhr.reset();
  vi.stubGlobal('XMLHttpRequest', FakeUploadXhr);
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('private game wizard step (PdfUploadStep): the PDF is already there', () => {
  it('goes on with the EXISTING document, and does not announce an upload', async () => {
    answerWith(INGEST_EXISTING_USER_BODY);
    const onComplete = vi.fn();
    const { container } = render(<PdfUploadStep onComplete={onComplete} gameId="game-1" />);

    choose(container.querySelector('#pdf-file-input')!);
    fireEvent.click(screen.getByRole('button', { name: /Carica PDF/ }));

    await waitFor(() => expect(onComplete).toHaveBeenCalledTimes(1));
    expect(onComplete).toHaveBeenCalledWith(EXISTING_PDF_DOCUMENT_ID, FILE_NAME, true);
    expect(toastSuccess).not.toHaveBeenCalledWith('PDF caricato con successo!');
    expect(toastInfo).toHaveBeenCalledWith(expect.stringContaining('già presente'));
  });

  it('control: a PDF stored now still goes on with its documentId', async () => {
    answerWith(INGEST_UPLOADED_BODY);
    const onComplete = vi.fn();
    const { container } = render(<PdfUploadStep onComplete={onComplete} gameId="game-1" />);

    choose(container.querySelector('#pdf-file-input')!);
    fireEvent.click(screen.getByRole('button', { name: /Carica PDF/ }));

    await waitFor(() => expect(onComplete).toHaveBeenCalledWith(NEW_DOCUMENT_ID, FILE_NAME, true));
    expect(toastSuccess).toHaveBeenCalledWith('PDF caricato con successo!');
  });
});

describe('collection wizard (UploadPrivatePDF): the PDF is already there', () => {
  it('stores the EXISTING document in the wizard and moves on', async () => {
    answerWith(INGEST_EXISTING_USER_BODY);
    const { container } = render(<UploadPrivatePDF />);

    choose(container.querySelector('#pdf-file-input')!);
    fireEvent.click(screen.getByRole('button', { name: /Upload PDF/ }));

    await waitFor(() => expect(setUploadedPdf).toHaveBeenCalledTimes(1));
    expect(setUploadedPdf).toHaveBeenCalledWith(EXISTING_PDF_DOCUMENT_ID, FILE_NAME);
    expect(goNext).toHaveBeenCalledTimes(1);
    expect(toastSuccess).not.toHaveBeenCalledWith('PDF uploaded successfully!');
    expect(toastInfo).toHaveBeenCalledWith(expect.stringContaining('already uploaded'));
  });

  it('control: a PDF stored now is stored with its documentId', async () => {
    answerWith(INGEST_UPLOADED_BODY);
    const { container } = render(<UploadPrivatePDF />);

    choose(container.querySelector('#pdf-file-input')!);
    fireEvent.click(screen.getByRole('button', { name: /Upload PDF/ }));

    await waitFor(() => expect(setUploadedPdf).toHaveBeenCalledWith(NEW_DOCUMENT_ID, FILE_NAME));
  });
});

describe('game night rules step (UploadRulesStep): the PDF is already there', () => {
  async function uploadThroughTheStep(onComplete: (pdfId?: string) => void) {
    render(
      <UploadRulesStep gameId="game-1" gameTitle="Catan" onComplete={onComplete} onSkip={vi.fn()} />
    );
    fireEvent.click(screen.getByTestId('upload-rules-button'));
    fireEvent.click(screen.getByText('accept-disclaimer'));
    choose(screen.getByTestId('pdf-file-input'));
    return screen.findByText('continue-after-processing');
  }

  it('completes with the EXISTING document id, not with nothing', async () => {
    answerWith(INGEST_EXISTING_USER_BODY);
    const onComplete = vi.fn();

    fireEvent.click(await uploadThroughTheStep(onComplete));

    expect(onComplete).toHaveBeenCalledWith(EXISTING_PDF_DOCUMENT_ID);
  });

  it('control: a PDF stored now completes with its documentId', async () => {
    answerWith(INGEST_UPLOADED_BODY);
    const onComplete = vi.fn();

    fireEvent.click(await uploadThroughTheStep(onComplete));

    expect(onComplete).toHaveBeenCalledWith(NEW_DOCUMENT_ID);
  });

  it('refuses a body that is neither of the two, as the step did for a body it could not read', async () => {
    answerWith({});
    render(
      <UploadRulesStep gameId="game-1" gameTitle="Catan" onComplete={vi.fn()} onSkip={vi.fn()} />
    );
    fireEvent.click(screen.getByTestId('upload-rules-button'));
    fireEvent.click(screen.getByText('accept-disclaimer'));

    choose(screen.getByTestId('pdf-file-input'));

    expect(await screen.findByTestId('upload-error')).toHaveTextContent(
      'Risposta server non valida'
    );
  });
});

describe('PDF upload form (PdfUploadForm): the PDF is already there', () => {
  const fetchMock = vi.fn();

  beforeEach(() => {
    vi.stubGlobal('fetch', fetchMock);
  });

  function respondWith(body: unknown) {
    fetchMock.mockResolvedValue({ ok: true, status: 200, json: async () => body });
  }

  async function submitRulebook(onUploadSuccess: (documentId: string) => void) {
    const { container } = render(
      <PdfUploadForm
        gameId="game-1"
        gameName="Catan"
        onUploadSuccess={onUploadSuccess}
        onUploadError={vi.fn()}
      />
    );
    choose(container.querySelector('#pdf-file')!);
    const submit = screen.getByRole('button', { name: 'Upload PDF' });
    await waitFor(() => expect(submit).toBeEnabled());
    fireEvent.click(submit);
  }

  it('reports the EXISTING document as the PDF of the game', async () => {
    respondWith(INGEST_EXISTING_USER_BODY);
    const onUploadSuccess = vi.fn();

    await submitRulebook(onUploadSuccess);

    await waitFor(() => expect(onUploadSuccess).toHaveBeenCalledTimes(1));
    expect(onUploadSuccess).toHaveBeenCalledWith(EXISTING_PDF_DOCUMENT_ID);
  });

  it('control: a PDF stored now is reported with its documentId', async () => {
    respondWith(INGEST_UPLOADED_BODY);
    const onUploadSuccess = vi.fn();

    await submitRulebook(onUploadSuccess);

    await waitFor(() => expect(onUploadSuccess).toHaveBeenCalledWith(NEW_DOCUMENT_ID));
  });
});

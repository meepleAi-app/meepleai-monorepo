/**
 * #4442: il completamento del caricamento a pezzi accoda già il documento nel backend, nella stessa
 * transazione del record. Il pannello non deve accodarlo di nuovo: riceverebbe un 409 «già in coda» e
 * mostrerebbe «PDF caricato ma non accodato» per un upload riuscito. Il ramo normale accoda ancora
 * finché #4443 non sposta anche lui sulla coda.
 */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { UploadZone } from '../upload-zone';

const GAME_ID = 'game-4442';
const CHUNKED_SIZE = 11 * 1024 * 1024; // sopra la soglia di 10 MB del caricamento a pezzi

const pdfApi = {
  initChunkedUpload: vi.fn(),
  uploadChunk: vi.fn(),
  completeChunkedUpload: vi.fn(),
  uploadPdf: vi.fn(),
};

vi.mock('@/lib/api/context', () => ({
  useApiClient: () => ({ pdf: pdfApi }),
}));

vi.mock('@/lib/domain-hooks/useGameSearch', () => ({
  useGameSearch: () => ({ data: [], isLoading: false }),
}));

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

vi.mock('@/app/admin/(dashboard)/knowledge-base/queue/lib/queue-api', () => ({
  enqueuePdf: vi.fn(),
  PRIORITY_NORMAL: 10,
  PRIORITY_URGENT: 30,
}));

import { enqueuePdf } from '@/app/admin/(dashboard)/knowledge-base/queue/lib/queue-api';

function renderZone() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <UploadZone initialGameId={GAME_ID} />
    </QueryClientProvider>
  );
}

function pdfFile(name: string, size: number): File {
  const file = new File(['%PDF-1.4'], name, { type: 'application/pdf' });
  Object.defineProperty(file, 'size', { value: size });
  return file;
}

async function chooseFile(container: HTMLElement, file: File) {
  // Il gioco si preseleziona da initialGameId (fetch del dettaglio): senza gioco l'upload non parte.
  await waitFor(() => expect(container.textContent).toContain('Caricamento per: Catan'));
  const input = container.querySelector('input[type="file"]') as HTMLInputElement;
  await waitFor(() => expect(input).not.toBeNull());
  fireEvent.change(input, { target: { files: [file] } });
}

describe('UploadZone', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ id: GAME_ID, title: 'Catan' }),
    }) as unknown as typeof fetch;
    pdfApi.initChunkedUpload.mockResolvedValue({
      sessionId: 'session-1',
      totalChunks: 1,
      chunkSizeBytes: CHUNKED_SIZE,
    });
    pdfApi.uploadChunk.mockResolvedValue(undefined);
    pdfApi.completeChunkedUpload.mockResolvedValue({ success: true, documentId: 'doc-chunked' });
    pdfApi.uploadPdf.mockResolvedValue({ documentId: 'doc-single' });
    vi.mocked(enqueuePdf).mockResolvedValue({ jobId: 'job-1' });
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('caricamento a pezzi: non riaccoda il documento, lo ha già accodato il backend', async () => {
    const { container } = renderZone();

    await chooseFile(container, pdfFile('big.pdf', CHUNKED_SIZE));

    await waitFor(() => expect(pdfApi.completeChunkedUpload).toHaveBeenCalledWith('session-1'));
    // Lascia finire il ramo dopo il completamento.
    await waitFor(() => expect(container.textContent).toContain('big.pdf'));
    expect(enqueuePdf).not.toHaveBeenCalled();
  });

  it('caricamento normale: accoda ancora il documento (fino a #4443)', async () => {
    const { container } = renderZone();

    await chooseFile(container, pdfFile('small.pdf', 1024));

    await waitFor(() => expect(enqueuePdf).toHaveBeenCalledWith('doc-single', 10));
    expect(pdfApi.completeChunkedUpload).not.toHaveBeenCalled();
  });
});

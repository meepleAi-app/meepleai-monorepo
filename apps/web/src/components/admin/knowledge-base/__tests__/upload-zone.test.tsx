/**
 * #4442 / #4443: il backend accoda da sé l'upload di un admin, in entrambi i rami (normale e a pezzi),
 * con la priorità che il pannello gli passa. Il pannello non deve accodarlo di nuovo: la coda
 * diventerebbe un secondo proprietario del documento, o risponderebbe 409 «già in coda».
 */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { resetPdfUploadLimit, setPdfUploadLimit } from '@/__tests__/utils/pdf-upload-limits-mock';

import { UploadZone } from '../upload-zone';

// #4449: the size limit is the backend's, read through usePdfUploadLimits.
vi.mock(
  '@/hooks/queries/usePdfUploadLimits',
  () => import('@/__tests__/utils/pdf-upload-limits-mock')
);

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
    resetPdfUploadLimit();
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

  it('rifiuta un file oltre il limite dato dal backend, prima di caricarlo (#4449)', async () => {
    // 20 MB: nessuna costante del componente vale tanto (era 500 MB).
    setPdfUploadLimit(20 * 1024 * 1024);
    const { container } = renderZone();

    await chooseFile(container, pdfFile('big.pdf', 21 * 1024 * 1024));

    await waitFor(() => expect(container.textContent).toContain('File too large (max 20 MB)'));
    expect(container.textContent).toContain('Max 20 MB per file');
    expect(pdfApi.initChunkedUpload).not.toHaveBeenCalled();
    expect(pdfApi.uploadPdf).not.toHaveBeenCalled();
  });

  it('caricamento a pezzi: non riaccoda il documento, lo ha già accodato il backend', async () => {
    const { container } = renderZone();

    await chooseFile(container, pdfFile('big.pdf', CHUNKED_SIZE));

    await waitFor(() => expect(pdfApi.completeChunkedUpload).toHaveBeenCalled());
    // Lascia finire il ramo dopo il completamento.
    await waitFor(() => expect(container.textContent).toContain('big.pdf'));
    expect(enqueuePdf).not.toHaveBeenCalled();
  });

  it('caricamento a pezzi: passa la priorità al completamento', async () => {
    const { container } = renderZone();

    await chooseFile(container, pdfFile('big.pdf', CHUNKED_SIZE));

    await waitFor(() =>
      expect(pdfApi.completeChunkedUpload).toHaveBeenCalledWith('session-1', 'normal')
    );
  });

  it("caricamento normale: non accoda, passa la priorità all'upload", async () => {
    const { container } = renderZone();

    await chooseFile(container, pdfFile('small.pdf', 1024));

    await waitFor(() =>
      expect(pdfApi.uploadPdf).toHaveBeenCalledWith(
        GAME_ID,
        expect.any(File),
        expect.any(Function),
        {
          priority: 'normal',
        }
      )
    );
    await waitFor(() => expect(container.textContent).toContain('small.pdf'));
    expect(enqueuePdf).not.toHaveBeenCalled();
    expect(pdfApi.completeChunkedUpload).not.toHaveBeenCalled();
  });

  it('«Urgente» arriva al backend come priorità', async () => {
    const { container } = renderZone();
    await waitFor(() => expect(container.textContent).toContain('Caricamento per: Catan'));
    fireEvent.click(container.querySelector('[data-testid="priority-toggle"]') as HTMLElement);

    await chooseFile(container, pdfFile('urgent.pdf', 1024));

    await waitFor(() =>
      expect(pdfApi.uploadPdf).toHaveBeenCalledWith(
        GAME_ID,
        expect.any(File),
        expect.any(Function),
        {
          priority: 'urgent',
        }
      )
    );
  });
});

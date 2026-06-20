import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { toast } from 'sonner';

import { playRecordsApi } from '@/lib/api/play-records.api';

import { PlayRecordPhotoUploadDialog } from '../PlayRecordPhotoUploadDialog';

// ─── i18n mock ────────────────────────────────────────────────────────────────
// useTranslation in jsdom returns MESSAGES[key] ?? key.
// We define the keys the component renders so getByLabelText / findByText resolve.
const MESSAGES: Record<string, string> = {
  'playRecords.photos.dialogTitle': 'Carica foto',
  'playRecords.photos.dialogDescription': 'JPG, PNG, WebP o HEIC · max 5MB · fino a 10 foto.',
  'playRecords.photos.selectLabel': 'Seleziona foto',
  'playRecords.photos.captionLabel': 'Didascalia (opzionale)',
  'playRecords.photos.captionPlaceholder': 'Es. tabellone finale',
  'playRecords.photos.extractScoreLabel': 'Estrai il punteggio dalla foto (OCR)',
  'playRecords.photos.uploadCta': 'Carica',
  'playRecords.photos.uploading': 'Caricamento…',
  'playRecords.photos.tooLarge': 'File troppo grande, massimo 5MB.',
  'playRecords.photos.tooMany': 'Massimo 10 foto per partita.',
  'playRecords.photos.badFormat': 'Formato non supportato. Usa JPG, PNG, WebP o HEIC.',
  'playRecords.photos.heicFailed': 'Conversione HEIC fallita. Riprova con JPEG o PNG.',
  'playRecords.photos.uploadError': 'Caricamento foto non riuscito.',
  'playRecords.photos.dedupToast': 'Questa foto è già presente.',
  'playRecords.photos.ocrResultTitle': 'Testo rilevato',
};

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({
    t: (key: string) => MESSAGES[key] ?? key,
  }),
}));

// ─── sonner mock ─────────────────────────────────────────────────────────────
vi.mock('sonner', () => ({
  toast: {
    info: vi.fn(),
    success: vi.fn(),
    error: vi.fn(),
  },
}));

// ─── wrapper ──────────────────────────────────────────────────────────────────

function wrap(node: ReactNode) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={client}>{node}</QueryClientProvider>;
}

describe('PlayRecordPhotoUploadDialog', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    vi.clearAllMocks();
  });

  it('rejects files over 5MB', async () => {
    render(wrap(<PlayRecordPhotoUploadDialog recordId="r1" open onClose={() => {}} />));
    const big = new File([new Uint8Array(6 * 1024 * 1024)], 'big.jpg', { type: 'image/jpeg' });
    const input = screen.getByLabelText(/seleziona foto|select photo/i) as HTMLInputElement;
    fireEvent.change(input, { target: { files: [big] } });
    // DialogDescription also contains "max 5MB" — query the role="alert" element specifically
    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent(/massimo 5MB|max 5MB/i);
  });

  it('uploads a valid jpeg with OCR flag', async () => {
    const spy = vi.spyOn(playRecordsApi, 'uploadPhoto').mockResolvedValue({
      photoId: 'p1',
      photoUrl: 'u',
      thumbnailUrl: null,
      ocrText: '42',
      wasDeduplicated: false,
    });
    render(wrap(<PlayRecordPhotoUploadDialog recordId="r1" open onClose={() => {}} />));
    const file = new File(['x'], 'ok.jpg', { type: 'image/jpeg' });
    fireEvent.change(screen.getByLabelText(/seleziona foto|select photo/i), {
      target: { files: [file] },
    });
    fireEvent.click(screen.getByLabelText(/estrai il punteggio|extract score/i));
    fireEvent.click(screen.getByRole('button', { name: /carica|upload/i }));
    await waitFor(() =>
      expect(spy).toHaveBeenCalledWith(
        'r1',
        expect.any(File),
        expect.objectContaining({ extractScoreFromPhoto: true })
      )
    );
  });

  it('>10 files triggers the tooMany error', async () => {
    render(wrap(<PlayRecordPhotoUploadDialog recordId="r1" open onClose={() => {}} />));
    const files = Array.from(
      { length: 11 },
      (_, i) => new File(['x'], `p${i}.jpg`, { type: 'image/jpeg' })
    );
    const input = screen.getByLabelText(/seleziona foto|select photo/i) as HTMLInputElement;
    fireEvent.change(input, { target: { files } });
    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent(/massimo 10|max 10/i);
  });

  it('wasDeduplicated shows an info toast', async () => {
    vi.spyOn(playRecordsApi, 'uploadPhoto').mockResolvedValue({
      photoId: 'p1',
      photoUrl: 'u',
      thumbnailUrl: null,
      ocrText: null,
      wasDeduplicated: true,
    });
    render(wrap(<PlayRecordPhotoUploadDialog recordId="r1" open onClose={() => {}} />));
    const file = new File(['x'], 'dup.jpg', { type: 'image/jpeg' });
    fireEvent.change(screen.getByLabelText(/seleziona foto|select photo/i), {
      target: { files: [file] },
    });
    fireEvent.click(screen.getByRole('button', { name: /carica|upload/i }));
    await waitFor(() => expect(toast.info).toHaveBeenCalled());
  });
});

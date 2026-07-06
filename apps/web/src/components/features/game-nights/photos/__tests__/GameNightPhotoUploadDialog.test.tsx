import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { toast } from 'sonner';

import { api } from '@/lib/api';

import { GameNightPhotoUploadDialog } from '../GameNightPhotoUploadDialog';

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t: (key: string) => key, locale: 'it-IT' }),
}));

vi.mock('sonner', () => ({
  toast: { info: vi.fn(), success: vi.fn(), error: vi.fn() },
}));

function wrap(node: ReactNode) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={client}>{node}</QueryClientProvider>;
}

describe('GameNightPhotoUploadDialog', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    vi.clearAllMocks();
  });

  it('rejects files over 5MB', async () => {
    render(wrap(<GameNightPhotoUploadDialog gameNightId="gn-1" open onClose={() => {}} />));
    const big = new File([new Uint8Array(6 * 1024 * 1024)], 'big.jpg', { type: 'image/jpeg' });
    fireEvent.change(screen.getByLabelText('gameNightDetail.photos.selectLabel'), {
      target: { files: [big] },
    });
    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('gameNightDetail.photos.tooLarge');
  });

  it('rejects an unsupported format', async () => {
    render(wrap(<GameNightPhotoUploadDialog gameNightId="gn-1" open onClose={() => {}} />));
    const gif = new File(['x'], 'anim.gif', { type: 'image/gif' });
    fireEvent.change(screen.getByLabelText('gameNightDetail.photos.selectLabel'), {
      target: { files: [gif] },
    });
    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('gameNightDetail.photos.badFormat');
  });

  it('uploads a valid jpeg with the OCR flag and closes', async () => {
    const spy = vi.spyOn(api.gameNights, 'uploadPhoto').mockResolvedValue({
      photoId: '11111111-1111-1111-1111-111111111111',
      photoUrl: 'u',
      thumbnailUrl: null,
      ocrText: '42',
      wasDeduplicated: false,
    });
    const onClose = vi.fn();
    render(wrap(<GameNightPhotoUploadDialog gameNightId="gn-1" open onClose={onClose} />));

    const file = new File(['x'], 'ok.jpg', { type: 'image/jpeg' });
    fireEvent.change(screen.getByLabelText('gameNightDetail.photos.selectLabel'), {
      target: { files: [file] },
    });
    fireEvent.click(screen.getByLabelText('gameNightDetail.photos.extractScoreLabel'));
    fireEvent.click(screen.getByRole('button', { name: 'gameNightDetail.photos.uploadCta' }));

    await waitFor(() =>
      expect(spy).toHaveBeenCalledWith(
        'gn-1',
        expect.any(File),
        expect.objectContaining({ extractScoreFromPhoto: true })
      )
    );
    await waitFor(() => expect(onClose).toHaveBeenCalled());
  });

  it('shows an info toast when the upload was deduplicated', async () => {
    vi.spyOn(api.gameNights, 'uploadPhoto').mockResolvedValue({
      photoId: '11111111-1111-1111-1111-111111111111',
      photoUrl: 'u',
      thumbnailUrl: null,
      ocrText: null,
      wasDeduplicated: true,
    });
    render(wrap(<GameNightPhotoUploadDialog gameNightId="gn-1" open onClose={() => {}} />));

    const file = new File(['x'], 'dup.jpg', { type: 'image/jpeg' });
    fireEvent.change(screen.getByLabelText('gameNightDetail.photos.selectLabel'), {
      target: { files: [file] },
    });
    fireEvent.click(screen.getByRole('button', { name: 'gameNightDetail.photos.uploadCta' }));

    await waitFor(() => expect(toast.info).toHaveBeenCalled());
  });
});

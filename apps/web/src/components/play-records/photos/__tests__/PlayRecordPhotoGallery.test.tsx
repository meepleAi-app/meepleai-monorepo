import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import type { PlayRecordPhoto } from '@/lib/api/schemas/play-records.schemas';

import { PlayRecordPhotoGallery } from '../PlayRecordPhotoGallery';

const labels = {
  title: 'Foto',
  emptyTitle: 'Nessuna foto',
  emptyDescription: 'Carica una foto',
  photoAltFallback: 'Foto',
  ocrResultTitle: 'Testo',
  close: 'Chiudi',
  prev: 'Prec',
  next: 'Succ',
};

const photos: PlayRecordPhoto[] = [
  {
    id: 'a',
    url: 'http://x/a.webp',
    thumbnailUrl: null,
    ocrText: '42',
    caption: 'board',
    uploadedByUserId: 'u',
    uploadedAt: '2026-06-20T09:00:00Z',
  },
  {
    id: 'b',
    url: 'http://x/b.webp',
    thumbnailUrl: null,
    ocrText: null,
    caption: null,
    uploadedByUserId: 'u',
    uploadedAt: '2026-06-20T10:00:00Z',
  },
];

describe('PlayRecordPhotoGallery', () => {
  it('renders empty state', () => {
    render(<PlayRecordPhotoGallery photos={[]} labels={labels} />);
    expect(screen.getByText('Nessuna foto')).toBeInTheDocument();
  });

  it('opens lightbox on tile click and navigates', () => {
    render(<PlayRecordPhotoGallery photos={photos} labels={labels} />);
    fireEvent.click(screen.getByRole('button', { name: 'board' }));
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(screen.getByText(/42/)).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Succ' }));
    expect(screen.getByAltText('Foto')).toBeInTheDocument();
  });
});

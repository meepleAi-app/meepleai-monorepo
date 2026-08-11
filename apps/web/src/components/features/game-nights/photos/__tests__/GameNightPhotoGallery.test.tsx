import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import type { GameNightPhotoDto } from '@/lib/api/schemas/game-nights.schemas';

import { GameNightPhotoGallery } from '../GameNightPhotoGallery';

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t: (k: string) => k, locale: 'it-IT' }),
}));

const photos: GameNightPhotoDto[] = [
  {
    id: 'a',
    photoUrl: 'http://x/a.webp',
    thumbnailUrl: null,
    caption: 'board',
    uploadedByUserId: 'u1',
    uploadedAt: '2026-06-20T09:00:00Z',
  },
  {
    id: 'b',
    photoUrl: 'http://x/b.webp',
    thumbnailUrl: 'http://x/b-thumb.webp',
    caption: null,
    uploadedByUserId: 'u2',
    uploadedAt: '2026-06-20T10:00:00Z',
  },
];

describe('GameNightPhotoGallery', () => {
  it('renders the empty state', () => {
    render(<GameNightPhotoGallery photos={[]} />);
    const section = screen.getByRole('status');
    expect(section).toBeInTheDocument();
    expect(screen.getByText('gameNightDetail.photos.emptyTitle')).toBeInTheDocument();
  });

  it('renders a tile per photo', () => {
    render(<GameNightPhotoGallery photos={photos} />);
    // Each tile is a button labelled by caption (or the fallback for the captionless one).
    expect(screen.getByRole('button', { name: 'board' })).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: 'gameNightDetail.photos.photoAltFallback' })
    ).toBeInTheDocument();
  });

  it('opens the lightbox on tile click', () => {
    render(<GameNightPhotoGallery photos={photos} />);
    fireEvent.click(screen.getByRole('button', { name: 'board' }));
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(screen.getByAltText('board')).toBeInTheDocument();
  });

  it('shows an Add button that calls onAddPhoto', () => {
    const onAddPhoto = vi.fn();
    render(<GameNightPhotoGallery photos={photos} onAddPhoto={onAddPhoto} />);
    fireEvent.click(screen.getByRole('button', { name: /gameNightDetail\.photos\.addCta/ }));
    expect(onAddPhoto).toHaveBeenCalledOnce();
  });

  it('gates the delete affordance via canDeletePhoto and calls onDeletePhoto', () => {
    const onDeletePhoto = vi.fn();
    render(
      <GameNightPhotoGallery
        photos={photos}
        onDeletePhoto={onDeletePhoto}
        canDeletePhoto={p => p.uploadedByUserId === 'u1'}
      />
    );
    const deleteButtons = screen.getAllByRole('button', {
      name: 'gameNightDetail.photos.deleteCta',
    });
    // Only photo 'a' (uploadedByUserId u1) is deletable.
    expect(deleteButtons).toHaveLength(1);
    fireEvent.click(deleteButtons[0]);
    expect(onDeletePhoto).toHaveBeenCalledWith('a');
  });
});

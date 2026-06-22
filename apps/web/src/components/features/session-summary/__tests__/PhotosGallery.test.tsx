/**
 * PhotosGallery unit tests — Wave D.3 (Issue #756).
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import type { SessionSnapshotDto } from '@/lib/api/clients/sessionSnapshotsClient';

import { PhotosGallery } from '../PhotosGallery';
import type { PhotosGalleryProps } from '../PhotosGallery';

const SNAPSHOTS: SessionSnapshotDto[] = [
  {
    id: 's1',
    sessionId: 'sess1',
    turnNumber: 1,
    caption: 'Tableau T8',
    hasGameState: false,
    createdAt: '2026-04-23T13:00:00Z',
    images: [
      {
        id: 'i1',
        downloadUrl: 'https://example.com/snap1.jpg',
        mediaType: 'image/jpeg',
        width: 800,
        height: 600,
        orderIndex: 0,
      },
    ],
  },
  {
    id: 's2',
    sessionId: 'sess1',
    turnNumber: 5,
    caption: null,
    hasGameState: false,
    createdAt: '2026-04-23T13:30:00Z',
    images: [
      {
        id: 'i2',
        downloadUrl: null,
        mediaType: 'image/jpeg',
        width: 800,
        height: 600,
        orderIndex: 0,
      },
    ],
  },
];

const LABELS: PhotosGalleryProps['labels'] = {
  title: 'Foto sessione',
  emptyTitle: 'Nessuna foto',
  emptyDescription: 'Le foto scattate appariranno qui',
  photoAltFallback: 'Foto sessione',
};

describe('PhotosGallery', () => {
  it('renders data-slot="photos-gallery"', () => {
    render(<PhotosGallery snapshots={SNAPSHOTS} labels={LABELS} />);
    expect(document.querySelector('[data-slot="photos-gallery"]')).not.toBeNull();
  });

  it('renders empty state when snapshots array is empty', () => {
    render(<PhotosGallery snapshots={[]} labels={LABELS} />);
    const root = document.querySelector('[data-slot="photos-gallery"]')!;
    expect(root.getAttribute('data-empty')).toBe('true');
    expect(screen.getByText('Nessuna foto')).toBeTruthy();
    expect(screen.getByText('Le foto scattate appariranno qui')).toBeTruthy();
  });

  it('does NOT have data-empty when snapshots present', () => {
    render(<PhotosGallery snapshots={SNAPSHOTS} labels={LABELS} />);
    const root = document.querySelector('[data-slot="photos-gallery"]')!;
    expect(root.getAttribute('data-empty')).toBeNull();
  });

  it('renders one photo item per snapshot', () => {
    render(<PhotosGallery snapshots={SNAPSHOTS} labels={LABELS} />);
    expect(document.querySelectorAll('[data-slot="photo-item"]').length).toBe(2);
  });

  it('uses lazy loading on images with downloadUrl', () => {
    render(<PhotosGallery snapshots={SNAPSHOTS} labels={LABELS} />);
    const img = document.querySelector('img');
    expect(img).not.toBeNull();
    expect(img!.getAttribute('loading')).toBe('lazy');
  });

  it('uses caption as alt when present', () => {
    render(<PhotosGallery snapshots={SNAPSHOTS} labels={LABELS} />);
    const img = document.querySelector('img');
    expect(img!.getAttribute('alt')).toBe('Tableau T8');
  });

  it('uses photoAltFallback when caption is null', () => {
    const noCaption: SessionSnapshotDto[] = [{ ...SNAPSHOTS[0], caption: null }];
    render(<PhotosGallery snapshots={noCaption} labels={LABELS} />);
    const img = document.querySelector('img');
    expect(img!.getAttribute('alt')).toBe('Foto sessione');
  });

  it('renders placeholder div for snapshot without downloadUrl', () => {
    render(<PhotosGallery snapshots={[SNAPSHOTS[1]]} labels={LABELS} />);
    expect(document.querySelector('img')).toBeNull();
    expect(document.querySelector('[data-slot="photo-item"]')).not.toBeNull();
  });

  it('fires onPhotoClick callback when photo is clicked', () => {
    const onPhotoClick = vi.fn();
    render(<PhotosGallery snapshots={SNAPSHOTS} labels={LABELS} onPhotoClick={onPhotoClick} />);
    const photo = document.querySelector('[data-slot="photo-item"]') as HTMLButtonElement;
    fireEvent.click(photo);
    expect(onPhotoClick).toHaveBeenCalledOnce();
    expect(onPhotoClick).toHaveBeenCalledWith(SNAPSHOTS[0]);
  });

  it('disables photo button when no onPhotoClick handler provided', () => {
    render(<PhotosGallery snapshots={SNAPSHOTS} labels={LABELS} />);
    const photo = document.querySelector('[data-slot="photo-item"]') as HTMLButtonElement;
    expect(photo.disabled).toBe(true);
  });

  it('marks data-snapshot-id per photo for stable selection', () => {
    render(<PhotosGallery snapshots={SNAPSHOTS} labels={LABELS} />);
    const photos = document.querySelectorAll('[data-slot="photo-item"]');
    expect(photos[0].getAttribute('data-snapshot-id')).toBe('s1');
    expect(photos[1].getAttribute('data-snapshot-id')).toBe('s2');
  });

  it('uses 4-col desktop / 2-col mobile responsive grid', () => {
    render(<PhotosGallery snapshots={SNAPSHOTS} labels={LABELS} />);
    const grid = document.querySelector('[data-slot="photos-gallery-grid"]')!;
    expect(grid.className).toMatch(/grid-cols-2/);
    expect(grid.className).toMatch(/sm:grid-cols-4/);
  });
});

/**
 * PhotosTabContent unit tests — #2588 A1
 *
 * Coverage:
 *  - renders gallery container
 *  - lists photos from mocked photo-store
 *  - file capture calls addPhoto and displays new photo
 *  - delete button calls deletePhoto and removes photo
 *  - empty state shown when no photos
 */

import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { PhotosTabContent } from '../PhotosTabContent';

// ─── Mock photo-store ─────────────────────────────────────────────────────────

const mockListPhotos = vi.fn();
const mockAddPhoto = vi.fn();
const mockDeletePhoto = vi.fn();

vi.mock('@/lib/storage/photo-store', () => ({
  listPhotos: (...args: unknown[]) => mockListPhotos(...args),
  addPhoto: (...args: unknown[]) => mockAddPhoto(...args),
  deletePhoto: (...args: unknown[]) => mockDeletePhoto(...args),
}));

// ─── Helpers ──────────────────────────────────────────────────────────────────

function makeBlob(content = 'img'): Blob {
  return new Blob([content], { type: 'image/jpeg' });
}

function makeStoredPhoto(id: string, timestamp = 1000) {
  return {
    id,
    sessionId: 'sess-1',
    filename: `${id}.jpg`,
    timestamp,
    blob: makeBlob(),
  };
}

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('PhotosTabContent — empty state', () => {
  beforeEach(() => {
    mockListPhotos.mockResolvedValue([]);
    mockAddPhoto.mockReset();
    mockDeletePhoto.mockReset();
  });

  it('renders the gallery container', async () => {
    render(<PhotosTabContent sessionId="sess-1" />);
    expect(await screen.findByTestId('photos-tab-content')).toBeInTheDocument();
  });

  it('shows empty-state when no photos', async () => {
    render(<PhotosTabContent sessionId="sess-1" />);
    expect(await screen.findByText('Nessuna foto ancora')).toBeInTheDocument();
  });

  it('renders capture button', async () => {
    render(<PhotosTabContent sessionId="sess-1" />);
    expect(await screen.findByTestId('capture-button')).toBeInTheDocument();
  });

  it('renders hidden file input', async () => {
    render(<PhotosTabContent sessionId="sess-1" />);
    expect(await screen.findByTestId('photo-input')).toBeInTheDocument();
  });
});

describe('PhotosTabContent — listing photos', () => {
  beforeEach(() => {
    mockListPhotos.mockResolvedValue([makeStoredPhoto('p1', 1000), makeStoredPhoto('p2', 2000)]);
    mockAddPhoto.mockReset();
    mockDeletePhoto.mockReset();
  });

  it('calls listPhotos with sessionId on mount', async () => {
    render(<PhotosTabContent sessionId="sess-42" />);
    await waitFor(() => expect(mockListPhotos).toHaveBeenCalledWith('sess-42'));
  });

  it('displays photo count when photos exist', async () => {
    render(<PhotosTabContent sessionId="sess-1" />);
    expect(await screen.findByText('2 foto')).toBeInTheDocument();
  });

  it('renders an img for each photo', async () => {
    render(<PhotosTabContent sessionId="sess-1" />);
    const images = await screen.findAllByRole('img');
    expect(images).toHaveLength(2);
  });

  it('does not show empty state when photos exist', async () => {
    render(<PhotosTabContent sessionId="sess-1" />);
    await screen.findByText('2 foto');
    expect(screen.queryByText('Nessuna foto ancora')).not.toBeInTheDocument();
  });
});

describe('PhotosTabContent — capture', () => {
  beforeEach(() => {
    mockListPhotos.mockResolvedValue([]);
    const newBlob = makeBlob('new');
    mockAddPhoto.mockResolvedValue({
      id: 'p-new',
      sessionId: 'sess-1',
      filename: 'new.jpg',
      timestamp: 9000,
      blob: newBlob,
    });
    mockDeletePhoto.mockReset();
  });

  it('calls addPhoto when a file is selected', async () => {
    render(<PhotosTabContent sessionId="sess-1" />);
    await screen.findByTestId('photo-input');

    const file = new File(['img'], 'photo.jpg', { type: 'image/jpeg' });
    const input = screen.getByTestId('photo-input');

    await act(async () => {
      fireEvent.change(input, { target: { files: [file] } });
    });

    expect(mockAddPhoto).toHaveBeenCalledWith('sess-1', file, 'photo.jpg');
  });

  it('shows the new photo after capture', async () => {
    render(<PhotosTabContent sessionId="sess-1" />);
    await screen.findByTestId('photo-input');

    const file = new File(['img'], 'photo.jpg', { type: 'image/jpeg' });
    const input = screen.getByTestId('photo-input');

    await act(async () => {
      fireEvent.change(input, { target: { files: [file] } });
    });

    expect(await screen.findByText('1 foto')).toBeInTheDocument();
  });
});

describe('PhotosTabContent — delete', () => {
  beforeEach(() => {
    mockListPhotos.mockResolvedValue([makeStoredPhoto('p1', 1000)]);
    mockAddPhoto.mockReset();
    mockDeletePhoto.mockResolvedValue(undefined);
  });

  it('calls deletePhoto when delete button is clicked', async () => {
    render(<PhotosTabContent sessionId="sess-1" />);
    const deleteBtn = await screen.findByTestId('delete-photo-p1');

    await act(async () => {
      fireEvent.click(deleteBtn);
    });

    expect(mockDeletePhoto).toHaveBeenCalledWith('p1');
  });

  it('removes the photo from display after delete', async () => {
    render(<PhotosTabContent sessionId="sess-1" />);
    const deleteBtn = await screen.findByTestId('delete-photo-p1');

    await act(async () => {
      fireEvent.click(deleteBtn);
    });

    await waitFor(() => expect(screen.queryByTestId('delete-photo-p1')).not.toBeInTheDocument());
    expect(screen.getByText('Nessuna foto ancora')).toBeInTheDocument();
  });
});

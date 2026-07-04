/**
 * PhotosTabContent unit tests — #2588 A1 + A2
 *
 * Coverage:
 *  A1 (gallery):
 *   - renders gallery container
 *   - lists photos from mocked photo-store
 *   - file capture calls addPhoto and displays new photo
 *   - delete button calls deletePhoto and removes photo
 *   - empty state shown when no photos
 *  A2 (Vision-AI snapshots):
 *   - SessionSnapshotPanel rendered within the Foto tab
 *   - panel receives the correct sessionId + userId + currentTurn
 *   - gallery and snapshot panel coexist
 */

import type { ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { IntlProvider } from 'react-intl';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import itMessages from '@/locales/it.json';

import { PhotosTabContent } from '../PhotosTabContent';

// ─── i18n wrapper ──────────────────────────────────────────────────────────────

function flattenMessages(obj: Record<string, unknown>, prefix = ''): Record<string, string> {
  return Object.entries(obj).reduce<Record<string, string>>((acc, [key, val]) => {
    const fullKey = prefix ? `${prefix}.${key}` : key;
    if (typeof val === 'string') {
      acc[fullKey] = val;
    } else if (typeof val === 'object' && val !== null) {
      Object.assign(acc, flattenMessages(val as Record<string, unknown>, fullKey));
    }
    return acc;
  }, {});
}

const FLAT_IT = flattenMessages(itMessages as Record<string, unknown>);

function makeWrapper(qc?: QueryClient) {
  const client = qc ?? new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return function Wrapper({ children }: { children: ReactNode }) {
    return (
      <QueryClientProvider client={client}>
        <IntlProvider locale="it" messages={FLAT_IT}>
          {children}
        </IntlProvider>
      </QueryClientProvider>
    );
  };
}

function renderTab(props?: Partial<{ sessionId: string; userId: string; currentTurn: number }>) {
  return render(
    <PhotosTabContent
      sessionId={props?.sessionId ?? 'sess-1'}
      userId={props?.userId ?? 'user-1'}
      currentTurn={props?.currentTurn}
    />,
    { wrapper: makeWrapper() }
  );
}

// ─── Mock photo-store (A1 gallery) ──────────────────────────────────────────────

const mockListPhotos = vi.fn();
const mockAddPhoto = vi.fn();
const mockDeletePhoto = vi.fn();

vi.mock('@/lib/storage/photo-store', () => ({
  listPhotos: (...args: unknown[]) => mockListPhotos(...args),
  addPhoto: (...args: unknown[]) => mockAddPhoto(...args),
  deletePhoto: (...args: unknown[]) => mockDeletePhoto(...args),
}));

// ─── Spy-stub SessionSnapshotPanel (A2) ─────────────────────────────────────────
// Stub echoes received props into the DOM so threading is asserted
// deterministically without standing up the real server-backed hooks.

const snapshotPanelProps = vi.fn();

vi.mock('@/components/session/SessionSnapshotPanel', () => ({
  SessionSnapshotPanel: (props: { sessionId: string; userId: string; currentTurn?: number }) => {
    snapshotPanelProps(props);
    return (
      <div
        data-testid="mock-session-snapshot-panel"
        data-session-id={props.sessionId}
        data-user-id={props.userId}
        data-current-turn={String(props.currentTurn)}
      >
        SessionSnapshotPanel
      </div>
    );
  },
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
    snapshotPanelProps.mockReset();
  });

  it('renders the gallery container', async () => {
    renderTab();
    expect(await screen.findByTestId('photos-tab-content')).toBeInTheDocument();
  });

  it('shows empty-state when no photos', async () => {
    renderTab();
    expect(await screen.findByText('Nessuna foto ancora')).toBeInTheDocument();
  });

  it('renders capture button', async () => {
    renderTab();
    expect(await screen.findByTestId('capture-button')).toBeInTheDocument();
  });

  it('renders hidden file input', async () => {
    renderTab();
    expect(await screen.findByTestId('photo-input')).toBeInTheDocument();
  });
});

describe('PhotosTabContent — listing photos', () => {
  beforeEach(() => {
    mockListPhotos.mockResolvedValue([makeStoredPhoto('p1', 1000), makeStoredPhoto('p2', 2000)]);
    mockAddPhoto.mockReset();
    mockDeletePhoto.mockReset();
    snapshotPanelProps.mockReset();
  });

  it('calls listPhotos with sessionId on mount', async () => {
    renderTab({ sessionId: 'sess-42' });
    await waitFor(() => expect(mockListPhotos).toHaveBeenCalledWith('sess-42'));
  });

  it('displays photo count when photos exist', async () => {
    renderTab();
    expect(await screen.findByText('2 foto')).toBeInTheDocument();
  });

  it('renders an img for each photo', async () => {
    renderTab();
    const images = await screen.findAllByRole('img');
    expect(images).toHaveLength(2);
  });

  it('does not show empty state when photos exist', async () => {
    renderTab();
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
    snapshotPanelProps.mockReset();
  });

  it('calls addPhoto when a file is selected', async () => {
    renderTab();
    await screen.findByTestId('photo-input');

    const file = new File(['img'], 'photo.jpg', { type: 'image/jpeg' });
    const input = screen.getByTestId('photo-input');

    await act(async () => {
      fireEvent.change(input, { target: { files: [file] } });
    });

    expect(mockAddPhoto).toHaveBeenCalledWith('sess-1', file, 'photo.jpg');
  });

  it('shows the new photo after capture', async () => {
    renderTab();
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
    snapshotPanelProps.mockReset();
  });

  it('calls deletePhoto when delete button is clicked', async () => {
    renderTab();
    const deleteBtn = await screen.findByTestId('delete-photo-p1');

    await act(async () => {
      fireEvent.click(deleteBtn);
    });

    expect(mockDeletePhoto).toHaveBeenCalledWith('p1');
  });

  it('removes the photo from display after delete', async () => {
    renderTab();
    const deleteBtn = await screen.findByTestId('delete-photo-p1');

    await act(async () => {
      fireEvent.click(deleteBtn);
    });

    await waitFor(() => expect(screen.queryByTestId('delete-photo-p1')).not.toBeInTheDocument());
    expect(screen.getByText('Nessuna foto ancora')).toBeInTheDocument();
  });
});

describe('PhotosTabContent — Vision-AI snapshots (A2)', () => {
  beforeEach(() => {
    mockListPhotos.mockResolvedValue([]);
    mockAddPhoto.mockReset();
    mockDeletePhoto.mockReset();
    snapshotPanelProps.mockReset();
  });

  it('renders SessionSnapshotPanel within the Foto tab', async () => {
    renderTab();
    expect(await screen.findByTestId('mock-session-snapshot-panel')).toBeInTheDocument();
  });

  it('renders the snapshots section wrapper', async () => {
    renderTab();
    expect(await screen.findByTestId('photos-tab-snapshots')).toBeInTheDocument();
  });

  it('passes the correct sessionId + userId + currentTurn to the panel', async () => {
    renderTab({ sessionId: 'sess-77', userId: 'user-99', currentTurn: 5 });
    await screen.findByTestId('mock-session-snapshot-panel');

    expect(snapshotPanelProps).toHaveBeenCalledWith(
      expect.objectContaining({ sessionId: 'sess-77', userId: 'user-99', currentTurn: 5 })
    );
  });

  it('defaults currentTurn to 1 when not provided', async () => {
    renderTab({ sessionId: 'sess-1', userId: 'user-1' });
    await screen.findByTestId('mock-session-snapshot-panel');

    expect(snapshotPanelProps).toHaveBeenCalledWith(expect.objectContaining({ currentTurn: 1 }));
  });

  it('gallery and snapshot panel coexist in the same tab', async () => {
    mockListPhotos.mockResolvedValue([makeStoredPhoto('p1', 1000)]);
    renderTab();

    // Gallery present (photo card from store)
    expect(await screen.findByTestId('delete-photo-p1')).toBeInTheDocument();
    // Snapshot panel present alongside it
    expect(screen.getByTestId('mock-session-snapshot-panel')).toBeInTheDocument();
  });
});

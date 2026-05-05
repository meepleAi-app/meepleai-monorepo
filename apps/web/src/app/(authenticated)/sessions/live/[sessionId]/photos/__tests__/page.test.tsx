import 'fake-indexeddb/auto';
import { Suspense } from 'react';
import {
  render as rtlRender,
  screen,
  fireEvent,
  waitFor,
  type RenderResult,
} from '@testing-library/react';
import { describe, it, expect, beforeEach } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import PhotosPage from '../page';
import { clearAllPhotos, addPhoto } from '@/lib/storage/photo-store';

function render(ui: React.ReactElement): RenderResult {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 0 } },
  });
  return rtlRender(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>);
}

// jsdom doesn't implement URL.createObjectURL / revokeObjectURL.
// Stub them so <img src={...}> and revoke-on-unmount work in tests.
if (typeof URL.createObjectURL === 'undefined') {
  // @ts-expect-error — assign for jsdom
  URL.createObjectURL = () => 'blob:mock';
}
if (typeof URL.revokeObjectURL === 'undefined') {
  // @ts-expect-error — assign for jsdom
  URL.revokeObjectURL = () => {};
}

function makeFakeFile(name = 'snap.png'): File {
  return new File([new Blob(['fake'], { type: 'image/png' })], name, { type: 'image/png' });
}

/**
 * Build an "already fulfilled" thenable that React.use() can unwrap
 * synchronously without suspending. React 19 supports thenables with
 * `status: 'fulfilled'` + `value` set up-front.
 */
function fulfilledParams<T extends object>(value: T): Promise<T> {
  const thenable = Promise.resolve(value) as Promise<T> & { status: string; value: T };
  thenable.status = 'fulfilled';
  thenable.value = value;
  return thenable;
}

function renderPhotosPage(sessionId: string): RenderResult {
  return render(
    <Suspense fallback={null}>
      <PhotosPage params={fulfilledParams({ sessionId })} />
    </Suspense>
  );
}

describe('PhotosPage — IndexedDB persistence', () => {
  beforeEach(async () => {
    await clearAllPhotos();
  });

  it('loads existing photos from store on mount', async () => {
    await addPhoto('sess-x', new Blob(['a'], { type: 'image/png' }), 'preexisting.png');

    renderPhotosPage('sess-x');

    await waitFor(() => expect(screen.getByText(/1 foto/)).toBeInTheDocument());
  });

  it('persists captured photo and shows it in the grid', async () => {
    renderPhotosPage('sess-y');

    const input = await screen.findByTestId('photo-input');
    fireEvent.change(input, { target: { files: [makeFakeFile()] } });

    await waitFor(() => expect(screen.getByText(/1 foto/)).toBeInTheDocument());
  });

  it('survives a remount (data persisted in IndexedDB)', async () => {
    const { unmount } = renderPhotosPage('sess-z');

    const input = await screen.findByTestId('photo-input');
    fireEvent.change(input, { target: { files: [makeFakeFile()] } });
    await waitFor(() => expect(screen.getByText(/1 foto/)).toBeInTheDocument());

    unmount();

    renderPhotosPage('sess-z');
    await waitFor(() => expect(screen.getByText(/1 foto/)).toBeInTheDocument());
  });

  it('delete removes the photo from store', async () => {
    renderPhotosPage('sess-d');

    const input = await screen.findByTestId('photo-input');
    fireEvent.change(input, { target: { files: [makeFakeFile()] } });
    await waitFor(() => expect(screen.getByText(/1 foto/)).toBeInTheDocument());

    const deleteBtn = await screen.findByLabelText(/Elimina foto/i);
    fireEvent.click(deleteBtn);

    await waitFor(() => expect(screen.getByText(/Nessuna foto ancora/i)).toBeInTheDocument());
  });
});

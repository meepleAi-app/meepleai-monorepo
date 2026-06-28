/**
 * EndgamePhotoUploadSection tests — SP4 #2501
 *
 * Test cases:
 * 1. selecting_files_shows_local_previews
 * 2. upload_disabled_while_recordId_null
 * 3. successful_upload_marks_photo_done_and_calls_onUploadingChange
 * 4. failed_upload_shows_inline_error_and_retry
 * 5. respects_max_files
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import type { ReactNode } from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { IntlProvider } from 'react-intl';

import itMessages from '@/locales/it.json';

// Flatten nested i18n JSON to { 'a.b.c': 'value' } for IntlProvider
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

// ─── Mock usePlayRecordPhotoUpload ────────────────────────────────────────────

const mutateAsyncMock = vi.fn();

vi.mock('@/hooks/mutations/usePlayRecordPhotoUpload', () => ({
  usePlayRecordPhotoUpload: () => ({
    mutateAsync: mutateAsyncMock,
    isPending: false,
  }),
}));

// Mock heic2any to avoid browser API issues in jsdom
vi.mock('heic2any', () => ({ default: vi.fn() }));

// ─── Helpers ─────────────────────────────────────────────────────────────────

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

function makeFile(name: string, type = 'image/jpeg', size = 1024): File {
  return new File(['x'.repeat(size)], name, { type });
}

import { EndgamePhotoUploadSection } from '../EndgamePhotoUploadSection';

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('EndgamePhotoUploadSection', () => {
  beforeEach(() => {
    mutateAsyncMock.mockReset();
    // jsdom does not implement URL.createObjectURL; provide a stub
    global.URL.createObjectURL = vi.fn(() => 'blob:mock-url');
    global.URL.revokeObjectURL = vi.fn();
  });

  it('selecting_files_shows_local_previews — 2 files shown even with recordId=null', () => {
    render(<EndgamePhotoUploadSection recordId={null} />, {
      wrapper: makeWrapper(),
    });

    const input = screen.getByLabelText(/seleziona foto/i);
    const file1 = makeFile('photo1.jpg');
    const file2 = makeFile('photo2.jpg');
    fireEvent.change(input, { target: { files: [file1, file2] } });

    expect(screen.getAllByTestId('photo-preview-item')).toHaveLength(2);
  });

  it('upload_disabled_while_recordId_null — shows preparing label when null, carica label when present', () => {
    const { rerender } = render(<EndgamePhotoUploadSection recordId={null} />, {
      wrapper: makeWrapper(),
    });

    const input = screen.getByLabelText(/seleziona foto/i);
    fireEvent.change(input, { target: { files: [makeFile('photo1.jpg')] } });

    // When recordId=null the button shows the "preparing" label and is disabled
    const preparingBtn = screen.getByRole('button', { name: /preparazione/i });
    expect(preparingBtn).toBeDisabled();

    rerender(<EndgamePhotoUploadSection recordId="rec-123" />);
    // After recordId is set the button switches to the upload label and is enabled
    expect(screen.getByRole('button', { name: /carica foto/i })).not.toBeDisabled();
  });

  it('successful_upload_marks_photo_done_and_calls_onUploadingChange', async () => {
    mutateAsyncMock.mockResolvedValue({
      photoId: 'p1',
      photoUrl: 'http://example.com/p1.jpg',
      thumbnailUrl: null,
      ocrText: null,
      wasDeduplicated: false,
    });

    const onUploadingChange = vi.fn();
    render(<EndgamePhotoUploadSection recordId="rec-123" onUploadingChange={onUploadingChange} />, {
      wrapper: makeWrapper(),
    });

    const input = screen.getByLabelText(/seleziona foto/i);
    fireEvent.change(input, { target: { files: [makeFile('photo1.jpg')] } });

    const uploadBtn = screen.getByRole('button', { name: /carica foto/i });
    fireEvent.click(uploadBtn);

    await waitFor(() => expect(screen.getByText(/caricata/i)).toBeInTheDocument());
    expect(onUploadingChange).toHaveBeenCalledWith(true);
    expect(onUploadingChange).toHaveBeenCalledWith(false);
  });

  it('failed_upload_shows_inline_error_and_retry — first photo errors, second succeeds; retry resolves first to done', async () => {
    // photo1 fails, photo2 succeeds on initial upload
    mutateAsyncMock
      .mockRejectedValueOnce(new Error('Upload failed')) // photo1 initial attempt → error
      .mockResolvedValueOnce({
        // photo2 initial attempt → done
        photoId: 'p2',
        photoUrl: 'http://example.com/p2.jpg',
        thumbnailUrl: null,
        ocrText: null,
        wasDeduplicated: false,
      })
      .mockResolvedValueOnce({
        // photo1 retry → done
        photoId: 'p1',
        photoUrl: 'http://example.com/p1.jpg',
        thumbnailUrl: null,
        ocrText: null,
        wasDeduplicated: false,
      });

    render(<EndgamePhotoUploadSection recordId="rec-123" />, {
      wrapper: makeWrapper(),
    });

    const input = screen.getByLabelText(/seleziona foto/i);
    // Select 2 files
    fireEvent.change(input, {
      target: { files: [makeFile('photo1.jpg'), makeFile('photo2.jpg')] },
    });

    const uploadBtn = screen.getByRole('button', { name: /carica foto/i });
    fireEvent.click(uploadBtn);

    // (a) photo1 should show an inline error and retry button
    await waitFor(() => expect(screen.getByRole('alert')).toBeInTheDocument());
    const retryBtn = screen.getByRole('button', { name: /riprova/i });
    expect(retryBtn).toBeInTheDocument();

    // (b) photo2 should be in the "done" state (not affected by photo1 error)
    // The done badge appears for photo2 while photo1 still has the error
    await waitFor(() => {
      const items = screen.getAllByTestId('photo-preview-item');
      expect(items).toHaveLength(2);
      const doneItems = items.filter(el => el.getAttribute('data-status') === 'done');
      const errorItems = items.filter(el => el.getAttribute('data-status') === 'error');
      expect(doneItems).toHaveLength(1); // photo2 is done
      expect(errorItems).toHaveLength(1); // photo1 is in error
    });

    // Click retry on photo1 — should resolve to done
    fireEvent.click(retryBtn);

    // Now both should be done
    await waitFor(() => {
      const items = screen.getAllByTestId('photo-preview-item');
      const doneItems = items.filter(el => el.getAttribute('data-status') === 'done');
      expect(doneItems).toHaveLength(2);
    });
  });

  it('respects_max_files — caps selection to 10, shows warning alert if more', () => {
    render(<EndgamePhotoUploadSection recordId="rec-123" />, {
      wrapper: makeWrapper(),
    });

    const input = screen.getByLabelText(/seleziona foto/i);
    const files = Array.from({ length: 12 }, (_, i) => makeFile(`photo${i}.jpg`));
    fireEvent.change(input, { target: { files } });

    // Capped to 10
    expect(screen.getAllByTestId('photo-preview-item')).toHaveLength(10);
    // Warning alert about too many files
    expect(screen.getByRole('alert')).toBeInTheDocument();
  });
});

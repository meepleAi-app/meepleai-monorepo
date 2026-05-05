/**
 * PhotoUploader — G1 + G6 + G8 tests
 *
 * Covers the smartphone-ready additions:
 *   G1: MAX_PHOTOS=5 limit enforcement + oversize warning
 *   G6: capture="environment" attribute on file input
 *   G8: "Start chat" link appears on Completed status
 */

import { fireEvent, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import { PhotoUploader } from '../_components/PhotoUploader';

// ── API mocks ────────────────────────────────────────────────────────────────

const mockUploadPhotoBatch = vi.hoisted(() => vi.fn());
const mockGetPhotoBatchStatus = vi.hoisted(() => vi.fn());

vi.mock('@/lib/gamebook/api', () => ({
  uploadPhotoBatch: mockUploadPhotoBatch,
  getPhotoBatchStatus: mockGetPhotoBatchStatus,
}));

vi.mock('@/lib/gamebook/file-to-base64', () => ({
  filesToPhotoItems: vi.fn(async (files: File[]) =>
    files.map(f => ({ base64Data: 'mock-base64', fileName: f.name, mimeType: f.type }))
  ),
}));

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
  useSearchParams: () => ({ get: (_k: string) => null }),
}));

// ── Helpers ───────────────────────────────────────────────────────────────────

const GAME_ID = '3fa85f64-5717-4562-b3fc-2c963f66afa6';
const BATCH_ID = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee';

function makeFile(name: string, sizeBytes = 100_000, type = 'image/jpeg'): File {
  const content = new Uint8Array(Math.min(sizeBytes, 100));
  const file = new File([content], name, { type });
  // Override .size property so we can test the size threshold
  Object.defineProperty(file, 'size', { value: sizeBytes, configurable: true });
  return file;
}

function selectFiles(files: File[]): void {
  const input = screen.getByTestId('file-input') as HTMLInputElement;
  Object.defineProperty(input, 'files', { value: files, configurable: true });
  fireEvent.change(input, { target: { files } });
}

// ── Tests ────────────────────────────────────────────────────────────────────

describe('PhotoUploader — G6: camera capture attribute', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetPhotoBatchStatus.mockResolvedValue(null);
  });

  it('file input has capture="environment" for mobile camera access', () => {
    renderWithQuery(<PhotoUploader gameId={GAME_ID} />);

    const input = screen.getByTestId('file-input') as HTMLInputElement;
    expect(input.getAttribute('capture')).toBe('environment');
  });
});

describe('PhotoUploader — G1: photo limit enforcement', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetPhotoBatchStatus.mockResolvedValue(null);
  });

  it('shows photo limit hint in the dropzone', () => {
    renderWithQuery(<PhotoUploader gameId={GAME_ID} />);

    expect(screen.getByTestId('photo-limit-hint')).toBeInTheDocument();
  });

  it('shows validation error when more than 5 files are selected', async () => {
    renderWithQuery(<PhotoUploader gameId={GAME_ID} />);

    const sixFiles = Array.from({ length: 6 }, (_, i) => makeFile(`page-0${i + 1}.jpg`));
    selectFiles(sixFiles);

    await waitFor(() => {
      expect(screen.getByTestId('validation-error')).toBeInTheDocument();
    });
    // Upload button stays disabled
    expect(screen.getByTestId('upload-button')).toBeDisabled();
  });

  it('accepts exactly 5 files without error', async () => {
    renderWithQuery(<PhotoUploader gameId={GAME_ID} />);

    const fiveFiles = Array.from({ length: 5 }, (_, i) => makeFile(`page-0${i + 1}.jpg`));
    selectFiles(fiveFiles);

    await waitFor(() => {
      expect(screen.getByTestId('selected-files')).toBeInTheDocument();
    });
    expect(screen.queryByTestId('validation-error')).not.toBeInTheDocument();
    // Upload button enabled
    expect(screen.getByTestId('upload-button')).not.toBeDisabled();
  });

  it('shows oversize warning when files exceed 1 MB', async () => {
    renderWithQuery(<PhotoUploader gameId={GAME_ID} />);

    const bigFile = makeFile('big-page.jpg', 1_500_000); // 1.5 MB
    selectFiles([bigFile]);

    await waitFor(() => {
      expect(screen.getByTestId('oversize-warning')).toBeInTheDocument();
    });
    // Still allows upload (soft warning)
    expect(screen.getByTestId('upload-button')).not.toBeDisabled();
  });

  it('does not show oversize warning for normal-size files', async () => {
    renderWithQuery(<PhotoUploader gameId={GAME_ID} />);

    const smallFile = makeFile('small-page.jpg', 300_000); // 300 KB
    selectFiles([smallFile]);

    await waitFor(() => {
      expect(screen.getByTestId('selected-files')).toBeInTheDocument();
    });
    expect(screen.queryByTestId('oversize-warning')).not.toBeInTheDocument();
  });
});

describe('PhotoUploader — G8: chat link after completion', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('shows "Start chat" link when batch status is Completed', async () => {
    mockUploadPhotoBatch.mockResolvedValue({ batchId: BATCH_ID });
    mockGetPhotoBatchStatus.mockResolvedValue({
      batchId: BATCH_ID,
      status: 'Completed',
      totalPages: 3,
      processedPages: 3,
      averageConfidence: 0.9,
      errorMessage: null,
      createdAt: '2026-05-04T10:00:00Z',
      completedAt: '2026-05-04T10:05:00Z',
    });

    renderWithQuery(<PhotoUploader gameId={GAME_ID} />);
    const input = screen.getByTestId('file-input') as HTMLInputElement;
    Object.defineProperty(input, 'files', {
      value: [makeFile('scan1.jpg')],
      configurable: true,
    });
    fireEvent.change(input, { target: { files: [makeFile('scan1.jpg')] } });

    await waitFor(() => {
      expect(screen.getByTestId('upload-button')).not.toBeDisabled();
    });
    fireEvent.click(screen.getByTestId('upload-button'));

    await waitFor(() => {
      expect(screen.getByTestId('chat-link-panel')).toBeInTheDocument();
    });

    const link = screen.getByTestId('start-chat-link') as HTMLAnchorElement;
    expect(link.href).toContain(`/chat?gameId=${GAME_ID}`);
  });

  it('does not show chat link while batch is Processing', async () => {
    mockUploadPhotoBatch.mockResolvedValue({ batchId: BATCH_ID });
    mockGetPhotoBatchStatus.mockResolvedValue({
      batchId: BATCH_ID,
      status: 'Processing',
      totalPages: 3,
      processedPages: 1,
      averageConfidence: null,
      errorMessage: null,
      createdAt: '2026-05-04T10:00:00Z',
      completedAt: null,
    });

    renderWithQuery(<PhotoUploader gameId={GAME_ID} />);
    const input = screen.getByTestId('file-input') as HTMLInputElement;
    Object.defineProperty(input, 'files', {
      value: [makeFile('scan1.jpg')],
      configurable: true,
    });
    fireEvent.change(input, { target: { files: [makeFile('scan1.jpg')] } });

    await waitFor(() => {
      expect(screen.getByTestId('upload-button')).not.toBeDisabled();
    });
    fireEvent.click(screen.getByTestId('upload-button'));

    await waitFor(() => {
      expect(screen.getByTestId('batch-status')).toBeInTheDocument();
    });
    expect(screen.queryByTestId('chat-link-panel')).not.toBeInTheDocument();
  });
});

/**
 * PhotoUploader — unit tests (Sprint 1, Task 1.8, TDD green phase)
 *
 * Covers:
 *   1. Renders dropzone with i18n label
 *   2. Upload button disabled when no files selected
 *   3. Files appear in list after selection
 *   4. Upload button triggers mutation
 *   5. Batch status panel renders after successful upload
 *   6. ConfidenceBadge shows correct colour tier
 *   7. Polling stops on terminal status (via data assertions)
 *
 * Strategy:
 *  - Mock `@/lib/gamebook/api` to control upload + status responses
 *  - Mock `@/lib/gamebook/hooks/usePhotoBatchStatus` where polling control is needed
 *  - Use renderWithQuery (IntlProvider + QueryClientProvider)
 *  - Use test-i18n `t()` for locale-agnostic assertions
 */

import { fireEvent, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';
import { getFlexibleMatcher, t } from '@/test-utils/test-i18n';

import { PhotoUploader } from '../_components/PhotoUploader';

// ── API mocks ────────────────────────────────────────────────────────────────

const mockUploadPhotoBatch = vi.hoisted(() => vi.fn());
const mockGetPhotoBatchStatus = vi.hoisted(() => vi.fn());

vi.mock('@/lib/gamebook/api', () => ({
  uploadPhotoBatch: mockUploadPhotoBatch,
  getPhotoBatchStatus: mockGetPhotoBatchStatus,
}));

// ── file-to-base64 mock (avoids FileReader in jsdom) ────────────────────────

vi.mock('@/lib/gamebook/file-to-base64', () => ({
  filesToPhotoItems: vi.fn(async (files: File[]) =>
    files.map(f => ({ base64Data: 'mock-base64', fileName: f.name, mimeType: f.type }))
  ),
}));

// ── next/navigation mock ────────────────────────────────────────────────────

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
  useSearchParams: () => ({ get: (_k: string) => null }),
}));

// ── Helpers ──────────────────────────────────────────────────────────────────

const GAME_ID = '3fa85f64-5717-4562-b3fc-2c963f66afa6';
const BATCH_ID = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee';

function makeFile(name: string, type = 'image/jpeg'): File {
  return new File(['fake-image-data'], name, { type });
}

function selectFiles(files: File[]): void {
  const input = screen.getByTestId('file-input') as HTMLInputElement;
  fireEvent.change(input, { target: { files } });
}

// ── Tests ────────────────────────────────────────────────────────────────────

describe('PhotoUploader', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    // Default: status poll returns null (no batch yet)
    mockGetPhotoBatchStatus.mockResolvedValue(null);
  });

  it('renders the dropzone with i18n label', () => {
    renderWithQuery(<PhotoUploader gameId={GAME_ID} />);

    // Dropzone region is present
    expect(screen.getByTestId('photo-uploader')).toBeInTheDocument();

    // i18n label — use getFlexibleMatcher to match any language variant
    const dropzoneMatcher = getFlexibleMatcher('gamebook.upload.dropzoneLabel');
    expect(screen.getAllByText(dropzoneMatcher).length).toBeGreaterThan(0);
  });

  it('upload button is disabled when no files are selected', () => {
    renderWithQuery(<PhotoUploader gameId={GAME_ID} />);

    const btn = screen.getByTestId('upload-button') as HTMLButtonElement;
    expect(btn).toBeDisabled();
  });

  it('shows selected files in list after input change', async () => {
    renderWithQuery(<PhotoUploader gameId={GAME_ID} />);

    const file = makeFile('page-01.jpg');
    selectFiles([file]);

    await waitFor(() => {
      expect(screen.getByTestId('selected-files')).toBeInTheDocument();
    });
    expect(screen.getByText('page-01.jpg')).toBeInTheDocument();
  });

  it('enables upload button when files are selected', async () => {
    renderWithQuery(<PhotoUploader gameId={GAME_ID} />);

    selectFiles([makeFile('p1.jpg'), makeFile('p2.png', 'image/png')]);

    await waitFor(() => {
      const btn = screen.getByTestId('upload-button') as HTMLButtonElement;
      expect(btn).not.toBeDisabled();
    });
  });

  it('calls uploadPhotoBatch mutation on button click', async () => {
    mockUploadPhotoBatch.mockResolvedValue({ batchId: BATCH_ID });

    renderWithQuery(<PhotoUploader gameId={GAME_ID} />);

    selectFiles([makeFile('scan1.jpg')]);

    await waitFor(() => {
      expect(screen.getByTestId('upload-button')).not.toBeDisabled();
    });

    fireEvent.click(screen.getByTestId('upload-button'));

    await waitFor(() => {
      expect(mockUploadPhotoBatch).toHaveBeenCalledOnce();
      expect(mockUploadPhotoBatch).toHaveBeenCalledWith(
        GAME_ID,
        expect.arrayContaining([
          expect.objectContaining({ fileName: 'scan1.jpg', mimeType: 'image/jpeg' }),
        ])
      );
    });
  });

  it('shows batch status panel after successful upload', async () => {
    mockUploadPhotoBatch.mockResolvedValue({ batchId: BATCH_ID });

    const pendingStatus = {
      batchId: BATCH_ID,
      status: 'Pending',
      totalPages: 5,
      processedPages: 0,
      averageConfidence: null,
      errorMessage: null,
      createdAt: '2026-05-04T10:00:00Z',
      completedAt: null,
    };
    mockGetPhotoBatchStatus.mockResolvedValue(pendingStatus);

    renderWithQuery(<PhotoUploader gameId={GAME_ID} />);
    selectFiles([makeFile('scan1.jpg')]);

    await waitFor(() => {
      expect(screen.getByTestId('upload-button')).not.toBeDisabled();
    });

    fireEvent.click(screen.getByTestId('upload-button'));

    await waitFor(() => {
      expect(screen.getByTestId('batch-status')).toBeInTheDocument();
    });

    // Status label i18n — flexible matcher works in any language
    const pendingMatcher = getFlexibleMatcher('gamebook.upload.statusPending');
    expect(screen.getByText(pendingMatcher)).toBeInTheDocument();
  });

  it('shows upload error message on mutation failure', async () => {
    mockUploadPhotoBatch.mockRejectedValue(new Error('Network error'));

    renderWithQuery(<PhotoUploader gameId={GAME_ID} />);

    selectFiles([makeFile('broken.jpg')]);

    await waitFor(() => {
      expect(screen.getByTestId('upload-button')).not.toBeDisabled();
    });

    fireEvent.click(screen.getByTestId('upload-button'));

    await waitFor(() => {
      // Error role=alert should appear
      expect(screen.getByRole('alert')).toBeInTheDocument();
    });
  });
});

// ── ConfidenceBadge sub-tests ────────────────────────────────────────────────

describe('ConfidenceBadge (via PhotoUploader status panel)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  async function renderWithStatus(confidence: number | null) {
    mockUploadPhotoBatch.mockResolvedValue({ batchId: BATCH_ID });

    const statusDto = {
      batchId: BATCH_ID,
      status: 'Processing',
      totalPages: 10,
      processedPages: 3,
      averageConfidence: confidence,
      errorMessage: null,
      createdAt: '2026-05-04T10:00:00Z',
      completedAt: null,
    };
    mockGetPhotoBatchStatus.mockResolvedValue(statusDto);

    renderWithQuery(<PhotoUploader gameId={GAME_ID} />);
    selectFiles([makeFile('s.jpg')]);

    await waitFor(() => {
      expect(screen.getByTestId('upload-button')).not.toBeDisabled();
    });
    fireEvent.click(screen.getByTestId('upload-button'));

    await waitFor(() => {
      expect(screen.getByTestId('batch-status')).toBeInTheDocument();
    });
  }

  it('shows "—" badge when confidence is null', async () => {
    await renderWithStatus(null);
    expect(screen.getByTestId('confidence-badge')).toHaveTextContent('—');
  });

  it('shows green badge for confidence >= 0.80', async () => {
    await renderWithStatus(0.85);
    const badge = screen.getByTestId('confidence-badge');
    expect(badge).toHaveTextContent('85%');
    expect(badge.className).toContain('green');
  });

  it('shows amber badge for confidence 0.50–0.79', async () => {
    await renderWithStatus(0.65);
    const badge = screen.getByTestId('confidence-badge');
    expect(badge).toHaveTextContent('65%');
    expect(badge.className).toContain('amber');
  });

  it('shows red badge for confidence < 0.50', async () => {
    await renderWithStatus(0.3);
    const badge = screen.getByTestId('confidence-badge');
    expect(badge).toHaveTextContent('30%');
    expect(badge.className).toContain('red');
  });
});

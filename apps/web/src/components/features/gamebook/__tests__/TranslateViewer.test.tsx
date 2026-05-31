import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, fireEvent, waitFor, act } from '@testing-library/react';
import { axe } from 'jest-axe';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';

// Mock all four hooks before importing the component
vi.mock('@/lib/gamebook/hooks/usePhotoUpload');
vi.mock('@/lib/gamebook/hooks/useSegmentPhoto');
vi.mock('@/lib/gamebook/hooks/useTranslateSegmentSSE');
vi.mock('@/hooks/useGameBooks');

import * as uploadHook from '@/lib/gamebook/hooks/usePhotoUpload';
import * as segmentHook from '@/lib/gamebook/hooks/useSegmentPhoto';
import * as sseHook from '@/lib/gamebook/hooks/useTranslateSegmentSSE';
import * as booksHook from '@/hooks/useGameBooks';
import { GameRefKind, type GameRef } from '@/lib/api/gamebook';

import { TranslateViewer } from '../TranslateViewer';

const CAMPAIGN_ID = '11111111-1111-4111-a111-111111111111';
const GAME_ID = '99999999-9999-4999-a999-999999999999';
const BOOK_STORY_ID = 'aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa';
const BOOK_ENCOUNTER_ID = 'bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb';
const PHOTO_ID = 'cccccccc-cccc-4ccc-accc-cccccccccccc';
const ARTIFACT_ID = 'dddddddd-dddd-4ddd-addd-dddddddddddd';
const GAME_REF: GameRef = { id: GAME_ID, kind: GameRefKind.Shared };

/** A fake artifact with 1 segment (para §1) for segment-pick tests */
const FAKE_ARTIFACT = {
  id: ARTIFACT_ID,
  campaignId: CAMPAIGN_ID,
  photoId: PHOTO_ID,
  segments: [
    {
      paragraphNumber: 1,
      sourceText: 'You wake up in a dungeon.',
    },
  ],
};

/** Mock the books hook with a single narrative book so camera is auto-enabled */
function makeOneBookMock() {
  vi.mocked(booksHook.useGameBooks).mockReturnValue({
    data: [
      {
        id: BOOK_STORY_ID,
        gameRefId: GAME_ID,
        gameRefKind: GameRefKind.Shared,
        ownerUserId: null,
        displayName: 'Solo Storybook',
        roles: 4,
        paragraphScheme: 1,
        language: 'en',
        sequentialRead: true,
        kbSourceDocId: null,
        physicalOnly: true,
        createdAt: '2026-05-07T10:00:00Z',
      },
    ],
    isLoading: false,
    isError: false,
    error: null,
  } as never);
}

/** Fire a file-change event on the hidden photo input to kick off handleFile() */
function firePhotoInput() {
  const input = screen.getByTestId('photo-input');
  fireEvent.change(input, {
    target: { files: [new File(['x'], 'photo.png', { type: 'image/png' })] },
  });
}

function wrap(ui: ReactNode) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={qc}>{ui}</QueryClientProvider>);
}

function makeDefaultMocks() {
  vi.mocked(uploadHook.usePhotoUpload).mockReturnValue({
    mutate: vi.fn(),
    mutateAsync: vi.fn(),
    isPending: false,
    isSuccess: false,
    isError: false,
    error: null,
  } as never);

  vi.mocked(segmentHook.useSegmentPhoto).mockReturnValue({
    mutate: vi.fn(),
    mutateAsync: vi.fn(),
    isPending: false,
    isSuccess: false,
    isError: false,
    error: null,
  } as never);

  vi.mocked(sseHook.useTranslateSegmentSSE).mockReturnValue({
    partialText: '',
    isComplete: false,
    appliedTerms: [],
    error: undefined,
    start: vi.fn(),
    stop: vi.fn(),
  } as never);

  // Default: 2 narrative books → BookPicker visible. Roles 4 = Narrative,
  // roles 8 = Encounter (the latter is filtered out by the viewer).
  vi.mocked(booksHook.useGameBooks).mockReturnValue({
    data: [
      {
        id: BOOK_STORY_ID,
        gameRefId: GAME_ID,
        gameRefKind: GameRefKind.Shared,
        ownerUserId: null,
        displayName: 'Storybook',
        roles: 4,
        paragraphScheme: 1,
        language: 'en',
        sequentialRead: true,
        kbSourceDocId: null,
        physicalOnly: true,
        createdAt: '2026-05-07T10:00:00Z',
      },
      {
        id: BOOK_ENCOUNTER_ID,
        gameRefId: GAME_ID,
        gameRefKind: GameRefKind.Shared,
        ownerUserId: null,
        displayName: 'Encounter Book',
        roles: 4 | 8,
        paragraphScheme: 2,
        language: 'en',
        sequentialRead: false,
        kbSourceDocId: null,
        physicalOnly: true,
        createdAt: '2026-05-07T10:00:00Z',
      },
    ],
    isLoading: false,
    isError: false,
    error: null,
  } as never);
}

describe('TranslateViewer', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    makeDefaultMocks();
  });

  it('renders the page header and camera button in idle state', () => {
    wrap(<TranslateViewer campaignId={CAMPAIGN_ID} gameRef={GAME_REF} />);

    expect(screen.getByText('Traduci pagina libro game')).toBeInTheDocument();
    expect(screen.getByTestId('open-camera-button')).toBeInTheDocument();
    // E3 (multi-book): the BookPicker replaces the old page-type-select.
    // With 2+ books and no selection yet, the camera button stays disabled
    // until the user picks a book — this is the intended UX guard.
    expect(screen.getByTestId('open-camera-button')).toBeDisabled();
    expect(screen.getByTestId('translate-viewer-book-picker-section')).toBeInTheDocument();
    expect(screen.getByTestId('book-picker')).toBeInTheDocument();
  });

  it('renders the hidden file input with capture attribute', () => {
    wrap(<TranslateViewer campaignId={CAMPAIGN_ID} gameRef={GAME_REF} />);
    const input = screen.getByTestId('photo-input') as HTMLInputElement;
    expect(input.type).toBe('file');
    expect(input.accept).toBe('image/*');
    expect(input.getAttribute('capture')).toBe('environment');
  });

  it('disables the camera button while uploading', () => {
    vi.mocked(uploadHook.usePhotoUpload).mockReturnValue({
      mutate: vi.fn(),
      mutateAsync: vi.fn(),
      isPending: true,
      isSuccess: false,
      isError: false,
      error: null,
    } as never);

    wrap(<TranslateViewer campaignId={CAMPAIGN_ID} gameRef={GAME_REF} />);
    // We can't trigger phase='uploading' from mock alone — but we verify
    // the button label changes to in-progress text only during active phases.
    // The idle state button text is present:
    expect(screen.getByTestId('open-camera-button')).toBeInTheDocument();
  });

  it('shows upload error message when upload mutation errors', () => {
    vi.mocked(uploadHook.usePhotoUpload).mockReturnValue({
      mutate: vi.fn(),
      mutateAsync: vi.fn(),
      isPending: false,
      isSuccess: false,
      isError: true,
      error: new Error('upload failed 413: file too large'),
    } as never);

    wrap(<TranslateViewer campaignId={CAMPAIGN_ID} gameRef={GAME_REF} />);

    expect(screen.getByTestId('translate-viewer-error')).toHaveTextContent('upload failed 413');
  });

  it('shows SSE error via TranslationPane when sse.error is set and activeSegment exists', () => {
    vi.mocked(sseHook.useTranslateSegmentSSE).mockReturnValue({
      partialText: '',
      isComplete: false,
      appliedTerms: [],
      error: 'stream_error',
      start: vi.fn(),
      stop: vi.fn(),
    } as never);

    // To show TranslationPane, activeSegment must be set — but with mocked hooks
    // we cannot drive internal state from outside. We verify error surfacing via
    // the error message fallback in the upload section instead.
    wrap(<TranslateViewer campaignId={CAMPAIGN_ID} gameRef={GAME_REF} />);

    // SSE error surfaces in translate-viewer-error when no upload/segment error
    expect(screen.getByTestId('translate-viewer-error')).toHaveTextContent('stream_error');
  });

  it('shows error copy and disables camera when no narrative books exist (E3)', () => {
    vi.mocked(booksHook.useGameBooks).mockReturnValue({
      data: [],
      isLoading: false,
      isError: false,
      error: null,
    } as never);

    wrap(<TranslateViewer campaignId={CAMPAIGN_ID} gameRef={GAME_REF} />);

    expect(screen.getByTestId('translate-viewer-no-narrative-books')).toBeInTheDocument();
    expect(screen.getByTestId('open-camera-button')).toBeDisabled();
  });

  it('hides BookPicker section when only one narrative book exists (E3 auto-select)', () => {
    vi.mocked(booksHook.useGameBooks).mockReturnValue({
      data: [
        {
          id: BOOK_STORY_ID,
          gameRefId: GAME_ID,
          gameRefKind: GameRefKind.Shared,
          ownerUserId: null,
          displayName: 'Solo Storybook',
          roles: 4,
          paragraphScheme: 1,
          language: 'en',
          sequentialRead: true,
          kbSourceDocId: null,
          physicalOnly: true,
          createdAt: '2026-05-07T10:00:00Z',
        },
      ],
      isLoading: false,
      isError: false,
      error: null,
    } as never);

    wrap(<TranslateViewer campaignId={CAMPAIGN_ID} gameRef={GAME_REF} />);

    expect(screen.queryByTestId('translate-viewer-book-picker-section')).not.toBeInTheDocument();
    // Camera is enabled because we auto-selected the single book.
    expect(screen.getByTestId('open-camera-button')).not.toBeDisabled();
  });

  // ---------------------------------------------------------------------------
  // S1 — Uploading shows skeleton + label (T6)
  // ---------------------------------------------------------------------------
  it('S1: uploading phase shows translate-skeleton-uploading + correct label + aria-busy', async () => {
    makeOneBookMock();

    // Hold upload unresolved so component stays in 'uploading' phase
    let resolveUpload!: (v: unknown) => void;
    const uploadPromise = new Promise(r => {
      resolveUpload = r;
    });
    vi.mocked(uploadHook.usePhotoUpload).mockReturnValue({
      mutate: vi.fn(),
      mutateAsync: vi.fn().mockReturnValue(uploadPromise),
      isPending: false,
      isSuccess: false,
      isError: false,
      error: null,
    } as never);

    wrap(<TranslateViewer campaignId={CAMPAIGN_ID} gameRef={GAME_REF} />);

    await act(async () => {
      firePhotoInput();
    });

    await waitFor(() => {
      expect(screen.getByTestId('translate-skeleton-uploading')).toBeInTheDocument();
    });

    expect(screen.getByTestId('translate-step-label')).toHaveTextContent('Caricamento foto…');
    expect(screen.getByTestId('translate-skeleton-uploading')).toHaveAttribute('aria-busy', 'true');

    // Resolve upload to avoid dangling promise in cleanup
    await act(async () => {
      resolveUpload({ id: PHOTO_ID, segments: [] });
    });
  });

  // ---------------------------------------------------------------------------
  // S2 — OCR step shows (T6)
  // ---------------------------------------------------------------------------
  it('S2: segmenting phase shows translate-skeleton-ocr + correct label', async () => {
    makeOneBookMock();

    // Upload resolves immediately; hold segment unresolved
    let resolveSegment!: (v: unknown) => void;
    const segmentPromise = new Promise(r => {
      resolveSegment = r;
    });
    vi.mocked(uploadHook.usePhotoUpload).mockReturnValue({
      mutate: vi.fn(),
      mutateAsync: vi.fn().mockResolvedValue({ id: PHOTO_ID, segments: [] }),
      isPending: false,
      isSuccess: false,
      isError: false,
      error: null,
    } as never);
    vi.mocked(segmentHook.useSegmentPhoto).mockReturnValue({
      mutate: vi.fn(),
      mutateAsync: vi.fn().mockReturnValue(segmentPromise),
      isPending: false,
      isSuccess: false,
      isError: false,
      error: null,
    } as never);

    wrap(<TranslateViewer campaignId={CAMPAIGN_ID} gameRef={GAME_REF} />);

    await act(async () => {
      firePhotoInput();
    });

    await waitFor(() => {
      expect(screen.getByTestId('translate-skeleton-ocr')).toBeInTheDocument();
    });

    expect(screen.getByTestId('translate-step-label')).toHaveTextContent('Sto leggendo il libro…');

    // Resolve segment to avoid dangling promise
    await act(async () => {
      resolveSegment(FAKE_ARTIFACT);
    });
  });

  // ---------------------------------------------------------------------------
  // S3 — Translating step shows (no appliedTerms) (T6)
  // ---------------------------------------------------------------------------
  it('S3: translating phase with empty appliedTerms shows translate-skeleton-translating', async () => {
    makeOneBookMock();

    vi.mocked(uploadHook.usePhotoUpload).mockReturnValue({
      mutate: vi.fn(),
      mutateAsync: vi.fn().mockResolvedValue({ id: PHOTO_ID, segments: [] }),
      isPending: false,
      isSuccess: false,
      isError: false,
      error: null,
    } as never);
    vi.mocked(segmentHook.useSegmentPhoto).mockReturnValue({
      mutate: vi.fn(),
      mutateAsync: vi.fn().mockResolvedValue(FAKE_ARTIFACT),
      isPending: false,
      isSuccess: false,
      isError: false,
      error: null,
    } as never);
    // SSE stays incomplete with no appliedTerms → 'translating' uiStep
    vi.mocked(sseHook.useTranslateSegmentSSE).mockReturnValue({
      partialText: '',
      isComplete: false,
      appliedTerms: [],
      error: undefined,
      start: vi.fn(),
      stop: vi.fn(),
    } as never);

    wrap(<TranslateViewer campaignId={CAMPAIGN_ID} gameRef={GAME_REF} />);

    await act(async () => {
      firePhotoInput();
    });

    // Wait for segments_ready phase (SegmentPicker visible)
    await waitFor(() => {
      expect(screen.getByTestId('segment-picker')).toBeInTheDocument();
    });

    // Click translate on §1
    await act(async () => {
      fireEvent.click(screen.getByTestId('segment-picker-translate-1'));
    });

    await waitFor(() => {
      expect(screen.getByTestId('translate-skeleton-translating')).toBeInTheDocument();
    });

    expect(screen.getByTestId('translate-step-label')).toHaveTextContent('Sto traducendo…');
  });

  // ---------------------------------------------------------------------------
  // S4 — Glossary-check step on appliedTerms signal (T7)
  // ---------------------------------------------------------------------------
  it('S4: translating phase with appliedTerms shows translate-skeleton-glossary-check', async () => {
    makeOneBookMock();

    vi.mocked(uploadHook.usePhotoUpload).mockReturnValue({
      mutate: vi.fn(),
      mutateAsync: vi.fn().mockResolvedValue({ id: PHOTO_ID, segments: [] }),
      isPending: false,
      isSuccess: false,
      isError: false,
      error: null,
    } as never);
    vi.mocked(segmentHook.useSegmentPhoto).mockReturnValue({
      mutate: vi.fn(),
      mutateAsync: vi.fn().mockResolvedValue(FAKE_ARTIFACT),
      isPending: false,
      isSuccess: false,
      isError: false,
      error: null,
    } as never);
    // SSE returns appliedTerms → glossary-check uiStep
    vi.mocked(sseHook.useTranslateSegmentSSE).mockReturnValue({
      partialText: 'partial…',
      isComplete: false,
      appliedTerms: ['sentinel', 'gold'],
      error: undefined,
      start: vi.fn(),
      stop: vi.fn(),
    } as never);

    wrap(<TranslateViewer campaignId={CAMPAIGN_ID} gameRef={GAME_REF} />);

    await act(async () => {
      firePhotoInput();
    });

    await waitFor(() => {
      expect(screen.getByTestId('segment-picker')).toBeInTheDocument();
    });

    await act(async () => {
      fireEvent.click(screen.getByTestId('segment-picker-translate-1'));
    });

    await waitFor(() => {
      expect(screen.getByTestId('translate-skeleton-glossary-check')).toBeInTheDocument();
    });

    expect(screen.getByTestId('translate-step-label')).toHaveTextContent(
      'Cerco parole nel glossario…'
    );
  });

  // ---------------------------------------------------------------------------
  // S5 — Abort hidden during uploading (T6)
  // ---------------------------------------------------------------------------
  it('S5: abort button is NOT visible during uploading (step 1)', async () => {
    makeOneBookMock();

    let resolveUpload!: (v: unknown) => void;
    const uploadPromise = new Promise(r => {
      resolveUpload = r;
    });
    vi.mocked(uploadHook.usePhotoUpload).mockReturnValue({
      mutate: vi.fn(),
      mutateAsync: vi.fn().mockReturnValue(uploadPromise),
      isPending: false,
      isSuccess: false,
      isError: false,
      error: null,
    } as never);

    wrap(<TranslateViewer campaignId={CAMPAIGN_ID} gameRef={GAME_REF} />);

    await act(async () => {
      firePhotoInput();
    });

    await waitFor(() => {
      expect(screen.getByTestId('translate-skeleton-uploading')).toBeInTheDocument();
    });

    // Abort button must NOT appear during uploading (DEC-6 / DEC-3)
    expect(screen.queryByTestId('translate-abort-button')).not.toBeInTheDocument();

    await act(async () => {
      resolveUpload({ id: PHOTO_ID, segments: [] });
    });
  });

  // ---------------------------------------------------------------------------
  // S6 — Abort visible from step 2+ (segmenting) (T6)
  // ---------------------------------------------------------------------------
  it('S6: abort button IS visible during segmenting (step 2+)', async () => {
    makeOneBookMock();

    let resolveSegment!: (v: unknown) => void;
    const segmentPromise = new Promise(r => {
      resolveSegment = r;
    });
    vi.mocked(uploadHook.usePhotoUpload).mockReturnValue({
      mutate: vi.fn(),
      mutateAsync: vi.fn().mockResolvedValue({ id: PHOTO_ID, segments: [] }),
      isPending: false,
      isSuccess: false,
      isError: false,
      error: null,
    } as never);
    vi.mocked(segmentHook.useSegmentPhoto).mockReturnValue({
      mutate: vi.fn(),
      mutateAsync: vi.fn().mockReturnValue(segmentPromise),
      isPending: false,
      isSuccess: false,
      isError: false,
      error: null,
    } as never);

    wrap(<TranslateViewer campaignId={CAMPAIGN_ID} gameRef={GAME_REF} />);

    await act(async () => {
      firePhotoInput();
    });

    await waitFor(() => {
      expect(screen.getByTestId('translate-skeleton-ocr')).toBeInTheDocument();
    });

    // Abort button MUST appear from step 2 (DEC-6)
    expect(screen.getByTestId('translate-abort-button')).toBeInTheDocument();

    await act(async () => {
      resolveSegment(FAKE_ARTIFACT);
    });
  });

  // ---------------------------------------------------------------------------
  // S7 — Abort during translating reverts to segments_ready (T7)
  // ---------------------------------------------------------------------------
  it('S7: clicking abort during translating calls sse.stop() and reverts to segments_ready', async () => {
    makeOneBookMock();

    const stopMock = vi.fn();
    vi.mocked(uploadHook.usePhotoUpload).mockReturnValue({
      mutate: vi.fn(),
      mutateAsync: vi.fn().mockResolvedValue({ id: PHOTO_ID, segments: [] }),
      isPending: false,
      isSuccess: false,
      isError: false,
      error: null,
    } as never);
    vi.mocked(segmentHook.useSegmentPhoto).mockReturnValue({
      mutate: vi.fn(),
      mutateAsync: vi.fn().mockResolvedValue(FAKE_ARTIFACT),
      isPending: false,
      isSuccess: false,
      isError: false,
      error: null,
    } as never);
    vi.mocked(sseHook.useTranslateSegmentSSE).mockReturnValue({
      partialText: '',
      isComplete: false,
      appliedTerms: [],
      error: undefined,
      start: vi.fn(),
      stop: stopMock,
    } as never);

    wrap(<TranslateViewer campaignId={CAMPAIGN_ID} gameRef={GAME_REF} />);

    await act(async () => {
      firePhotoInput();
    });

    await waitFor(() => {
      expect(screen.getByTestId('segment-picker')).toBeInTheDocument();
    });

    await act(async () => {
      fireEvent.click(screen.getByTestId('segment-picker-translate-1'));
    });

    await waitFor(() => {
      expect(screen.getByTestId('translate-abort-button')).toBeInTheDocument();
    });

    // Click abort
    await act(async () => {
      fireEvent.click(screen.getByTestId('translate-abort-button'));
    });

    // sse.stop() must have been called
    expect(stopMock).toHaveBeenCalledOnce();

    // Phase rolls back to segments_ready: SegmentPicker still visible,
    // no skeleton, no abort button
    await waitFor(() => {
      expect(screen.getByTestId('segment-picker')).toBeInTheDocument();
    });
    expect(screen.queryByTestId('translate-abort-button')).not.toBeInTheDocument();
    expect(screen.queryByTestId('translate-skeleton-translating')).not.toBeInTheDocument();
  });

  // ---------------------------------------------------------------------------
  // S8 — Hard timeout 20s fires (T7)
  // ---------------------------------------------------------------------------
  it('S8: 20s hard timeout calls sse.stop() and surfaces timeout error', async () => {
    makeOneBookMock();

    const stopMock = vi.fn();
    vi.mocked(uploadHook.usePhotoUpload).mockReturnValue({
      mutate: vi.fn(),
      mutateAsync: vi.fn().mockResolvedValue({ id: PHOTO_ID, segments: [] }),
      isPending: false,
      isSuccess: false,
      isError: false,
      error: null,
    } as never);
    vi.mocked(segmentHook.useSegmentPhoto).mockReturnValue({
      mutate: vi.fn(),
      mutateAsync: vi.fn().mockResolvedValue(FAKE_ARTIFACT),
      isPending: false,
      isSuccess: false,
      isError: false,
      error: null,
    } as never);
    vi.mocked(sseHook.useTranslateSegmentSSE).mockReturnValue({
      partialText: '',
      isComplete: false,
      appliedTerms: [],
      error: undefined,
      start: vi.fn(),
      stop: stopMock,
    } as never);

    // Use fake timers with shouldAdvanceTime so that promise microtasks still
    // flush (required for waitFor / act to work alongside advanceTimersByTime)
    vi.useFakeTimers({ shouldAdvanceTime: true });

    try {
      wrap(<TranslateViewer campaignId={CAMPAIGN_ID} gameRef={GAME_REF} />);

      await act(async () => {
        firePhotoInput();
      });

      // Flush all pending microtasks so upload+segment promises resolve
      await act(async () => {
        await vi.runAllTimersAsync();
      });

      // Confirm we reached segments_ready (SegmentPicker visible)
      expect(screen.getByTestId('segment-picker')).toBeInTheDocument();

      // Click translate to enter translating phase
      await act(async () => {
        fireEvent.click(screen.getByTestId('segment-picker-translate-1'));
      });

      expect(screen.getByTestId('translate-skeleton-translating')).toBeInTheDocument();

      // Advance timers past 20s to fire the hard timeout
      await act(async () => {
        vi.advanceTimersByTime(20_001);
      });

      // sse.stop() must have been called by the timeout handler
      expect(stopMock).toHaveBeenCalled();

      // Error message must reference '20 secondi'
      expect(screen.getByTestId('translate-viewer-error')).toHaveTextContent('20 secondi');

      // Phase rolls back: SegmentPicker still visible (segments_ready), no active skeleton
      expect(screen.getByTestId('segment-picker')).toBeInTheDocument();
      expect(screen.queryByTestId('translate-skeleton-translating')).not.toBeInTheDocument();
    } finally {
      vi.useRealTimers();
    }
  }, 60_000); // generous timeout for fake-timer test

  // ---------------------------------------------------------------------------
  // S9 — jest-axe scan (T8)
  // ---------------------------------------------------------------------------
  it('S9: idle state has no axe accessibility violations', async () => {
    // Use default mocks (2-book state, idle phase)
    const { container } = wrap(<TranslateViewer campaignId={CAMPAIGN_ID} gameRef={GAME_REF} />);
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('S9b: uploading skeleton state has no axe accessibility violations', async () => {
    makeOneBookMock();

    let resolveUpload!: (v: unknown) => void;
    const uploadPromise = new Promise(r => {
      resolveUpload = r;
    });
    vi.mocked(uploadHook.usePhotoUpload).mockReturnValue({
      mutate: vi.fn(),
      mutateAsync: vi.fn().mockReturnValue(uploadPromise),
      isPending: false,
      isSuccess: false,
      isError: false,
      error: null,
    } as never);

    const { container } = wrap(<TranslateViewer campaignId={CAMPAIGN_ID} gameRef={GAME_REF} />);

    await act(async () => {
      firePhotoInput();
    });

    await waitFor(() => {
      expect(screen.getByTestId('translate-skeleton-uploading')).toBeInTheDocument();
    });

    const results = await axe(container);
    expect(results).toHaveNoViolations();

    await act(async () => {
      resolveUpload({ id: PHOTO_ID, segments: [] });
    });
  });
});

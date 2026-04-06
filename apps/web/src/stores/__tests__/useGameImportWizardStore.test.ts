/**
 * Game Import Wizard Store Tests
 *
 * Test coverage:
 * - Initial state
 * - Navigation (goNext, goBack, canGoNext, canGoBack, setStep)
 * - Step validation (canGoNext per step)
 * - Data actions (setUploadedPdf, setReviewedMetadata, setCoverImage)
 * - Import execution (executeImport / submitWizard)
 * - Step 5 actions (setIndexingReady, addRagMessage, updateRagMessage)
 * - Reset functionality
 *
 * Target: ≥85% coverage
 */

import { renderHook, act } from '@testing-library/react';
import { describe, it, expect, beforeEach, vi } from 'vitest';

import {
  useGameImportWizardStore,
  type GameMetadata,
  type UploadedPdf,
  type CoverImageSelection,
} from '../useGameImportWizardStore';

// Mock toast (store uses toast.error / toast.warning)
vi.mock('@/components/layout', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
    warning: vi.fn(),
  },
}));

// Mock API client for executeImport
const mockImportGameFromPdf = vi.fn();
vi.mock('@/lib/api', () => ({
  api: {
    sharedGames: {
      importGameFromPdf: (...args: unknown[]) => mockImportGameFromPdf(...args),
    },
  },
}));

const mockUploadedPdf: UploadedPdf = {
  pdfDocumentId: 'pdf-123',
  id: 'pdf-123',
  fileName: 'rulebook.pdf',
};

const mockMetadata: GameMetadata = {
  title: 'Test Game',
  minPlayers: 2,
  maxPlayers: 4,
  playingTimeMinutes: 60,
  description: 'A test game',
  yearPublished: 2020,
  minAge: 10,
  publishers: ['Publisher A'],
  designers: ['Designer A'],
  categories: ['Strategy'],
  mechanics: ['Deck Building'],
};

const mockCoverImage: CoverImageSelection = {
  mode: 'pdf-page',
  imageUrl: 'https://example.com/cover.jpg',
};

beforeEach(() => {
  vi.clearAllMocks();

  // Reset store to initial state
  useGameImportWizardStore.setState({
    currentStep: 1,
    uploadedPdf: null,
    reviewedMetadata: null,
    coverImage: { mode: 'placeholder', imageUrl: null },
    importResult: null,
    ragTestHistory: [],
    isIndexingReady: false,
    isProcessing: false,
    error: null,
    extractedMetadata: null,
    selectedBggId: null,
    enrichedData: null,
    bggGameData: null,
  });

  // Default success response
  mockImportGameFromPdf.mockResolvedValue({
    gameId: 'game-new-123',
    pdfDocumentId: 'pdf-123',
    indexingStatus: 'pending',
    warning: null,
  });
});

describe('useGameImportWizardStore', () => {
  describe('Initial State', () => {
    it('initializes with step 1 and null data', () => {
      const { result } = renderHook(() => useGameImportWizardStore());

      expect(result.current.currentStep).toBe(1);
      expect(result.current.uploadedPdf).toBeNull();
      expect(result.current.reviewedMetadata).toBeNull();
      expect(result.current.importResult).toBeNull();
      expect(result.current.isProcessing).toBe(false);
      expect(result.current.error).toBeNull();
      expect(result.current.ragTestHistory).toHaveLength(0);
      expect(result.current.isIndexingReady).toBe(false);
    });
  });

  describe('Navigation', () => {
    it('goNext increments step when allowed', () => {
      const { result } = renderHook(() => useGameImportWizardStore());

      act(() => {
        result.current.setUploadedPdf(mockUploadedPdf);
      });

      expect(result.current.canGoNext()).toBe(true);

      act(() => {
        result.current.goNext();
      });

      expect(result.current.currentStep).toBe(2);
    });

    it('goNext does not increment past step 5', () => {
      const { result } = renderHook(() => useGameImportWizardStore());

      act(() => {
        result.current.setStep(5);
      });

      act(() => {
        result.current.goNext();
      });

      expect(result.current.currentStep).toBe(5);
    });

    it('goBack decrements step', () => {
      const { result } = renderHook(() => useGameImportWizardStore());

      act(() => {
        result.current.setStep(3);
      });

      act(() => {
        result.current.goBack();
      });

      expect(result.current.currentStep).toBe(2);
    });

    it('goBack does not decrement below step 1', () => {
      const { result } = renderHook(() => useGameImportWizardStore());

      expect(result.current.currentStep).toBe(1);

      act(() => {
        result.current.goBack();
      });

      expect(result.current.currentStep).toBe(1);
    });

    it('canGoBack returns true only for steps 2 and 3', () => {
      const { result } = renderHook(() => useGameImportWizardStore());

      // Step 1 — cannot go back
      expect(result.current.canGoBack()).toBe(false);

      act(() => result.current.setStep(2));
      expect(result.current.canGoBack()).toBe(true);

      act(() => result.current.setStep(3));
      expect(result.current.canGoBack()).toBe(true);

      // Step 4 (saga in progress) — locked
      act(() => result.current.setStep(4));
      expect(result.current.canGoBack()).toBe(false);

      // Step 5 — locked
      act(() => result.current.setStep(5));
      expect(result.current.canGoBack()).toBe(false);
    });

    it('setStep updates current step and clears error', () => {
      const { result } = renderHook(() => useGameImportWizardStore());

      act(() => {
        useGameImportWizardStore.setState({ error: 'Test error' });
      });

      act(() => {
        result.current.setStep(3);
      });

      expect(result.current.currentStep).toBe(3);
      expect(result.current.error).toBeNull();
    });
  });

  describe('Step Validation', () => {
    it('step 1: canGoNext false without PDF, true with PDF', () => {
      const { result } = renderHook(() => useGameImportWizardStore());

      expect(result.current.canGoNext()).toBe(false);

      act(() => {
        result.current.setUploadedPdf(mockUploadedPdf);
      });

      expect(result.current.canGoNext()).toBe(true);
    });

    it('step 2: canGoNext requires reviewed metadata with non-empty title', () => {
      const { result } = renderHook(() => useGameImportWizardStore());

      act(() => result.current.setStep(2));
      expect(result.current.canGoNext()).toBe(false);

      act(() => result.current.setReviewedMetadata(mockMetadata));
      expect(result.current.canGoNext()).toBe(true);
    });

    it('step 2: canGoNext false when title is blank', () => {
      const { result } = renderHook(() => useGameImportWizardStore());

      act(() => {
        result.current.setStep(2);
        result.current.setReviewedMetadata({ ...mockMetadata, title: '   ' });
      });

      expect(result.current.canGoNext()).toBe(false);
    });

    it('step 3: canGoNext requires reviewed metadata', () => {
      const { result } = renderHook(() => useGameImportWizardStore());

      act(() => result.current.setStep(3));
      expect(result.current.canGoNext()).toBe(false);

      act(() => result.current.setReviewedMetadata(mockMetadata));
      expect(result.current.canGoNext()).toBe(true);
    });

    it('step 4: canGoNext requires importResult', () => {
      const { result } = renderHook(() => useGameImportWizardStore());

      act(() => result.current.setStep(4));
      expect(result.current.canGoNext()).toBe(false);

      act(() => {
        useGameImportWizardStore.setState({
          importResult: { gameId: 'g1', pdfDocumentId: 'p1', indexingStatus: 'pending' },
        });
      });

      expect(result.current.canGoNext()).toBe(true);
    });

    it('step 5: canGoNext always false', () => {
      const { result } = renderHook(() => useGameImportWizardStore());

      act(() => result.current.setStep(5));
      expect(result.current.canGoNext()).toBe(false);
    });
  });

  describe('Data Actions', () => {
    it('setUploadedPdf updates state and clears error', () => {
      const { result } = renderHook(() => useGameImportWizardStore());

      act(() => {
        useGameImportWizardStore.setState({ error: 'Test error' });
      });

      act(() => {
        result.current.setUploadedPdf(mockUploadedPdf);
      });

      expect(result.current.uploadedPdf).toEqual(mockUploadedPdf);
      expect(result.current.error).toBeNull();
    });

    it('setReviewedMetadata updates reviewedMetadata and extractedMetadata (legacy)', () => {
      const { result } = renderHook(() => useGameImportWizardStore());

      act(() => {
        result.current.setReviewedMetadata(mockMetadata);
      });

      expect(result.current.reviewedMetadata).toEqual(mockMetadata);
      // Legacy alias
      expect(result.current.extractedMetadata).toEqual(mockMetadata);
      expect(result.current.error).toBeNull();
    });

    it('setCoverImage updates cover selection', () => {
      const { result } = renderHook(() => useGameImportWizardStore());

      act(() => {
        result.current.setCoverImage(mockCoverImage);
      });

      expect(result.current.coverImage).toEqual(mockCoverImage);
    });
  });

  describe('Import Execution', () => {
    it('executeImport calls API and sets importResult on success', async () => {
      const { result } = renderHook(() => useGameImportWizardStore());

      act(() => {
        result.current.setUploadedPdf(mockUploadedPdf);
        result.current.setReviewedMetadata(mockMetadata);
        result.current.setCoverImage(mockCoverImage);
      });

      await act(async () => {
        await result.current.executeImport();
      });

      expect(mockImportGameFromPdf).toHaveBeenCalledWith(
        expect.objectContaining({
          title: mockMetadata.title,
          pdfDocumentId: mockUploadedPdf.pdfDocumentId,
        })
      );

      expect(result.current.importResult).not.toBeNull();
      expect(result.current.importResult?.gameId).toBe('game-new-123');
      expect(result.current.isProcessing).toBe(false);
      expect(result.current.error).toBeNull();
    });

    it('executeImport sets error and returns early when no PDF', async () => {
      const { result } = renderHook(() => useGameImportWizardStore());

      act(() => {
        result.current.setReviewedMetadata(mockMetadata);
      });

      await act(async () => {
        await result.current.executeImport();
      });

      expect(mockImportGameFromPdf).not.toHaveBeenCalled();
      expect(result.current.error).toBeTruthy();
      expect(result.current.isProcessing).toBe(false);
    });

    it('executeImport sets error and returns early when title is empty', async () => {
      const { result } = renderHook(() => useGameImportWizardStore());

      act(() => {
        result.current.setUploadedPdf(mockUploadedPdf);
        result.current.setReviewedMetadata({ ...mockMetadata, title: '' });
      });

      await act(async () => {
        await result.current.executeImport();
      });

      expect(mockImportGameFromPdf).not.toHaveBeenCalled();
      expect(result.current.error).toBeTruthy();
    });

    it('executeImport sets error on API failure', async () => {
      const { result } = renderHook(() => useGameImportWizardStore());

      mockImportGameFromPdf.mockRejectedValue(new Error('Internal Server Error'));

      act(() => {
        result.current.setUploadedPdf(mockUploadedPdf);
        result.current.setReviewedMetadata(mockMetadata);
      });

      // Store rethrows — catch inside act() so state updates are flushed before assertions
      await act(async () => {
        try {
          await result.current.executeImport();
        } catch {
          // expected — store rethrows API errors
        }
      });

      expect(result.current.error).toBe('Internal Server Error');
      expect(result.current.isProcessing).toBe(false);
    });

    it('submitWizard delegates to executeImport', async () => {
      const { result } = renderHook(() => useGameImportWizardStore());

      act(() => {
        result.current.setUploadedPdf(mockUploadedPdf);
        result.current.setReviewedMetadata(mockMetadata);
      });

      await act(async () => {
        await result.current.submitWizard();
      });

      expect(mockImportGameFromPdf).toHaveBeenCalled();
    });
  });

  describe('Step 5 Actions', () => {
    it('setIndexingReady updates isIndexingReady', () => {
      const { result } = renderHook(() => useGameImportWizardStore());

      act(() => {
        result.current.setIndexingReady(true);
      });

      expect(result.current.isIndexingReady).toBe(true);
    });

    it('addRagMessage appends a pending message and returns its id', () => {
      const { result } = renderHook(() => useGameImportWizardStore());

      let msgId = '';
      act(() => {
        msgId = result.current.addRagMessage('Quante carte ci sono?');
      });

      expect(msgId).toBeTruthy();
      expect(result.current.ragTestHistory).toHaveLength(1);
      expect(result.current.ragTestHistory[0].question).toBe('Quante carte ci sono?');
      expect(result.current.ragTestHistory[0].isLoading).toBe(true);
    });

    it('updateRagMessage updates the correct message', () => {
      const { result } = renderHook(() => useGameImportWizardStore());

      let msgId = '';
      act(() => {
        msgId = result.current.addRagMessage('Test question');
      });

      act(() => {
        result.current.updateRagMessage(msgId, { answer: 'Test answer', isLoading: false });
      });

      const msg = result.current.ragTestHistory.find(m => m.id === msgId);
      expect(msg?.answer).toBe('Test answer');
      expect(msg?.isLoading).toBe(false);
    });
  });

  describe('Reset', () => {
    it('reset clears all wizard data and returns to step 1', () => {
      const { result } = renderHook(() => useGameImportWizardStore());

      act(() => {
        result.current.setUploadedPdf(mockUploadedPdf);
        result.current.setReviewedMetadata(mockMetadata);
        result.current.setCoverImage(mockCoverImage);
        result.current.setStep(3);
        useGameImportWizardStore.setState({ error: 'Test error' });
      });

      act(() => {
        result.current.reset();
      });

      expect(result.current.currentStep).toBe(1);
      expect(result.current.uploadedPdf).toBeNull();
      expect(result.current.reviewedMetadata).toBeNull();
      expect(result.current.importResult).toBeNull();
      expect(result.current.isProcessing).toBe(false);
      expect(result.current.error).toBeNull();
      expect(result.current.ragTestHistory).toHaveLength(0);
      expect(result.current.isIndexingReady).toBe(false);
    });
  });

  describe('Legacy compat', () => {
    it('setSelectedBggId is a no-op (BGG flow removed)', () => {
      const { result } = renderHook(() => useGameImportWizardStore());

      act(() => {
        result.current.setSelectedBggId(12345);
      });

      expect(result.current.selectedBggId).toBeNull();
    });
  });
});

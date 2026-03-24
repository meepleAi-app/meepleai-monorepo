/**
 * Game Import Wizard Store Tests - Issue #4161
 *
 * Test coverage:
 * - Initial state
 * - Navigation (goNext, goBack, canGoNext, canGoBack)
 * - Data actions (setters)
 * - Step validation
 * - Wizard submission
 * - Reset functionality
 *
 * Target: ≥85% coverage
 */

import { renderHook, act } from '@testing-library/react';
import { describe, it, expect, beforeEach, vi } from 'vitest';

import {
  useGameImportWizardStore,
  type ExtractedMetadata,
  type BggGameData,
  type EnrichedGameData,
  type UploadedPdf,
} from '../useGameImportWizardStore';

// Mock toast
vi.mock('@/components/layout', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

// Mock API client for submitWizard
const mockWizardCreateGame = vi.fn();
vi.mock('@/lib/api', () => ({
  api: {
    sharedGames: {
      wizardCreateGame: (...args: unknown[]) => mockWizardCreateGame(...args),
    },
  },
}));

const mockUploadedPdf: UploadedPdf = {
  id: 'pdf-123',
  fileName: 'rulebook.pdf',
};

const mockExtractedMetadata: ExtractedMetadata = {
  title: 'Test Game',
  minPlayers: 2,
  maxPlayers: 4,
  playingTime: 60,
  description: 'A test game',
};

const mockBggGameData: BggGameData = {
  bggId: 12345,
  name: 'Test Game from BGG',
  yearPublished: 2020,
  minPlayers: 2,
  maxPlayers: 5,
  playingTime: 90,
  minAge: 10,
  description: 'BGG description',
  imageUrl: 'https://example.com/image.jpg',
  thumbnailUrl: 'https://example.com/thumb.jpg',
};

const mockEnrichedData: EnrichedGameData = {
  title: 'Test Game (Final)',
  minPlayers: 2,
  maxPlayers: 4,
  playingTime: 60,
  description: 'Final description',
  bggId: 12345,
  imageUrl: 'https://example.com/image.jpg',
  year: 2020,
  minAge: 10,
};

beforeEach(() => {
  vi.clearAllMocks();

  // Reset Zustand store state to initial
  useGameImportWizardStore.setState({
    currentStep: 1,
    uploadedPdf: null,
    extractedMetadata: null,
    selectedBggId: null,
    bggGameData: null,
    enrichedData: null,
    isProcessing: false,
    error: null,
  });

  // Reset API mock
  mockWizardCreateGame.mockResolvedValue({
    gameId: 'game-new-123',
    approvalStatus: 'Published',
    bggEnrichmentApplied: false,
  });
});

describe('useGameImportWizardStore', () => {
  describe('Initial State', () => {
    it('initializes with step 1 and null data', () => {
      const { result } = renderHook(() => useGameImportWizardStore());

      expect(result.current.currentStep).toBe(1);
      expect(result.current.uploadedPdf).toBeNull();
      expect(result.current.extractedMetadata).toBeNull();
      expect(result.current.selectedBggId).toBeNull();
      expect(result.current.bggGameData).toBeNull();
      expect(result.current.enrichedData).toBeNull();
      expect(result.current.isProcessing).toBe(false);
      expect(result.current.error).toBeNull();
    });
  });

  describe('Navigation', () => {
    it('goNext increments step when allowed', () => {
      const { result } = renderHook(() => useGameImportWizardStore());

      // Set PDF to allow step 1 → 2
      act(() => {
        result.current.setUploadedPdf(mockUploadedPdf);
      });

      expect(result.current.canGoNext()).toBe(true);

      act(() => {
        result.current.goNext();
      });

      expect(result.current.currentStep).toBe(2);
    });

    it('goNext does not increment past step 4', () => {
      const { result } = renderHook(() => useGameImportWizardStore());

      act(() => {
        result.current.setStep(4);
      });

      act(() => {
        result.current.goNext();
      });

      expect(result.current.currentStep).toBe(4);
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

    it('canGoBack returns true except on step 1', () => {
      const { result } = renderHook(() => useGameImportWizardStore());

      expect(result.current.canGoBack()).toBe(false);

      act(() => {
        result.current.setStep(2);
      });

      expect(result.current.canGoBack()).toBe(true);
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
    it('step 1 requires uploaded PDF', () => {
      const { result } = renderHook(() => useGameImportWizardStore());

      expect(result.current.canGoNext()).toBe(false);

      act(() => {
        result.current.setUploadedPdf(mockUploadedPdf);
      });

      expect(result.current.canGoNext()).toBe(true);
    });

    it('step 2 requires extracted metadata', () => {
      const { result } = renderHook(() => useGameImportWizardStore());

      act(() => {
        result.current.setStep(2);
      });

      expect(result.current.canGoNext()).toBe(false);

      act(() => {
        result.current.setExtractedMetadata(mockExtractedMetadata);
      });

      expect(result.current.canGoNext()).toBe(true);
    });

    it('step 3 requires selected BGG ID', () => {
      const { result } = renderHook(() => useGameImportWizardStore());

      act(() => {
        result.current.setStep(3);
      });

      expect(result.current.canGoNext()).toBe(false);

      act(() => {
        result.current.setSelectedBggId(12345, mockBggGameData);
      });

      expect(result.current.canGoNext()).toBe(true);
    });

    it('step 4 cannot go next (final step)', () => {
      const { result } = renderHook(() => useGameImportWizardStore());

      act(() => {
        result.current.setStep(4);
      });

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

    it('setExtractedMetadata updates state and clears error', () => {
      const { result } = renderHook(() => useGameImportWizardStore());

      act(() => {
        result.current.setExtractedMetadata(mockExtractedMetadata);
      });

      expect(result.current.extractedMetadata).toEqual(mockExtractedMetadata);
      expect(result.current.error).toBeNull();
    });

    it('setSelectedBggId updates both ID and data', () => {
      const { result } = renderHook(() => useGameImportWizardStore());

      act(() => {
        result.current.setSelectedBggId(12345, mockBggGameData);
      });

      expect(result.current.selectedBggId).toBe(12345);
      expect(result.current.bggGameData).toEqual(mockBggGameData);
      expect(result.current.error).toBeNull();
    });

    it('setSelectedBggId works with ID only (no data)', () => {
      const { result } = renderHook(() => useGameImportWizardStore());

      act(() => {
        result.current.setSelectedBggId(12345);
      });

      expect(result.current.selectedBggId).toBe(12345);
      expect(result.current.bggGameData).toBeNull();
    });

    it('resolveConflicts updates enriched data', () => {
      const { result } = renderHook(() => useGameImportWizardStore());

      act(() => {
        result.current.resolveConflicts(mockEnrichedData);
      });

      expect(result.current.enrichedData).toEqual(mockEnrichedData);
      expect(result.current.error).toBeNull();
    });
  });

  describe('Wizard Submission', () => {
    it('submitWizard succeeds with valid data', async () => {
      const { result } = renderHook(() => useGameImportWizardStore());

      // Setup complete wizard state
      act(() => {
        result.current.setUploadedPdf(mockUploadedPdf);
        result.current.setSelectedBggId(12345, mockBggGameData);
        result.current.resolveConflicts(mockEnrichedData);
      });

      await act(async () => {
        await result.current.submitWizard();
      });

      expect(mockWizardCreateGame).toHaveBeenCalledWith({
        pdfDocumentId: mockUploadedPdf.id,
        extractedTitle: mockEnrichedData.title,
        minPlayers: mockEnrichedData.minPlayers,
        maxPlayers: mockEnrichedData.maxPlayers,
        playingTimeMinutes: mockEnrichedData.playingTime,
        minAge: mockEnrichedData.minAge,
        selectedBggId: 12345,
      });

      // State should reset after successful submission
      expect(result.current.currentStep).toBe(1);
      expect(result.current.uploadedPdf).toBeNull();
      expect(result.current.enrichedData).toBeNull();
    });

    it('submitWizard fails without PDF', async () => {
      const { result } = renderHook(() => useGameImportWizardStore());

      // Only set enriched data, no PDF
      act(() => {
        result.current.resolveConflicts(mockEnrichedData);
      });

      await expect(
        act(async () => {
          await result.current.submitWizard();
        })
      ).rejects.toThrow('No PDF uploaded');

      expect(result.current.error).toBe('No PDF uploaded');
      expect(result.current.isProcessing).toBe(false);
    });

    it('submitWizard fails without enriched data', async () => {
      const { result } = renderHook(() => useGameImportWizardStore());

      // Only set PDF, no enriched data
      act(() => {
        result.current.setUploadedPdf(mockUploadedPdf);
      });

      await expect(
        act(async () => {
          await result.current.submitWizard();
        })
      ).rejects.toThrow('Game title is required');

      expect(result.current.error).toBe('Game title is required');
    });

    it('submitWizard fails with empty title', async () => {
      const { result } = renderHook(() => useGameImportWizardStore());

      const invalidData = { ...mockEnrichedData, title: '   ' };

      act(() => {
        result.current.setUploadedPdf(mockUploadedPdf);
        result.current.resolveConflicts(invalidData);
      });

      await expect(
        act(async () => {
          await result.current.submitWizard();
        })
      ).rejects.toThrow('Game title is required');
    });

    it('submitWizard handles API errors', async () => {
      const { result } = renderHook(() => useGameImportWizardStore());

      // Mock API failure
      mockWizardCreateGame.mockRejectedValue(new Error('Internal Server Error'));

      act(() => {
        result.current.setUploadedPdf(mockUploadedPdf);
        result.current.resolveConflicts(mockEnrichedData);
      });

      let errorThrown = false;
      try {
        await act(async () => {
          await result.current.submitWizard();
        });
      } catch (err) {
        errorThrown = true;
        expect(err).toBeInstanceOf(Error);
        expect((err as Error).message).toBe('Internal Server Error');
      }

      expect(errorThrown).toBe(true);
      expect(result.current.error).toBe('Internal Server Error');
      expect(result.current.isProcessing).toBe(false);
    });

    it('submitWizard sets processing state during submission', async () => {
      const { result } = renderHook(() => useGameImportWizardStore());

      act(() => {
        result.current.setUploadedPdf(mockUploadedPdf);
        result.current.resolveConflicts(mockEnrichedData);
      });

      // Create a promise that we can control
      let resolveSubmit: (value: unknown) => void;
      const submitPromise = new Promise(resolve => {
        resolveSubmit = resolve;
      });

      mockWizardCreateGame.mockImplementation(async () => {
        await submitPromise;
        return {
          gameId: 'game-new-123',
          approvalStatus: 'Published',
          bggEnrichmentApplied: false,
        };
      });

      // Start submission
      const submissionPromise = act(async () => {
        await result.current.submitWizard();
      });

      // Resolve the submission
      resolveSubmit!(undefined);
      await submissionPromise;

      expect(result.current.isProcessing).toBe(false);
    });
  });

  describe('Reset', () => {
    it('reset clears all wizard data and returns to step 1', () => {
      const { result } = renderHook(() => useGameImportWizardStore());

      // Set some state
      act(() => {
        result.current.setUploadedPdf(mockUploadedPdf);
        result.current.setExtractedMetadata(mockExtractedMetadata);
        result.current.setSelectedBggId(12345, mockBggGameData);
        result.current.resolveConflicts(mockEnrichedData);
        result.current.setStep(4);
        useGameImportWizardStore.setState({ error: 'Test error' });
      });

      // Reset
      act(() => {
        result.current.reset();
      });

      // All should be back to initial state
      expect(result.current.currentStep).toBe(1);
      expect(result.current.uploadedPdf).toBeNull();
      expect(result.current.extractedMetadata).toBeNull();
      expect(result.current.selectedBggId).toBeNull();
      expect(result.current.bggGameData).toBeNull();
      expect(result.current.enrichedData).toBeNull();
      expect(result.current.isProcessing).toBe(false);
      expect(result.current.error).toBeNull();
    });
  });
});

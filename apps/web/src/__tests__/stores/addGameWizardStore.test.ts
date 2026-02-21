/**
 * Add Game Wizard Store Tests
 * Issue #3477, #3650: Unit tests for wizard state management
 */

import { describe, it, expect, beforeEach, vi } from 'vitest';

import { toast } from '@/components/layout';
import { api } from '@/lib/api';
import {
  useAddGameWizardStore,
  type CustomGameData,
} from '@/stores/addGameWizardStore';
import type { Game } from '@/types/domain';

// Mock toast
vi.mock('@/components/layout', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
    info: vi.fn(),
    warning: vi.fn(),
  },
}));

// Mock API client
vi.mock('@/lib/api', () => ({
  api: {
    library: {
      addGame: vi.fn(),
    },
  },
}));

describe('addGameWizardStore', () => {
  beforeEach(() => {
    // Reset store before each test
    useAddGameWizardStore.getState().reset();
    vi.clearAllMocks();
  });

  describe('Initial State', () => {
    it('starts at step 1', () => {
      const { step } = useAddGameWizardStore.getState();
      expect(step).toBe(1);
    });

    it('has no selected game initially', () => {
      const { selectedGame, isCustomGame } = useAddGameWizardStore.getState();
      expect(selectedGame).toBeNull();
      expect(isCustomGame).toBe(false);
    });

    it('has no uploaded PDF initially', () => {
      const { uploadedPdfId, uploadedPdfName } = useAddGameWizardStore.getState();
      expect(uploadedPdfId).toBeNull();
      expect(uploadedPdfName).toBeNull();
    });

    it('is not processing initially', () => {
      const { isProcessing, error } = useAddGameWizardStore.getState();
      expect(isProcessing).toBe(false);
      expect(error).toBeNull();
    });
  });

  describe('Navigation', () => {
    it('setStep changes current step', () => {
      const { setStep } = useAddGameWizardStore.getState();

      setStep(2);
      expect(useAddGameWizardStore.getState().step).toBe(2);

      setStep(4);
      expect(useAddGameWizardStore.getState().step).toBe(4);
    });

    it('goNext advances step normally', () => {
      const { setStep, selectCustomGame, goNext } = useAddGameWizardStore.getState();

      // Start at step 1, select custom game (so Step 2 is not skipped)
      selectCustomGame();
      goNext();
      expect(useAddGameWizardStore.getState().step).toBe(2);
    });

    it('goNext skips Step 2 when shared game selected', () => {
      const mockGame: Game = { id: '1', title: 'Test Game', createdAt: '2024-01-01' };
      const { selectSharedGame, goNext } = useAddGameWizardStore.getState();

      selectSharedGame(mockGame);
      goNext();
      // Should skip from Step 1 to Step 3
      expect(useAddGameWizardStore.getState().step).toBe(3);
    });

    it('goBack decrements step normally', () => {
      const { setStep, selectCustomGame, goBack } = useAddGameWizardStore.getState();

      // Start at step 3 with custom game (so Step 2 is not skipped)
      selectCustomGame();
      setStep(3);
      goBack();
      expect(useAddGameWizardStore.getState().step).toBe(2);
    });

    it('goBack skips Step 2 when shared game selected', () => {
      const mockGame: Game = { id: '1', title: 'Test Game', createdAt: '2024-01-01' };
      const { selectSharedGame, setStep, goBack } = useAddGameWizardStore.getState();

      selectSharedGame(mockGame);
      setStep(3);
      goBack();
      // Should skip from Step 3 back to Step 1
      expect(useAddGameWizardStore.getState().step).toBe(1);
    });

    it('canGoNext returns false on Step 1 with no game', () => {
      const { canGoNext } = useAddGameWizardStore.getState();
      expect(canGoNext()).toBe(false);
    });

    it('canGoNext returns true on Step 1 with shared game', () => {
      const mockGame: Game = { id: '1', title: 'Test Game', createdAt: '2024-01-01' };
      const { selectSharedGame, canGoNext } = useAddGameWizardStore.getState();

      selectSharedGame(mockGame);
      expect(canGoNext()).toBe(true);
    });

    it('canGoNext returns false on Step 2 with empty name', () => {
      const { selectCustomGame, setStep, canGoNext } = useAddGameWizardStore.getState();

      selectCustomGame();
      setStep(2);
      expect(canGoNext()).toBe(false);
    });

    it('canGoNext returns true on Step 2 with valid name', () => {
      const customData: CustomGameData = { name: 'My Custom Game' };
      const { selectCustomGame, setCustomGameData, setStep, canGoNext } =
        useAddGameWizardStore.getState();

      selectCustomGame();
      setCustomGameData(customData);
      setStep(2);
      expect(canGoNext()).toBe(true);
    });

    it('canGoNext returns true on Step 3 (PDF optional)', () => {
      const { setStep, canGoNext } = useAddGameWizardStore.getState();

      setStep(3);
      expect(canGoNext()).toBe(true);
    });

    it('canGoNext returns false on Step 4 (review step)', () => {
      const { setStep, canGoNext } = useAddGameWizardStore.getState();

      setStep(4);
      expect(canGoNext()).toBe(false);
    });

    it('canGoBack returns false on Step 1', () => {
      const { canGoBack } = useAddGameWizardStore.getState();
      expect(canGoBack()).toBe(false);
    });

    it('canGoBack returns true on Steps 2-4', () => {
      const { setStep, canGoBack } = useAddGameWizardStore.getState();

      setStep(2);
      expect(canGoBack()).toBe(true);

      setStep(3);
      expect(canGoBack()).toBe(true);

      setStep(4);
      expect(canGoBack()).toBe(true);
    });
  });

  describe('Game Selection', () => {
    it('selectSharedGame sets selected game and clears custom flag', () => {
      const mockGame: Game = { id: '1', title: 'Catan', createdAt: '2024-01-01' };
      const { selectSharedGame } = useAddGameWizardStore.getState();

      selectSharedGame(mockGame);

      const { selectedGame, isCustomGame, customGameData, error } =
        useAddGameWizardStore.getState();
      expect(selectedGame).toEqual(mockGame);
      expect(isCustomGame).toBe(false);
      expect(customGameData).toBeNull();
      expect(error).toBeNull();
    });

    it('selectCustomGame sets custom flag and clears selected game', () => {
      const mockGame: Game = { id: '1', title: 'Catan', createdAt: '2024-01-01' };
      const { selectSharedGame, selectCustomGame } = useAddGameWizardStore.getState();

      // First select a shared game
      selectSharedGame(mockGame);
      // Then switch to custom
      selectCustomGame();

      const { selectedGame, isCustomGame, customGameData, error } =
        useAddGameWizardStore.getState();
      expect(selectedGame).toBeNull();
      expect(isCustomGame).toBe(true);
      expect(customGameData).toEqual({ name: '' });
      expect(error).toBeNull();
    });

    it('setCustomGameData updates custom game data', () => {
      const customData: CustomGameData = {
        name: 'My Custom Game',
        minPlayers: 2,
        maxPlayers: 4,
        playTime: 60,
        complexity: 3,
      };
      const { setCustomGameData } = useAddGameWizardStore.getState();

      setCustomGameData(customData);

      const { customGameData, error } = useAddGameWizardStore.getState();
      expect(customGameData).toEqual(customData);
      expect(error).toBeNull();
    });
  });

  describe('PDF Upload', () => {
    it('setUploadedPdf stores PDF ID and name', () => {
      const { setUploadedPdf } = useAddGameWizardStore.getState();

      setUploadedPdf('pdf-123', 'rulebook.pdf');

      const { uploadedPdfId, uploadedPdfName, error } = useAddGameWizardStore.getState();
      expect(uploadedPdfId).toBe('pdf-123');
      expect(uploadedPdfName).toBe('rulebook.pdf');
      expect(error).toBeNull();
    });

    it('clearPdf removes PDF data', () => {
      const { setUploadedPdf, clearPdf } = useAddGameWizardStore.getState();

      setUploadedPdf('pdf-123', 'rulebook.pdf');
      clearPdf();

      const { uploadedPdfId, uploadedPdfName } = useAddGameWizardStore.getState();
      expect(uploadedPdfId).toBeNull();
      expect(uploadedPdfName).toBeNull();
    });
  });

  describe('Wizard Submission', () => {
    it('submitWizard fails with no game selected', async () => {
      const { submitWizard } = useAddGameWizardStore.getState();

      await submitWizard();

      const { isProcessing, error } = useAddGameWizardStore.getState();
      expect(isProcessing).toBe(false);
      expect(error).toBe('No game selected');
      expect(toast.error).toHaveBeenCalledWith('No game selected');
    });

    it('submitWizard fails with custom game but no name', async () => {
      const { selectCustomGame, submitWizard } = useAddGameWizardStore.getState();

      selectCustomGame(); // Has empty name by default
      await submitWizard();

      const { isProcessing, error } = useAddGameWizardStore.getState();
      expect(isProcessing).toBe(false);
      expect(error).toBe('Custom game requires a name');
      expect(toast.error).toHaveBeenCalledWith('Custom game requires a name');
    });

    it('submitWizard succeeds with shared game', async () => {
      const mockGame: Game = { id: '1', title: 'Catan', createdAt: '2024-01-01' };
      const { selectSharedGame, setStep, submitWizard } = useAddGameWizardStore.getState();

      // Mock window.location.href
      delete (window as { location?: unknown }).location;
      window.location = { href: '' } as Location;

      // Mock successful API call
      vi.mocked(api.library.addGame).mockResolvedValueOnce({
        id: 'entry-uuid',
        userId: 'user-uuid',
        gameId: '1',
        gameTitle: 'Catan',
        gamePublisher: null,
        gameYearPublished: null,
        gameIconUrl: null,
        gameImageUrl: null,
        addedAt: '2024-01-01T00:00:00Z',
        notes: null,
        isFavorite: false,
        currentState: 'Owned',
        stateChangedAt: null,
        stateNotes: null,
        hasKb: false,
        kbCardCount: 0,
        kbIndexedCount: 0,
        kbProcessingCount: 0,
        agentIsOwned: true,
      });

      selectSharedGame(mockGame);
      setStep(4);
      await submitWizard();

      // Should call API
      expect(api.library.addGame).toHaveBeenCalledWith('1', {
        notes: null,
        isFavorite: false,
      });

      // Should show success toast
      expect(toast.success).toHaveBeenCalledWith('"Catan" added to your collection!');

      // Should reset store
      const { step, selectedGame } = useAddGameWizardStore.getState();
      expect(step).toBe(1);
      expect(selectedGame).toBeNull();

      // Should redirect
      expect(window.location.href).toBe('/dashboard/collection');
    });

    it('submitWizard shows info message for custom games (backend not implemented)', async () => {
      const customData: CustomGameData = { name: 'My Custom Game' };
      const {
        selectCustomGame,
        setCustomGameData,
        setStep,
        submitWizard,
      } = useAddGameWizardStore.getState();

      selectCustomGame();
      setCustomGameData(customData);
      setStep(4);
      await submitWizard();

      // Should show info toast (custom games not yet supported by backend)
      expect(toast.info).toHaveBeenCalledWith('Custom game support coming soon! For now, search for games in the catalog.');

      // Should NOT reset store (operation didn't complete)
      const { isCustomGame } = useAddGameWizardStore.getState();
      expect(isCustomGame).toBe(true);
    });

    it('submitWizard handles API errors gracefully', async () => {
      const mockGame: Game = { id: '1', title: 'Catan', createdAt: '2024-01-01' };
      const { selectSharedGame, setStep, submitWizard } = useAddGameWizardStore.getState();

      // Mock API failure
      vi.mocked(api.library.addGame).mockRejectedValueOnce(new Error('Network error'));

      selectSharedGame(mockGame);
      setStep(4);
      await submitWizard();

      // Should show error toast
      expect(toast.error).toHaveBeenCalledWith('Network error');

      // Should set error state
      const { error, isProcessing } = useAddGameWizardStore.getState();
      expect(error).toBe('Network error');
      expect(isProcessing).toBe(false);
    });

    it('submitWizard handles PDF association when uploaded', async () => {
      const mockGame: Game = { id: '1', title: 'Catan', createdAt: '2024-01-01' };
      const {
        selectSharedGame,
        setUploadedPdf,
        setStep,
        submitWizard,
      } = useAddGameWizardStore.getState();

      // Mock window.location.href
      delete (window as { location?: unknown }).location;
      window.location = { href: '' } as Location;

      // Mock successful API calls
      vi.mocked(api.library.addGame).mockResolvedValueOnce({
        id: 'entry-uuid',
        userId: 'user-uuid',
        gameId: '1',
        gameTitle: 'Catan',
        gamePublisher: null,
        gameYearPublished: null,
        gameIconUrl: null,
        gameImageUrl: null,
        addedAt: '2024-01-01T00:00:00Z',
        notes: null,
        isFavorite: false,
        currentState: 'Owned',
        stateChangedAt: null,
        stateNotes: null,
        hasKb: false,
        kbCardCount: 0,
        kbIndexedCount: 0,
        kbProcessingCount: 0,
        agentIsOwned: true,
      });

      // Mock fetch for PDF association
      global.fetch = vi.fn().mockResolvedValueOnce({
        ok: true,
        json: async () => ({}),
      });

      selectSharedGame(mockGame);
      setUploadedPdf('pdf-123', 'rulebook.pdf');
      setStep(4);
      await submitWizard();

      // Should succeed (PDF association attempted)
      expect(toast.success).toHaveBeenCalledWith('"Catan" added to your collection!');
    });
  });

  describe('Reset', () => {
    it('reset clears all state', () => {
      const mockGame: Game = { id: '1', title: 'Catan', createdAt: '2024-01-01' };
      const { selectSharedGame, setUploadedPdf, setStep, reset } =
        useAddGameWizardStore.getState();

      // Set some state
      selectSharedGame(mockGame);
      setUploadedPdf('pdf-123', 'rulebook.pdf');
      setStep(4);

      // Reset
      reset();

      // Verify all cleared
      const state = useAddGameWizardStore.getState();
      expect(state.step).toBe(1);
      expect(state.selectedGame).toBeNull();
      expect(state.isCustomGame).toBe(false);
      expect(state.customGameData).toBeNull();
      expect(state.uploadedPdfId).toBeNull();
      expect(state.uploadedPdfName).toBeNull();
      expect(state.isProcessing).toBe(false);
      expect(state.error).toBeNull();
    });
  });
});

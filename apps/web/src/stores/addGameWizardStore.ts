/**
 * Add Game to Collection Wizard Store
 * Issue #3477: Multi-step wizard for adding games to user's personal collection
 *
 * Zustand store for wizard state management with devtools support.
 * Follows pattern from game-state-store.ts for consistency.
 */

import { create } from 'zustand';
import { devtools } from 'zustand/middleware';

import { toast } from '@/components/layout';
import type { Game } from '@/types/domain';

/**
 * Custom game data (when user creates a game not in SharedGameCatalog)
 */
export interface CustomGameData {
  name: string;
  minPlayers?: number;
  maxPlayers?: number;
  playTime?: number; // Minutes
  complexity?: number; // 1-5 scale
}

/**
 * Wizard step type (1-4, step 2 conditional on custom game)
 */
export type WizardStep = 1 | 2 | 3 | 4;

/**
 * Wizard state interface
 */
export interface AddGameWizardState {
  // Current step (1: Search/Select, 2: Custom Details, 3: Upload PDF, 4: Review)
  step: WizardStep;

  // Step 1: Game selection
  selectedGame: Game | null;
  isCustomGame: boolean;

  // Step 2: Custom game details (only if isCustomGame)
  customGameData: CustomGameData | null;

  // Step 3: Optional PDF upload
  uploadedPdfId: string | null;
  uploadedPdfName: string | null;

  // UI state
  isProcessing: boolean;
  error: string | null;

  // Actions: Navigation
  setStep: (step: WizardStep) => void;
  goNext: () => void;
  goBack: () => void;
  canGoNext: () => boolean;
  canGoBack: () => boolean;

  // Actions: Data
  selectSharedGame: (game: Game) => void;
  selectCustomGame: () => void;
  setCustomGameData: (data: CustomGameData) => void;
  setUploadedPdf: (pdfId: string, pdfName: string) => void;
  clearPdf: () => void;

  // Actions: Submission
  submitWizard: () => Promise<void>;

  // Actions: Reset
  reset: () => void;
}

const initialState = {
  step: 1 as WizardStep,
  selectedGame: null,
  isCustomGame: false,
  customGameData: null,
  uploadedPdfId: null,
  uploadedPdfName: null,
  isProcessing: false,
  error: null,
};

export const useAddGameWizardStore = create<AddGameWizardState>()(
  devtools(
    (set, get) => ({
      ...initialState,

      // NAVIGATION ACTIONS

      setStep: step => {
        set({ step, error: null });
      },

      goNext: () => {
        const current = get().step;
        const isCustom = get().isCustomGame;

        // Step 1 → Skip Step 2 if not custom, go to 3
        if (current === 1 && !isCustom) {
          set({ step: 3 });
          return;
        }

        // Otherwise, normal increment
        if (current < 4) {
          set({ step: (current + 1) as WizardStep });
        }
      },

      goBack: () => {
        const current = get().step;
        const isCustom = get().isCustomGame;

        // Step 3 → Skip back to Step 1 if not custom
        if (current === 3 && !isCustom) {
          set({ step: 1 });
          return;
        }

        // Otherwise, normal decrement
        if (current > 1) {
          set({ step: (current - 1) as WizardStep });
        }
      },

      canGoNext: () => {
        const { step, selectedGame, customGameData, isCustomGame } = get();

        switch (step) {
          case 1:
            // Need a game selected (shared or custom flag set)
            return selectedGame !== null || isCustomGame;
          case 2:
            // Need custom game data with valid name
            return customGameData !== null && customGameData.name.trim().length > 0;
          case 3:
            // PDF optional, always can proceed
            return true;
          case 4:
            // Review step, can't go next (submit instead)
            return false;
          default:
            return false;
        }
      },

      canGoBack: () => {
        const step = get().step;
        // Can always go back except from first step
        return step > 1;
      },

      // DATA ACTIONS

      selectSharedGame: game => {
        set({
          selectedGame: game,
          isCustomGame: false,
          customGameData: null,
          error: null,
        });
      },

      selectCustomGame: () => {
        set({
          selectedGame: null,
          isCustomGame: true,
          customGameData: { name: '' }, // Initialize with empty name
          error: null,
        });
      },

      setCustomGameData: data => {
        set({ customGameData: data, error: null });
      },

      setUploadedPdf: (pdfId, pdfName) => {
        set({
          uploadedPdfId: pdfId,
          uploadedPdfName: pdfName,
          error: null,
        });
      },

      clearPdf: () => {
        set({
          uploadedPdfId: null,
          uploadedPdfName: null,
        });
      },

      // SUBMISSION ACTION

      submitWizard: async () => {
        set({ isProcessing: true, error: null });

        try {
          const { selectedGame, isCustomGame, customGameData, uploadedPdfId } = get();

          // Validate final state
          if (!selectedGame && !isCustomGame) {
            throw new Error('No game selected');
          }

          if (isCustomGame && (!customGameData || !customGameData.name.trim())) {
            throw new Error('Custom game requires a name');
          }

          // TODO (Backend TBD): POST /api/v1/user-library/entries
          // For now, simulate API call with delay
          await new Promise(resolve => setTimeout(resolve, 1000));

          // If PDF uploaded, associate it
          if (uploadedPdfId) {
            // TODO (Backend TBD): POST /api/v1/user-library/entries/{entryId}/pdf
            await new Promise(resolve => setTimeout(resolve, 500));
          }

          toast.success(
            isCustomGame
              ? `"${customGameData?.name}" added to your collection!`
              : `"${selectedGame?.title}" added to your collection!`
          );

          // Reset wizard on success
          get().reset();

          // Navigate to collection dashboard
          window.location.href = '/dashboard/collection';
        } catch (err) {
          const message = err instanceof Error ? err.message : 'Failed to add game';
          set({ error: message, isProcessing: false });
          toast.error(message);
        }
      },

      // RESET ACTION

      reset: () => {
        set(initialState);
      },
    }),
    { name: 'AddGameWizardStore' }
  )
);

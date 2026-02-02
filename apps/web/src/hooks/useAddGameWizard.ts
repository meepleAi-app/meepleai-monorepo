/**
 * Add Game Wizard Hook
 * Issue #3477: Convenience hook wrapping addGameWizardStore
 *
 * Provides computed values and action helpers for wizard components.
 */

import { useAddGameWizardStore, type WizardStep } from '@/stores/addGameWizardStore';

/**
 * Step metadata for UI display
 */
export interface StepInfo {
  number: WizardStep;
  label: string;
  description: string;
  icon: string;
}

const STEPS: StepInfo[] = [
  {
    number: 1,
    label: '1. Search/Select',
    description: 'Find or create game',
    icon: '🔍',
  },
  {
    number: 2,
    label: '2. Game Details',
    description: 'Custom game info',
    icon: '📝',
  },
  {
    number: 3,
    label: '3. Upload PDF',
    description: 'Optional rulebook',
    icon: '📄',
  },
  {
    number: 4,
    label: '4. Review',
    description: 'Confirm and submit',
    icon: '✅',
  },
];

/**
 * Convenience hook for Add Game Wizard
 *
 * Re-exports all store actions and adds computed values.
 */
export function useAddGameWizard() {
  // Get all store state and actions
  const store = useAddGameWizardStore();

  // Computed: Current step info
  const currentStepInfo = STEPS[store.step - 1];

  // Computed: All steps (for progress indicator)
  const allSteps = STEPS;

  // Computed: Should Step 2 be visible?
  const shouldShowStep2 = store.isCustomGame;

  // Computed: Has selected a game (either shared or custom)
  const hasSelectedGame = store.selectedGame !== null || store.isCustomGame;

  // Computed: Summary data for review step
  const reviewSummary = {
    gameName: store.isCustomGame
      ? store.customGameData?.name ?? 'Unnamed Game'
      : store.selectedGame?.title ?? 'Unknown Game',
    isCustom: store.isCustomGame,
    players:
      store.isCustomGame && store.customGameData
        ? `${store.customGameData.minPlayers ?? '?'}-${store.customGameData.maxPlayers ?? '?'}`
        : null,
    playTime:
      store.isCustomGame && store.customGameData?.playTime
        ? `${store.customGameData.playTime} min`
        : null,
    complexity:
      store.isCustomGame && store.customGameData?.complexity
        ? `${store.customGameData.complexity}/5`
        : null,
    hasPdf: store.uploadedPdfId !== null,
    pdfName: store.uploadedPdfName,
  };

  return {
    // State
    step: store.step,
    selectedGame: store.selectedGame,
    isCustomGame: store.isCustomGame,
    customGameData: store.customGameData,
    uploadedPdfId: store.uploadedPdfId,
    uploadedPdfName: store.uploadedPdfName,
    isProcessing: store.isProcessing,
    error: store.error,

    // Computed
    currentStepInfo,
    allSteps,
    shouldShowStep2,
    hasSelectedGame,
    reviewSummary,

    // Navigation actions
    setStep: store.setStep,
    goNext: store.goNext,
    goBack: store.goBack,
    canGoNext: store.canGoNext,
    canGoBack: store.canGoBack,

    // Data actions
    selectSharedGame: store.selectSharedGame,
    selectCustomGame: store.selectCustomGame,
    setCustomGameData: store.setCustomGameData,
    setUploadedPdf: store.setUploadedPdf,
    clearPdf: store.clearPdf,

    // Submission
    submitWizard: store.submitWizard,

    // Reset
    reset: store.reset,
  };
}

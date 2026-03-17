/**
 * useWizardAutoSave - Wizard Auto-Save Hook
 * Issue #4167: Session auto-save draft
 *
 * Automatically saves wizard state to localStorage every 30s and restores on mount.
 * Features:
 * - Auto-save interval (30s)
 * - Draft restoration on mount
 * - Draft clearing on completion
 * - Toast notifications
 */

import { useEffect, useRef } from 'react';

import { toast } from 'sonner';

import { logger } from '@/lib/logger';
import {
  useGameImportWizardStore,
  type GameImportWizardState,
} from '@/stores/useGameImportWizardStore';

// localStorage key for wizard draft
const WIZARD_DRAFT_KEY = 'game_import_wizard_draft';

// Auto-save interval (30 seconds)
const AUTO_SAVE_INTERVAL_MS = 30000;

/**
 * Auto-save wizard state to localStorage
 */
function saveDraft(state: Partial<GameImportWizardState>) {
  try {
    const draft = {
      currentStep: state.currentStep,
      uploadedPdf: state.uploadedPdf,
      extractedMetadata: state.extractedMetadata,
      selectedBggId: state.selectedBggId,
      bggGameData: state.bggGameData,
      enrichedData: state.enrichedData,
      savedAt: new Date().toISOString(),
    };

    localStorage.setItem(WIZARD_DRAFT_KEY, JSON.stringify(draft));
  } catch (error) {
    logger.error('Failed to save wizard draft:', error);
  }
}

/**
 * Load wizard draft from localStorage
 */
function loadDraft(): Partial<GameImportWizardState> | null {
  try {
    const draft = localStorage.getItem(WIZARD_DRAFT_KEY);
    if (!draft) return null;

    const parsed = JSON.parse(draft);

    // Check if draft is stale (older than 24 hours)
    const savedAt = new Date(parsed.savedAt);
    const now = new Date();
    const hoursSinceLastSave = (now.getTime() - savedAt.getTime()) / (1000 * 60 * 60);

    if (hoursSinceLastSave > 24) {
      // Draft is stale, clear it
      clearDraft();
      return null;
    }

    return {
      currentStep: parsed.currentStep,
      uploadedPdf: parsed.uploadedPdf,
      extractedMetadata: parsed.extractedMetadata,
      selectedBggId: parsed.selectedBggId,
      bggGameData: parsed.bggGameData,
      enrichedData: parsed.enrichedData,
    };
  } catch (error) {
    logger.error('Failed to load wizard draft:', error);
    return null;
  }
}

/**
 * Clear wizard draft from localStorage
 */
export function clearDraft() {
  try {
    localStorage.removeItem(WIZARD_DRAFT_KEY);
  } catch (error) {
    logger.error('Failed to clear wizard draft:', error);
  }
}

/**
 * Hook for auto-saving wizard state to localStorage
 *
 * @example
 * ```tsx
 * function WizardComponent() {
 *   useWizardAutoSave();
 *   // Wizard state is automatically saved every 30s
 * }
 * ```
 */
export function useWizardAutoSave() {
  const store = useGameImportWizardStore();
  const hasRestoredRef = useRef(false);

  // Restore draft on mount (only once)
  useEffect(() => {
    if (hasRestoredRef.current) return;
    hasRestoredRef.current = true;

    const draft = loadDraft();
    if (!draft) return;

    // Restore wizard state
    if (draft.currentStep) store.setStep(draft.currentStep);
    if (draft.uploadedPdf) store.setUploadedPdf(draft.uploadedPdf);
    if (draft.extractedMetadata) store.setExtractedMetadata(draft.extractedMetadata);
    if (draft.selectedBggId)
      store.setSelectedBggId(draft.selectedBggId, draft.bggGameData ?? undefined);
    // Note: enrichedData is not directly restorable as it's typically computed in Step 4

    toast.success('Draft restored', {
      description: 'Your previous wizard session has been restored.',
    });
  }, [store]);

  // Auto-save wizard state every 30s
  useEffect(() => {
    const interval = setInterval(() => {
      // Only save if there's meaningful progress (beyond step 1)
      if (store.currentStep > 1 || store.uploadedPdf) {
        saveDraft({
          currentStep: store.currentStep,
          uploadedPdf: store.uploadedPdf,
          extractedMetadata: store.extractedMetadata,
          selectedBggId: store.selectedBggId,
          bggGameData: store.bggGameData,
          enrichedData: store.enrichedData,
        });
      }
    }, AUTO_SAVE_INTERVAL_MS);

    return () => clearInterval(interval);
  }, [store]);

  // Clear draft on wizard completion (when enrichedData is submitted)
  // This is handled by the wizard component calling clearDraft() explicitly
}

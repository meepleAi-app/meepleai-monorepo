/**
 * Game Import Wizard Store
 * Issue #4161: PDF Wizard Container & State Management
 *
 * Zustand store for managing the multi-step PDF import wizard state.
 * Integrates with backend endpoints from Issue #4157 (Wizard Endpoints).
 */

import { create } from 'zustand';
import { devtools } from 'zustand/middleware';

import { toast } from '@/components/layout';

/**
 * Metadata extracted from uploaded PDF
 */
export interface ExtractedMetadata {
  title?: string;
  yearPublished?: number;
  minPlayers?: number;
  maxPlayers?: number;
  playTime?: number;
  minAge?: number;
  complexity?: number;
  description?: string;
  confidence?: number; // 0-100 (UI representation of backend 0.0-1.0 ConfidenceScore)
}

/**
 * BoardGameGeek game data
 */
export interface BggGameData {
  id: number;
  name: string;
  yearPublished?: number;
  minPlayers?: number;
  maxPlayers?: number;
  playingTime?: number;
  minAge?: number;
  description?: string;
  imageUrl?: string;
  thumbnailUrl?: string;
}

/**
 * Enriched game data after conflict resolution
 */
export interface EnrichedGameData {
  title: string;
  minPlayers?: number;
  maxPlayers?: number;
  playTime?: number;
  complexity?: number;
  description?: string;
  bggId?: number;
  imageUrl?: string;
  yearPublished?: number;
  minAge?: number;
}

/**
 * Wizard step type (1-4)
 * 1: Upload PDF
 * 2: Review Extracted Metadata
 * 3: Select BGG Game
 * 4: Resolve Conflicts & Finalize
 */
export type WizardStep = 1 | 2 | 3 | 4;

/**
 * Uploaded PDF reference
 */
export interface UploadedPdf {
  id: string;
  fileName: string;
}

/**
 * Game Import Wizard state interface
 */
export interface GameImportWizardState {
  // Current step (1: Upload, 2: Metadata, 3: BGG, 4: Conflicts)
  currentStep: WizardStep;

  // Step 1: PDF upload
  uploadedPdf: UploadedPdf | null;

  // Step 2: Extracted metadata from PDF
  extractedMetadata: ExtractedMetadata | null;

  // Step 3: Selected BGG game ID
  selectedBggId: number | null;
  bggGameData: BggGameData | null;

  // Step 4: Enriched data after conflict resolution
  enrichedData: EnrichedGameData | null;

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
  setUploadedPdf: (pdf: UploadedPdf) => void;
  setExtractedMetadata: (metadata: ExtractedMetadata) => void;
  setSelectedBggId: (id: number, data?: BggGameData) => void;
  resolveConflicts: (data: EnrichedGameData) => void;

  // Actions: Submission
  submitWizard: () => Promise<void>;

  // Actions: Reset
  reset: () => void;
}

const initialState = {
  currentStep: 1 as WizardStep,
  uploadedPdf: null,
  extractedMetadata: null,
  selectedBggId: null,
  bggGameData: null,
  enrichedData: null,
  isProcessing: false,
  error: null,
};

export const useGameImportWizardStore = create<GameImportWizardState>()(
  devtools(
    (set, get) => ({
      ...initialState,

      // NAVIGATION ACTIONS

      setStep: step => {
        set({ currentStep: step, error: null });
      },

      goNext: () => {
        const current = get().currentStep;
        if (current < 4) {
          set({ currentStep: (current + 1) as WizardStep, error: null });
        }
      },

      goBack: () => {
        const current = get().currentStep;
        if (current > 1) {
          set({ currentStep: (current - 1) as WizardStep, error: null });
        }
      },

      canGoNext: () => {
        const { currentStep, uploadedPdf, extractedMetadata, selectedBggId } = get();

        switch (currentStep) {
          case 1:
            // Need PDF uploaded
            return uploadedPdf !== null;
          case 2:
            // Need extracted metadata (can be empty object)
            return extractedMetadata !== null;
          case 3:
            // Need BGG game selected
            return selectedBggId !== null;
          case 4:
            // Review step, can't go next (submit instead)
            return false;
          default:
            return false;
        }
      },

      canGoBack: () => {
        const currentStep = get().currentStep;
        // Can always go back except from first step
        return currentStep > 1;
      },

      // DATA ACTIONS

      setUploadedPdf: pdf => {
        set({
          uploadedPdf: pdf,
          error: null,
        });
      },

      setExtractedMetadata: metadata => {
        set({
          extractedMetadata: metadata,
          error: null,
        });
      },

      setSelectedBggId: (id, data) => {
        set({
          selectedBggId: id,
          bggGameData: data || null,
          error: null,
        });
      },

      resolveConflicts: data => {
        set({
          enrichedData: data,
          error: null,
        });
      },

      // SUBMISSION ACTION

      submitWizard: async () => {
        set({ isProcessing: true, error: null });

        try {
          const { uploadedPdf, enrichedData } = get();

          // Validate final state
          if (!uploadedPdf) {
            throw new Error('No PDF uploaded');
          }

          if (!enrichedData || !enrichedData.title.trim()) {
            throw new Error('Game title is required');
          }

          // Submit to backend API
          // Backend endpoint from Issue #4157 (PR #4225)
          // POST /api/v1/admin/games/wizard/confirm-import
          const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

          const response = await fetch(`${API_BASE}/api/v1/admin/games/wizard/confirm-import`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            credentials: 'include',
            body: JSON.stringify({
              pdfId: uploadedPdf.id,
              gameData: enrichedData,
            }),
          });

          if (!response.ok) {
            throw new Error(`Import failed: ${response.statusText}`);
          }

          await response.json();

          toast.success(`Game "${enrichedData.title}" imported successfully!`);

          // Reset wizard on success
          get().reset();

          // Navigate to admin games list
          if (typeof window !== 'undefined') {
            window.location.href = '/admin/shared-games';
          }
        } catch (err) {
          const message = err instanceof Error ? err.message : 'Failed to import game';
          set({ error: message, isProcessing: false });
          toast.error(message);
          throw err;
        }
      },

      // RESET ACTION

      reset: () => {
        set(initialState);
      },
    }),
    { name: 'GameImportWizardStore' }
  )
);

/**
 * Game Import Wizard Store — 5-step PDF direct import flow
 *
 * Steps:
 * 1. Upload PDF (chunked, up to 150MB) → { pdfDocumentId }
 * 2. Review metadata (LLM-extracted) + cover image picker + live MeepleCard preview
 * 3. Preview & Confirm (read-only MeepleCard)
 * 4. Creation progress (ImportGameFromPdfCommand saga)
 * 5. RAG test panel (wait for indexing, then interactive Q&A)
 */

import { create } from 'zustand';
import { devtools, persist } from 'zustand/middleware';

import { toast } from '@/components/layout';
import { api } from '@/lib/api';

// ─── Domain Types ─────────────────────────────────────────────────────────────

export interface UploadedPdf {
  pdfDocumentId: string;
  fileName: string;
  /** @deprecated alias for pdfDocumentId — kept for client.tsx compatibility */
  id: string;
}

export interface GameMetadata {
  title: string;
  yearPublished?: number;
  description?: string;
  minPlayers?: number;
  maxPlayers?: number;
  playingTimeMinutes?: number;
  minAge?: number;
  publishers?: string[];
  designers?: string[];
  categories?: string[];
  mechanics?: string[];
  confidenceScore?: number;
}

export type CoverImageMode = 'placeholder' | 'pdf-page' | 'upload';

export interface CoverImageSelection {
  mode: CoverImageMode;
  /** URL of the selected image (blob URL for uploads, API URL for pdf-page, null for placeholder) */
  imageUrl: string | null;
  /** Page number selected (only for mode=pdf-page) */
  pdfPageNumber?: number;
}

export interface ImportResult {
  gameId: string;
  pdfDocumentId: string;
  indexingStatus: 'pending' | 'failed';
  warning?: string;
}

export interface RagTestMessage {
  id: string;
  question: string;
  answer?: string;
  sources?: Array<{ text: string; pageNumber?: number }>;
  isLoading: boolean;
  error?: string;
}

export type WizardStep = 1 | 2 | 3 | 4 | 5;

// ─── Store Interface ───────────────────────────────────────────────────────────

export interface GameImportWizardState {
  currentStep: WizardStep;

  // Step 1
  uploadedPdf: UploadedPdf | null;

  // Step 2
  reviewedMetadata: GameMetadata | null;
  coverImage: CoverImageSelection;

  // Step 3 (no extra state — read from reviewedMetadata + coverImage)

  // Step 4
  importResult: ImportResult | null;

  // Step 5
  ragTestHistory: RagTestMessage[];
  isIndexingReady: boolean;

  // UI
  isProcessing: boolean;
  error: string | null;

  // Navigation
  setStep: (step: WizardStep) => void;
  goNext: () => void;
  goBack: () => void;
  canGoNext: () => boolean;
  canGoBack: () => boolean;

  // Step 1 actions
  setUploadedPdf: (pdf: UploadedPdf) => void;

  // Step 2 actions
  setReviewedMetadata: (metadata: GameMetadata) => void;
  setCoverImage: (cover: CoverImageSelection) => void;

  // Step 4 actions
  executeImport: () => Promise<void>;

  // Step 5 actions
  setIndexingReady: (ready: boolean) => void;
  addRagMessage: (question: string) => string;
  updateRagMessage: (id: string, update: Partial<RagTestMessage>) => void;

  reset: () => void;

  // Legacy compat props (used by client.tsx)
  extractedMetadata: GameMetadata | null;
  selectedBggId: null;
  enrichedData: null;
  bggGameData: null;
  setSelectedBggId: (id: number, data?: unknown) => void;
  submitWizard: () => Promise<void>;
}

// ─── Initial State ─────────────────────────────────────────────────────────────

const initialState = {
  currentStep: 1 as WizardStep,
  uploadedPdf: null,
  reviewedMetadata: null,
  coverImage: { mode: 'placeholder' as CoverImageMode, imageUrl: null },
  importResult: null,
  ragTestHistory: [] as RagTestMessage[],
  isIndexingReady: false,
  isProcessing: false,
  error: null,
  // legacy compat
  extractedMetadata: null as GameMetadata | null,
  selectedBggId: null as null,
  enrichedData: null as null,
  bggGameData: null as null,
};

// ─── Store ────────────────────────────────────────────────────────────────────

export const useGameImportWizardStore = create<GameImportWizardState>()(
  devtools(
    persist(
      (set, get) => ({
        ...initialState,

        // ── Navigation ──────────────────────────────────────────────────────

        setStep: step => set({ currentStep: step, error: null }),

        goNext: () => {
          const { currentStep } = get();
          if (currentStep < 5) {
            set({ currentStep: (currentStep + 1) as WizardStep, error: null });
          }
        },

        goBack: () => {
          const { currentStep } = get();
          if (currentStep > 1) {
            set({ currentStep: (currentStep - 1) as WizardStep, error: null });
          }
        },

        canGoNext: () => {
          const { currentStep, uploadedPdf, reviewedMetadata, importResult } = get();
          switch (currentStep) {
            case 1:
              return uploadedPdf !== null;
            case 2:
              return reviewedMetadata !== null && !!reviewedMetadata.title?.trim();
            case 3:
              return reviewedMetadata !== null;
            case 4:
              // Auto-advance to step 5 on success; can't manually go "next" from step 4
              return importResult !== null;
            case 5:
              return false;
            default:
              return false;
          }
        },

        canGoBack: () => {
          const { currentStep } = get();
          // Can't go back from step 4 (saga in progress) or step 5
          return currentStep > 1 && currentStep < 4;
        },

        // ── Step 1 ─────────────────────────────────────────────────────────

        setUploadedPdf: pdf => {
          set({ uploadedPdf: pdf, extractedMetadata: null, error: null });
        },

        // ── Step 2 ─────────────────────────────────────────────────────────

        setReviewedMetadata: metadata => {
          set({ reviewedMetadata: metadata, extractedMetadata: metadata, error: null });
        },

        setCoverImage: cover => {
          set({ coverImage: cover });
        },

        // ── Step 4: Import saga ────────────────────────────────────────────

        executeImport: async () => {
          const { uploadedPdf, reviewedMetadata, coverImage } = get();

          if (!uploadedPdf) {
            set({ error: 'Nessun PDF caricato' });
            return;
          }
          if (!reviewedMetadata?.title?.trim()) {
            set({ error: 'Il titolo del gioco è obbligatorio' });
            return;
          }

          set({ isProcessing: true, error: null });

          try {
            const result = await api.sharedGames.importGameFromPdf({
              title: reviewedMetadata.title,
              pdfDocumentId: uploadedPdf.pdfDocumentId,
              yearPublished: reviewedMetadata.yearPublished,
              description: reviewedMetadata.description,
              minPlayers: reviewedMetadata.minPlayers,
              maxPlayers: reviewedMetadata.maxPlayers,
              playingTimeMinutes: reviewedMetadata.playingTimeMinutes,
              minAge: reviewedMetadata.minAge,
              coverImageUrl: coverImage.mode !== 'placeholder' ? coverImage.imageUrl : null,
              publishers: reviewedMetadata.publishers,
              designers: reviewedMetadata.designers,
              categories: reviewedMetadata.categories,
              mechanics: reviewedMetadata.mechanics,
            });
            const importResult: ImportResult = {
              gameId: result.gameId,
              pdfDocumentId: result.pdfDocumentId,
              indexingStatus:
                (result.indexingStatus as ImportResult['indexingStatus']) ?? 'pending',
              warning: result.warning ?? undefined,
            };

            set({ importResult, isProcessing: false });

            if (importResult.warning) {
              toast.warning(importResult.warning);
            }
          } catch (err) {
            const message = err instanceof Error ? err.message : 'Import fallito';
            set({ error: message, isProcessing: false });
            toast.error(message);
            throw err;
          }
        },

        // ── Step 5 ─────────────────────────────────────────────────────────

        setIndexingReady: ready => set({ isIndexingReady: ready }),

        addRagMessage: question => {
          const id = crypto.randomUUID();
          set(state => ({
            ragTestHistory: [...state.ragTestHistory, { id, question, isLoading: true }],
          }));
          return id;
        },

        updateRagMessage: (id, update) => {
          set(state => ({
            ragTestHistory: state.ragTestHistory.map(m => (m.id === id ? { ...m, ...update } : m)),
          }));
        },

        // ── Reset ──────────────────────────────────────────────────────────

        reset: () => set(initialState),

        // ── Legacy compat (client.tsx uses these) ─────────────────────────

        setSelectedBggId: () => {
          // no-op: BGG flow removed
        },

        submitWizard: async () => {
          await get().executeImport();
        },
      }),
      {
        name: 'game-import-wizard-v2',
        version: 2,
        partialize: state => ({
          currentStep: state.currentStep,
          uploadedPdf: state.uploadedPdf,
          reviewedMetadata: state.reviewedMetadata,
          coverImage: state.coverImage,
        }),
      }
    ),
    { name: 'GameImportWizardStore' }
  )
);

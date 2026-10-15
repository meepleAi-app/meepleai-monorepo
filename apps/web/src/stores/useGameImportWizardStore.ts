/**
 * Game Import Wizard Store — 5-step PDF direct import flow
 *
 * Steps:
 * 1. Title + PDF: creates the draft game, then uploads the PDF for it (chunked, up to 150MB)
 *    → { gameId, pdfDocumentId }. #4401: the game comes first — the upload needs one.
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
  indexingStatus: 'ready' | 'pending' | 'failed';
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
  /** The title typed at step 1; the draft game is created with it. */
  importTitle: string;
  /** The draft game created at step 1 (#4401). A retried upload reuses it. */
  importGameId: string | null;
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
  setImportTitle: (title: string) => void;
  setImportGameId: (gameId: string) => void;
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

/**
 * The cover to save with the game. The wizard's previews (a PDF page, an uploaded image) are
 * `blob:` URLs that live in this browser tab: saved, they would be dead links. Only an http(s) URL
 * is sent; otherwise the game keeps its placeholder.
 */
export function persistableCoverUrl(cover: CoverImageSelection): string | null {
  if (cover.mode === 'placeholder' || !cover.imageUrl) return null;
  return /^https?:\/\//i.test(cover.imageUrl) ? cover.imageUrl : null;
}

// ─── Initial State ─────────────────────────────────────────────────────────────

const initialState = {
  currentStep: 1 as WizardStep,
  importTitle: '',
  importGameId: null as string | null,
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
              // The game's description is required (domain rule): without it step 4 is refused.
              return (
                reviewedMetadata !== null &&
                !!reviewedMetadata.title?.trim() &&
                !!reviewedMetadata.description?.trim()
              );
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

        setImportTitle: title => set({ importTitle: title }),

        setImportGameId: gameId => set({ importGameId: gameId }),

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
          const { importGameId, uploadedPdf, reviewedMetadata, coverImage } = get();

          if (!importGameId || !uploadedPdf) {
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
              gameId: importGameId,
              title: reviewedMetadata.title,
              pdfDocumentId: uploadedPdf.pdfDocumentId,
              yearPublished: reviewedMetadata.yearPublished,
              description: reviewedMetadata.description,
              minPlayers: reviewedMetadata.minPlayers,
              maxPlayers: reviewedMetadata.maxPlayers,
              playingTimeMinutes: reviewedMetadata.playingTimeMinutes,
              minAge: reviewedMetadata.minAge,
              coverImageUrl: persistableCoverUrl(coverImage),
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
        // 3 (#4401): the draft game id joined the state. A version-2 state holds an upload with no
        // game — which the API never accepted — so it is dropped rather than migrated.
        version: 3,
        partialize: state => ({
          currentStep: state.currentStep,
          importTitle: state.importTitle,
          importGameId: state.importGameId,
          uploadedPdf: state.uploadedPdf,
          reviewedMetadata: state.reviewedMetadata,
          // Sanitize blob: URLs — they are session-scoped and invalid after reload.
          // Upload mode loses its preview; other modes (none/pdfPage) are URL-safe.
          coverImage:
            state.coverImage?.mode === 'upload'
              ? { ...state.coverImage, imageUrl: null }
              : state.coverImage,
        }),
      }
    ),
    { name: 'GameImportWizardStore' }
  )
);

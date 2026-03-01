/**
 * Add Game Wizard Store
 * Issue #4818: AddGameSheet Drawer + State Machine
 * Epic #4817: User Collection Wizard
 *
 * Zustand store for collection wizard state management across 3 steps.
 */

import { create } from 'zustand';
import { devtools } from 'zustand/middleware';

export type WizardEntryPoint =
  | { type: 'fromGameCard'; sharedGameId: string }
  | { type: 'fromLibrary' }
  | { type: 'fromSearch'; bggId?: number };

export type WizardStep = 1 | 2 | 3;

export interface SelectedGameData {
  gameId: string;
  title: string;
  imageUrl?: string;
  thumbnailUrl?: string;
  minPlayers?: number;
  maxPlayers?: number;
  playingTimeMinutes?: number;
  complexityRating?: number;
  averageRating?: number;
  yearPublished?: number;
  description?: string;
  source: 'catalog' | 'bgg' | 'custom';
  categories?: string[];
  mechanics?: string[];
}

export interface PdfDocumentInfo {
  id: string;
  fileName: string;
  pageCount?: number;
  status: string;
  documentType: string;
}

export interface GameInfoValues {
  title: string;
  minPlayers?: number;
  maxPlayers?: number;
  playingTimeMinutes?: number;
  yearPublished?: number;
  complexityRating?: number;
  description?: string;
}

interface AddGameWizardStore {
  // Navigation
  currentStep: WizardStep;
  entryPoint: WizardEntryPoint | null;
  isDirty: boolean;

  // Step 1: Game Source
  selectedGame: SelectedGameData | null;

  // Step 2: PDF / KB
  documents: PdfDocumentInfo[];
  customPdfUploaded: boolean;

  // Step 3: Info & Save
  gameInfo: GameInfoValues | null;

  // Actions
  initialize: (entryPoint: WizardEntryPoint, gameData?: SelectedGameData) => void;
  setCurrentStep: (step: WizardStep) => void;
  goNext: () => void;
  goBack: () => void;
  setSelectedGame: (game: SelectedGameData) => void;
  setDocuments: (docs: PdfDocumentInfo[]) => void;
  setCustomPdfUploaded: (uploaded: boolean) => void;
  setGameInfo: (info: GameInfoValues) => void;
  markDirty: () => void;
  canGoNext: () => boolean;
  reset: () => void;
}

const initialState = {
  currentStep: 1 as WizardStep,
  entryPoint: null as WizardEntryPoint | null,
  isDirty: false,
  selectedGame: null as SelectedGameData | null,
  documents: [] as PdfDocumentInfo[],
  customPdfUploaded: false,
  gameInfo: null as GameInfoValues | null,
};

export const useAddGameWizardStore = create<AddGameWizardStore>()(
  devtools(
    (set, get) => ({
      ...initialState,

      initialize: (entryPoint, gameData) => {
        const startStep: WizardStep = entryPoint.type === 'fromLibrary' ? 1 : 2;
        set(
          {
            ...initialState,
            entryPoint,
            currentStep: startStep,
            selectedGame: gameData ?? null,
          },
          false,
          'initialize'
        );
      },

      setCurrentStep: (step) =>
        set({ currentStep: step }, false, 'setCurrentStep'),

      goNext: () => {
        const { currentStep } = get();
        if (currentStep < 3) {
          set({ currentStep: (currentStep + 1) as WizardStep }, false, 'goNext');
        }
      },

      goBack: () => {
        const { currentStep, entryPoint } = get();
        const minStep: WizardStep =
          entryPoint?.type === 'fromGameCard' || entryPoint?.type === 'fromSearch' ? 2 : 1;
        if (currentStep > minStep) {
          set({ currentStep: (currentStep - 1) as WizardStep }, false, 'goBack');
        }
      },

      setSelectedGame: (game) =>
        set({ selectedGame: game, isDirty: true }, false, 'setSelectedGame'),

      setDocuments: (docs) =>
        set({ documents: docs }, false, 'setDocuments'),

      setCustomPdfUploaded: (uploaded) =>
        set({ customPdfUploaded: uploaded, isDirty: true }, false, 'setCustomPdfUploaded'),

      setGameInfo: (info) =>
        set({ gameInfo: info, isDirty: true }, false, 'setGameInfo'),

      markDirty: () =>
        set({ isDirty: true }, false, 'markDirty'),

      canGoNext: () => {
        const { currentStep, selectedGame } = get();
        switch (currentStep) {
          case 1:
            return selectedGame !== null;
          case 2:
            return true; // PDF step is optional
          case 3:
            return false; // Last step - save button instead
          default:
            return false;
        }
      },

      reset: () => set(initialState, false, 'reset'),
    }),
    { name: 'add-game-wizard-store' }
  )
);

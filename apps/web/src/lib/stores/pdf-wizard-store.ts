/**
 * PDF Wizard Store
 * Issue #4141: Wizard Components - PDF Wizard
 *
 * Zustand store for PDF creation wizard state management across 4 steps.
 */

import { create } from 'zustand';
import { devtools } from 'zustand/middleware';

interface ManualFields {
  minPlayers?: number;
  maxPlayers?: number;
  playingTime?: number;
  minAge?: number;
  description?: string;
}

interface BggGameDetails {
  id: number;
  name: string;
  yearPublished: number;
  minPlayers: number;
  maxPlayers: number;
  playingTime: number;
  minAge: number;
  rating: number;
  thumbnail: string | null;
}

interface PdfWizardStore {
  // Current step (1-4)
  currentStep: number;

  // Step 1: PDF Upload
  pdfDocumentId: string | null;
  qualityScore: number;
  extractedTitle: string;

  // Step 2: Preview Extracted Data
  manualFields: ManualFields;
  duplicateWarnings: string[];

  // Step 3: BGG Match
  selectedBggId: number | null;
  bggDetails: BggGameDetails | null;

  // Actions
  setCurrentStep: (step: number) => void;
  setStep1Data: (data: {
    pdfDocumentId: string;
    qualityScore: number;
    extractedTitle: string;
  }) => void;
  setStep2Data: (data: {
    manualFields: ManualFields;
    duplicateWarnings?: string[];
  }) => void;
  setStep3Data: (data: {
    selectedBggId: number | null;
    bggDetails: BggGameDetails | null;
  }) => void;
  reset: () => void;
}

const initialState = {
  currentStep: 1,
  pdfDocumentId: null,
  qualityScore: 0,
  extractedTitle: '',
  manualFields: {},
  duplicateWarnings: [],
  selectedBggId: null,
  bggDetails: null,
};

export const usePdfWizardStore = create<PdfWizardStore>()(
  devtools(
    (set) => ({
      ...initialState,

      setCurrentStep: (step: number) =>
        set({ currentStep: step }, false, 'setCurrentStep'),

      setStep1Data: (data) =>
        set(
          {
            pdfDocumentId: data.pdfDocumentId,
            qualityScore: data.qualityScore,
            extractedTitle: data.extractedTitle,
          },
          false,
          'setStep1Data'
        ),

      setStep2Data: (data) =>
        set(
          {
            manualFields: data.manualFields,
            duplicateWarnings: data.duplicateWarnings || [],
          },
          false,
          'setStep2Data'
        ),

      setStep3Data: (data) =>
        set(
          {
            selectedBggId: data.selectedBggId,
            bggDetails: data.bggDetails,
          },
          false,
          'setStep3Data'
        ),

      reset: () => set(initialState, false, 'reset'),
    }),
    { name: 'pdf-wizard-store' }
  )
);

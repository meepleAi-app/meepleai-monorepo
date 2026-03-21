import { useReducer } from 'react';

export type WizardStep = 'upload' | 'parse' | 'review' | 'publish';

export type ProcessingStatus = 'pending' | 'processing' | 'completed' | 'failed';

export interface WizardState {
  currentStep: WizardStep;
  documentId: string | null;
  processingStatus: ProcessingStatus | null;
  processingError: string | null;
  error: string | null;
}

export type WizardAction =
  | { type: 'UPLOAD_SUCCESS'; documentId: string }
  | { type: 'PROCESSING_UPDATE'; status: ProcessingStatus }
  | { type: 'PROCESSING_ERROR'; error: string }
  | { type: 'PARSING_COMPLETE' }
  | { type: 'PUBLISH_COMPLETE' }
  | { type: 'NEXT_STEP' }
  | { type: 'PREV_STEP' }
  | { type: 'SET_STEP'; step: WizardStep }
  | { type: 'RESET' }
  | { type: 'ERROR'; error: string };

const initialState: WizardState = {
  currentStep: 'upload',
  documentId: null,
  processingStatus: null,
  processingError: null,
  error: null,
};

const steps: WizardStep[] = ['upload', 'parse', 'review', 'publish'];

function wizardReducer(state: WizardState, action: WizardAction): WizardState {
  switch (action.type) {
    case 'UPLOAD_SUCCESS':
      return {
        ...state,
        documentId: action.documentId,
        currentStep: 'parse',
        processingStatus: 'pending',
        processingError: null,
        error: null,
      };

    case 'PROCESSING_UPDATE':
      return {
        ...state,
        processingStatus: action.status,
      };

    case 'PROCESSING_ERROR':
      return {
        ...state,
        processingError: action.error,
        processingStatus: 'failed',
      };

    case 'PARSING_COMPLETE':
      return {
        ...state,
        currentStep: 'review',
        processingStatus: 'completed',
        processingError: null,
      };

    case 'PUBLISH_COMPLETE':
      return {
        ...state,
        currentStep: 'publish',
      };

    case 'NEXT_STEP': {
      const currentIndex = steps.indexOf(state.currentStep);
      const nextIndex = Math.min(currentIndex + 1, steps.length - 1);
      return {
        ...state,
        currentStep: steps[nextIndex],
      };
    }

    case 'PREV_STEP': {
      const currentIndex = steps.indexOf(state.currentStep);
      const prevIndex = Math.max(currentIndex - 1, 0);
      return {
        ...state,
        currentStep: steps[prevIndex],
      };
    }

    case 'SET_STEP':
      return {
        ...state,
        currentStep: action.step,
      };

    case 'RESET':
      return initialState;

    case 'ERROR':
      return {
        ...state,
        error: action.error,
      };

    default:
      return state;
  }
}

/**
 * useWizard - Wizard state management hook
 *
 * Features:
 * - Predictable state transitions with useReducer
 * - Type-safe actions
 * - Step navigation
 * - Processing status tracking
 * - Error handling
 *
 * Replaces 20+ useState variables with single useReducer
 */
export function useWizard() {
  const [state, dispatch] = useReducer(wizardReducer, initialState);

  return { state, dispatch };
}

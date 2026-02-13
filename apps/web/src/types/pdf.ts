/* eslint-disable security/detect-object-injection -- Safe typed Record access with ProcessingStep enum keys */
/**
 * PDF Processing Types (PDF-08)
 * Type definitions for PDF processing progress tracking
 */

/**
 * Processing step enum matching backend ProcessingStep
 */
export enum ProcessingStep {
  Uploading = 'Uploading',
  Extracting = 'Extracting',
  Chunking = 'Chunking',
  Embedding = 'Embedding',
  Indexing = 'Indexing',
  Completed = 'Completed',
  Failed = 'Failed',
}

/**
 * Processing progress response from API
 */
export interface ProcessingProgress {
  currentStep: ProcessingStep;
  percentComplete: number;
  estimatedTimeRemaining?: number;
  errorMessage?: string;
  updatedAt: string;
}

/**
 * Helper to check if processing is in a terminal state
 */
export function isProcessingComplete(step: ProcessingStep): boolean {
  return step === ProcessingStep.Completed || step === ProcessingStep.Failed;
}

/**
 * Helper to get user-friendly step label
 */
export function getStepLabel(step: ProcessingStep): string {
  const labels: Record<ProcessingStep, string> = {
    [ProcessingStep.Uploading]: 'Uploading PDF',
    [ProcessingStep.Extracting]: 'Extracting text from PDF',
    [ProcessingStep.Chunking]: 'Splitting text into chunks',
    [ProcessingStep.Embedding]: 'Generating embeddings',
    [ProcessingStep.Indexing]: 'Indexing in vector database',
    [ProcessingStep.Completed]: 'Processing completed',
    [ProcessingStep.Failed]: 'Processing failed',
  };
  return labels[step];
}

/**
 * Helper to get step order (for progress visualization)
 */
export function getStepOrder(step: ProcessingStep): number {
  const order: Record<ProcessingStep, number> = {
    [ProcessingStep.Uploading]: 0,
    [ProcessingStep.Extracting]: 1,
    [ProcessingStep.Chunking]: 2,
    [ProcessingStep.Embedding]: 3,
    [ProcessingStep.Indexing]: 4,
    [ProcessingStep.Completed]: 5,
    [ProcessingStep.Failed]: 5,
  };
  return order[step];
}

// ============================================================================
// PdfState Type (Issue #4217 - Multi-Location Status UI)
// ============================================================================

/**
 * Lowercase PDF state type for UI components (Issue #4217)
 * Maps to backend ProcessingStep enum with additional 'pending' state
 */
export type PdfState =
  | 'pending'
  | 'uploading'
  | 'extracting'
  | 'chunking'
  | 'embedding'
  | 'indexing'
  | 'ready'
  | 'failed';

/**
 * Maps backend ProcessingStep enum to frontend PdfState type
 * @param step Backend ProcessingStep value
 * @returns Lowercase PdfState for UI components
 */
export function mapProcessingStepToPdfState(step: ProcessingStep): PdfState {
  const mapping: Record<ProcessingStep, PdfState> = {
    [ProcessingStep.Uploading]: 'uploading',
    [ProcessingStep.Extracting]: 'extracting',
    [ProcessingStep.Chunking]: 'chunking',
    [ProcessingStep.Embedding]: 'embedding',
    [ProcessingStep.Indexing]: 'indexing',
    [ProcessingStep.Completed]: 'ready',
    [ProcessingStep.Failed]: 'failed',
  };
  return mapping[step];
}

/**
 * Helper to get user-friendly PdfState label
 */
export function getPdfStateLabel(state: PdfState): string {
  const labels: Record<PdfState, string> = {
    pending: 'Pending',
    uploading: 'Uploading',
    extracting: 'Extracting',
    chunking: 'Chunking',
    embedding: 'Embedding',
    indexing: 'Indexing',
    ready: 'Ready',
    failed: 'Failed',
  };
  // eslint-disable-next-line security/detect-object-injection -- state is from typed PdfState union
  return labels[state];
}

/**
 * Helper to check if PdfState is terminal (no more processing)
 */
export function isPdfStateTerminal(state: PdfState): boolean {
  return state === 'ready' || state === 'failed';
}

/**
 * Helper to get PdfState order (for progress visualization)
 */
export function getPdfStateOrder(state: PdfState): number {
  const order: Record<PdfState, number> = {
    pending: 0,
    uploading: 1,
    extracting: 2,
    chunking: 3,
    embedding: 4,
    indexing: 5,
    ready: 6,
    failed: 6,
  };
  // eslint-disable-next-line security/detect-object-injection -- state is from typed PdfState union
  return order[state];
}

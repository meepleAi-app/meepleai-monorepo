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
  Failed = 'Failed'
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
    [ProcessingStep.Failed]: 'Processing failed'
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
    [ProcessingStep.Failed]: 5
  };
  return order[step];
}

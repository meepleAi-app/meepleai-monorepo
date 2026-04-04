'use client';

import { createContext, useContext, useState, useMemo, useCallback, type ReactNode } from 'react';

export type PipelineStep = 'upload' | 'extraction' | 'chunking' | 'embedding' | 'ready';
export type StepStatus = 'pending' | 'in_progress' | 'completed' | 'failed';

export interface PipelineStepInfo {
  step: PipelineStep;
  status: StepStatus;
  startedAt?: string;
  completedAt?: string;
  durationMs?: number;
  error?: string;
}

export interface DocumentPipelineStatus {
  documentId: string;
  steps: PipelineStepInfo[];
  overallStatus: 'pending' | 'processing' | 'completed' | 'failed';
}

interface PipelineContextValue {
  processingStatus: Map<string, DocumentPipelineStatus>;
  setProcessingStatus: (docId: string, status: DocumentPipelineStatus) => void;
  clearProcessingStatus: () => void;
  isAnyProcessing: boolean;
  isAllReady: boolean;
  pollingEnabled: boolean;
  setPollingEnabled: (enabled: boolean) => void;
}

const PipelineContext = createContext<PipelineContextValue | null>(null);

export function PipelineProvider({ children }: { children: ReactNode }) {
  const [statusMap, setStatusMap] = useState<Map<string, DocumentPipelineStatus>>(new Map());
  const [pollingEnabled, setPollingEnabled] = useState(false);

  const setProcessingStatus = useCallback((docId: string, status: DocumentPipelineStatus) => {
    setStatusMap(prev => new Map(prev).set(docId, status));
  }, []);

  const clearProcessingStatus = useCallback(() => setStatusMap(new Map()), []);

  const isAnyProcessing = useMemo(
    () => Array.from(statusMap.values()).some(s => s.overallStatus === 'processing'),
    [statusMap]
  );

  const isAllReady = useMemo(
    () =>
      statusMap.size > 0 &&
      Array.from(statusMap.values()).every(s => s.overallStatus === 'completed'),
    [statusMap]
  );

  return (
    <PipelineContext.Provider
      value={{
        processingStatus: statusMap,
        setProcessingStatus,
        clearProcessingStatus,
        isAnyProcessing,
        isAllReady,
        pollingEnabled,
        setPollingEnabled,
      }}
    >
      {children}
    </PipelineContext.Provider>
  );
}

export function usePipeline() {
  const ctx = useContext(PipelineContext);
  if (!ctx) throw new Error('usePipeline must be used within PipelineProvider');
  return ctx;
}

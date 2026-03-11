/**
 * Step 3: Processing — Upload files sequentially and track progress via SSE
 *
 * For each file:
 * - Upload via addRagToSharedGame()
 * - If processingJobId returned, track via useJobSSE
 * - Show pipeline steps and status badges
 *
 * Transitions to step 4 when ALL files are done (completed or failed).
 */

'use client';

import { useState, useEffect, useCallback, useRef } from 'react';

import {
  CheckCircle2,
  AlertCircle,
  Loader2,
  Clock,
  Upload,
} from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Progress } from '@/components/ui/feedback/progress';

import { addRagToSharedGame, type AddRagResult } from '../lib/rag-api';
import type { FileConfig } from './rag-wizard';
import type { FileResult } from './rag-wizard';
import { useJobSSE } from '@/app/admin/(dashboard)/knowledge-base/queue/hooks/use-job-sse';
import { useJobDetail } from '@/app/admin/(dashboard)/knowledge-base/queue/lib/queue-api';

// ── Types ───────────────────────────────────────────────────────────────

type FileStatus = 'waiting' | 'uploading' | 'processing' | 'completed' | 'failed';

interface FileState {
  file: File;
  status: FileStatus;
  uploadProgress: number;
  result: AddRagResult | null;
  error: string | null;
}

// ── Pipeline step labels ────────────────────────────────────────────────

const PIPELINE_STEPS = [
  'Upload',
  'Estrazione testo',
  'Chunking',
  'Generazione embeddings',
  'Indicizzazione vettoriale',
] as const;

// ── Props ───────────────────────────────────────────────────────────────

interface StepProgressProps {
  sharedGameId: string;
  configs: FileConfig[];
  onComplete: (results: FileResult[]) => void;
}

// ── Status Badge ────────────────────────────────────────────────────────

function StatusBadge({ status }: { status: FileStatus }) {
  switch (status) {
    case 'waiting':
      return (
        <Badge variant="secondary" className="text-xs gap-1">
          <Clock className="h-3 w-3" />
          In attesa
        </Badge>
      );
    case 'uploading':
      return (
        <Badge variant="outline" className="text-xs gap-1 text-blue-600 border-blue-300">
          <Upload className="h-3 w-3" />
          Caricamento
        </Badge>
      );
    case 'processing':
      return (
        <Badge variant="outline" className="text-xs gap-1 text-amber-600 border-amber-300">
          <Loader2 className="h-3 w-3 animate-spin" />
          In elaborazione
        </Badge>
      );
    case 'completed':
      return (
        <Badge variant="default" className="text-xs gap-1 bg-green-600">
          <CheckCircle2 className="h-3 w-3" />
          Completato
        </Badge>
      );
    case 'failed':
      return (
        <Badge variant="destructive" className="text-xs gap-1">
          <AlertCircle className="h-3 w-3" />
          Errore
        </Badge>
      );
  }
}

// ── Job SSE Tracker (per file with a processingJobId) ───────────────────

const PROCESSING_TIMEOUT_MS = 300_000; // 5 minutes

function JobTracker({
  jobId,
  onTerminal,
}: {
  jobId: string;
  onTerminal: (status: 'completed' | 'failed') => void;
}) {
  const { connectionState } = useJobSSE(jobId);
  const { data: jobDetail } = useJobDetail(jobId, connectionState === 'connected');
  const notifiedRef = useRef(false);
  const onTerminalRef = useRef(onTerminal);
  onTerminalRef.current = onTerminal;

  // Terminal status from job detail
  useEffect(() => {
    if (!jobDetail || notifiedRef.current) return;
    if (jobDetail.status === 'Completed') {
      notifiedRef.current = true;
      onTerminalRef.current('completed');
    } else if (jobDetail.status === 'Failed') {
      notifiedRef.current = true;
      onTerminalRef.current('failed');
    }
  }, [jobDetail]);

  // Timeout: if processing exceeds 5 minutes, treat as failed
  useEffect(() => {
    if (notifiedRef.current) return;
    const timer = setTimeout(() => {
      if (!notifiedRef.current) {
        notifiedRef.current = true;
        onTerminalRef.current('failed');
      }
    }, PROCESSING_TIMEOUT_MS);
    return () => clearTimeout(timer);
  }, []);

  // SSE connection error: if connection is in 'error' state, treat as failed
  useEffect(() => {
    if (notifiedRef.current) return;
    if (connectionState === 'error') {
      notifiedRef.current = true;
      onTerminalRef.current('failed');
    }
  }, [connectionState]);

  // Show pipeline steps from job detail
  const currentStep = jobDetail?.currentStep;

  return (
    <div className="mt-2 space-y-1">
      {/* SSE connection indicator */}
      <div className="flex items-center gap-1.5 text-xs text-muted-foreground">
        <div
          className={`h-1.5 w-1.5 rounded-full ${
            connectionState === 'connected'
              ? 'bg-green-500'
              : connectionState === 'connecting' || connectionState === 'reconnecting'
                ? 'bg-amber-500 animate-pulse'
                : 'bg-red-500'
          }`}
        />
        SSE: {connectionState}
      </div>

      {/* Pipeline steps */}
      <div className="flex flex-wrap gap-1.5">
        {PIPELINE_STEPS.map(step => {
          const isDone =
            jobDetail?.steps?.some(
              s => s.stepName === step && s.status === 'Completed'
            ) ?? false;
          const isCurrent = currentStep === step;

          return (
            <span
              key={step}
              className={`text-xs px-2 py-0.5 rounded-full border ${
                isDone
                  ? 'bg-green-50 border-green-300 text-green-700 dark:bg-green-900/30 dark:text-green-300'
                  : isCurrent
                    ? 'bg-amber-50 border-amber-300 text-amber-700 dark:bg-amber-900/30 dark:text-amber-300'
                    : 'bg-muted border-transparent text-muted-foreground'
              }`}
            >
              {isDone && <CheckCircle2 className="inline h-3 w-3 mr-0.5 -mt-0.5" />}
              {isCurrent && <Loader2 className="inline h-3 w-3 mr-0.5 -mt-0.5 animate-spin" />}
              {step}
            </span>
          );
        })}
      </div>
    </div>
  );
}

// ── Main Component ──────────────────────────────────────────────────────

export function StepProgress({ sharedGameId, configs, onComplete }: StepProgressProps) {
  const [fileStates, setFileStates] = useState<FileState[]>(() =>
    configs.map(c => ({
      file: c.file,
      status: 'waiting' as FileStatus,
      uploadProgress: 0,
      result: null,
      error: null,
    }))
  );

  const processingStartedRef = useRef(false);

  // Update a single file state immutably
  const updateFileState = useCallback((index: number, updates: Partial<FileState>) => {
    setFileStates(prev =>
      prev.map((fs, i) => (i === index ? { ...fs, ...updates } : fs))
    );
  }, []);

  // Process files sequentially
  useEffect(() => {
    if (processingStartedRef.current) return;
    processingStartedRef.current = true;

    const processAll = async () => {
      for (let i = 0; i < configs.length; i++) {
        const config = configs[i];
        updateFileState(i, { status: 'uploading', uploadProgress: 0 });

        try {
          const result = await addRagToSharedGame(
            sharedGameId,
            config.file,
            config.documentType,
            config.version,
            undefined,
            (percent) => {
              updateFileState(i, { uploadProgress: percent });
            }
          );

          if (result.processingJobId) {
            // Has a job — transition to processing (JobTracker will handle SSE)
            updateFileState(i, {
              status: 'processing',
              uploadProgress: 100,
              result,
            });
          } else {
            // No job (editor pending approval) — mark as completed
            updateFileState(i, {
              status: 'completed',
              uploadProgress: 100,
              result,
            });
          }
        } catch (err) {
          const message = err instanceof Error ? err.message : 'Errore sconosciuto';
          updateFileState(i, {
            status: 'failed',
            error: message,
          });
        }
      }
    };

    void processAll();
  }, [configs, sharedGameId, updateFileState]);

  // Handle SSE terminal event for a processing file
  const handleJobTerminal = useCallback(
    (index: number, terminalStatus: 'completed' | 'failed') => {
      updateFileState(index, { status: terminalStatus });
    },
    [updateFileState]
  );

  // Check if all files are done (no 'waiting', 'uploading', or 'processing')
  const allDone = fileStates.every(
    fs => fs.status === 'completed' || fs.status === 'failed'
  );

  // Transition to complete step
  useEffect(() => {
    if (!allDone) return;

    const results: FileResult[] = fileStates.map(fs => ({
      fileName: fs.file.name,
      result: fs.result,
      error: fs.error,
    }));

    // Small delay so user can see final state
    const timer = setTimeout(() => onComplete(results), 1500);
    return () => clearTimeout(timer);
  }, [allDone, fileStates, onComplete]);

  return (
    <div className="space-y-4">
      <p className="text-sm text-muted-foreground">
        Upload e indicizzazione in corso...
      </p>

      {fileStates.map((fs, index) => (
        <div
          key={`${fs.file.name}-${fs.file.size}`}
          className="rounded-lg border bg-white/60 dark:bg-zinc-800/60 p-4 space-y-2"
        >
          <div className="flex items-center justify-between">
            <span className="text-sm font-medium truncate">{fs.file.name}</span>
            <StatusBadge status={fs.status} />
          </div>

          {/* Upload progress bar */}
          {fs.status === 'uploading' && (
            <Progress value={fs.uploadProgress} className="h-1.5" />
          )}

          {/* Error message */}
          {fs.status === 'failed' && fs.error && (
            <p className="text-xs text-destructive">{fs.error}</p>
          )}

          {/* SSE job tracking */}
          {fs.status === 'processing' && fs.result?.processingJobId && (
            <JobTracker
              jobId={fs.result.processingJobId}
              onTerminal={status => handleJobTerminal(index, status)}
            />
          )}

          {/* Completed — auto-approved info */}
          {fs.status === 'completed' && fs.result && (
            <p className="text-xs text-muted-foreground">
              {fs.result.autoApproved
                ? 'Auto-approvato per RAG'
                : 'In attesa di approvazione'}
            </p>
          )}
        </div>
      ))}
    </div>
  );
}

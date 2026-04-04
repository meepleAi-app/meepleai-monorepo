'use client';

import { useMemo } from 'react';

import { Upload, FileText, Scissors, Brain, CheckCircle2, type LucideIcon } from 'lucide-react';

import { cn } from '@/lib/utils';

import type { PipelineStepInfo, PipelineStep, StepStatus } from './contexts/PipelineContext';

interface PipelineOverviewProps {
  steps: PipelineStepInfo[];
  elapsedMs?: number;
}

const STEP_CONFIG: Record<PipelineStep, { icon: LucideIcon; label: string }> = {
  upload: { icon: Upload, label: 'Upload' },
  extraction: { icon: FileText, label: 'Estrazione' },
  chunking: { icon: Scissors, label: 'Chunking' },
  embedding: { icon: Brain, label: 'Embedding' },
  ready: { icon: CheckCircle2, label: 'Pronto' },
};

const STEP_ORDER: PipelineStep[] = ['upload', 'extraction', 'chunking', 'embedding', 'ready'];

const STATUS_CLASSES: Record<StepStatus, string> = {
  completed: 'bg-emerald-500 text-white border-emerald-500',
  in_progress: 'bg-amber-500 text-white border-amber-500 animate-pulse',
  failed: 'bg-red-500 text-white border-red-500',
  pending: 'bg-gray-100 text-gray-400 border-gray-300',
};

function formatElapsed(ms: number): string {
  if (ms < 1000) return `${ms}ms`;
  const seconds = Math.floor(ms / 1000);
  if (seconds < 60) return `${seconds}s`;
  const minutes = Math.floor(seconds / 60);
  const remainingSeconds = seconds % 60;
  return `${minutes}m ${remainingSeconds}s`;
}

function getStepInfo(steps: PipelineStepInfo[], step: PipelineStep): PipelineStepInfo {
  return (
    steps.find(s => s.step === step) ?? {
      step,
      status: 'pending' as StepStatus,
    }
  );
}

function connectorColor(leftStatus: StepStatus, rightStatus: StepStatus): string {
  if (leftStatus === 'completed' && rightStatus === 'completed') {
    return 'bg-emerald-500';
  }
  if (leftStatus === 'completed' && rightStatus === 'in_progress') {
    return 'bg-amber-300';
  }
  return 'bg-gray-300';
}

export function PipelineOverview({ steps, elapsedMs }: PipelineOverviewProps) {
  const stepInfos = useMemo(() => STEP_ORDER.map(s => getStepInfo(steps, s)), [steps]);

  return (
    <div className="space-y-3">
      {/* Node row */}
      <div className="flex items-center justify-between" role="list" aria-label="Pipeline steps">
        {stepInfos.map((info, idx) => {
          const config = STEP_CONFIG[info.step];
          const Icon = config.icon;
          return (
            <div key={info.step} className="flex items-center" role="listitem">
              {/* Node */}
              <div className="flex flex-col items-center gap-1.5">
                <div
                  data-testid={`step-node-${info.step}`}
                  className={cn(
                    'flex h-10 w-10 items-center justify-center rounded-full border-2 transition-colors',
                    STATUS_CLASSES[info.status]
                  )}
                >
                  <Icon className="h-4 w-4" />
                </div>
                <span className="font-nunito text-xs text-muted-foreground">{config.label}</span>
              </div>

              {/* Connector line (not after last node) */}
              {idx < stepInfos.length - 1 && (
                <div
                  data-testid={`connector-${info.step}`}
                  className={cn(
                    'mx-1 h-0.5 w-6 rounded-full transition-colors sm:w-10 md:w-14',
                    connectorColor(info.status, stepInfos[idx + 1].status)
                  )}
                />
              )}
            </div>
          );
        })}
      </div>

      {/* Elapsed time */}
      {elapsedMs !== undefined && elapsedMs > 0 && (
        <p
          data-testid="elapsed-time"
          className="font-nunito text-center text-xs text-muted-foreground"
        >
          Tempo trascorso: {formatElapsed(elapsedMs)}
        </p>
      )}
    </div>
  );
}

'use client';

import { useEffect, useState } from 'react';

import { X } from 'lucide-react';
import { useSearchParams } from 'next/navigation';

import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

type FlowStep = 'upload' | 'queue' | 'agent-test';
type StepStatus = 'done' | 'in-progress' | 'pending' | 'failed';

interface EmbeddingFlowBannerProps {
  currentStep: FlowStep;
  queueStatus?: 'Completed' | 'Processing' | 'Queued' | 'Failed';
  agentTestDone?: boolean;
}

const STEPS: { key: FlowStep; label: string }[] = [
  { key: 'upload', label: 'Upload' },
  { key: 'queue', label: 'Queue' },
  { key: 'agent-test', label: 'Agent Test' },
];

function getStepStatus(
  step: FlowStep,
  currentStep: FlowStep,
  queueStatus?: string,
  agentTestDone?: boolean
): StepStatus {
  const stepOrder: FlowStep[] = ['upload', 'queue', 'agent-test'];
  const currentIdx = stepOrder.indexOf(currentStep);
  const stepIdx = stepOrder.indexOf(step);

  if (stepIdx < currentIdx) return 'done';
  if (stepIdx > currentIdx) return 'pending';

  // Current step
  if (step === 'queue') {
    if (queueStatus === 'Completed') return 'done';
    if (queueStatus === 'Failed') return 'failed';
    return 'in-progress';
  }
  if (step === 'agent-test') {
    return agentTestDone ? 'done' : 'in-progress';
  }
  return 'done'; // upload is always done if we're here
}

const statusStyles: Record<StepStatus, string> = {
  done: 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400',
  'in-progress': 'bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-400',
  pending: 'bg-gray-100 text-gray-500 dark:bg-gray-800 dark:text-gray-500',
  failed: 'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400',
};

const statusIcons: Record<StepStatus, string> = {
  done: '✓',
  'in-progress': '●',
  pending: '○',
  failed: '✕',
};

export function EmbeddingFlowBanner({
  currentStep,
  queueStatus,
  agentTestDone,
}: EmbeddingFlowBannerProps) {
  const searchParams = useSearchParams();
  const flow = searchParams.get('flow');
  const gameName = searchParams.get('gameName');
  const gameId = searchParams.get('gameId');
  const dismissKey = `embedding-flow-dismissed-${gameId}`;

  // Read sessionStorage in useEffect to avoid SSR hydration mismatch
  const [dismissed, setDismissed] = useState(false);
  useEffect(() => {
    if (typeof window !== 'undefined' && sessionStorage.getItem(dismissKey)) {
      setDismissed(true);
    }
  }, [dismissKey]);

  if (flow !== 'embedding' || !gameName || dismissed) return null;

  const handleDismiss = () => {
    setDismissed(true);
    if (typeof window !== 'undefined') {
      sessionStorage.setItem(dismissKey, 'true');
    }
  };

  return (
    <div
      data-testid="embedding-flow-banner"
      className="mb-4 flex items-center justify-between rounded-lg border border-border bg-card px-4 py-3"
    >
      <div className="flex items-center gap-4">
        <span className="text-sm font-medium">{gameName} — Flusso Embedding</span>
        <div className="flex items-center gap-2">
          {STEPS.map((step, i) => {
            const status = getStepStatus(step.key, currentStep, queueStatus, agentTestDone);
            return (
              <div key={step.key} className="flex items-center gap-2">
                {i > 0 && <span className="text-muted-foreground">→</span>}
                <span
                  data-testid={`flow-step-${step.key}`}
                  data-status={status}
                  className={cn(
                    'rounded-full px-2 py-0.5 text-xs font-medium',
                    statusStyles[status]
                  )}
                >
                  {statusIcons[status]} {step.label}
                </span>
              </div>
            );
          })}
        </div>
      </div>
      <Button
        variant="ghost"
        size="icon"
        className="h-6 w-6"
        onClick={handleDismiss}
        aria-label="Chiudi banner"
      >
        <X className="h-4 w-4" />
      </Button>
    </div>
  );
}

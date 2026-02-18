'use client';

/**
 * Processing Monitor
 * Real-time visual pipeline showing PDF processing progress via SSE.
 * States: Pending → Uploading → Extracting → Chunking → Embedding → Indexing → Ready
 */

import { useEffect, useState } from 'react';
import Link from 'next/link';

import {
  CheckCircle2Icon,
  XCircleIcon,
  LoaderCircleIcon,
  ClockIcon,
  FileTextIcon,
  ScissorsIcon,
  BrainIcon,
  DatabaseIcon,
  UploadIcon,
  RefreshCwIcon,
  ArrowLeftIcon,
  RocketIcon,
} from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';

import {
  useWizardProgressStream,
  type WizardProgressEvent,
} from '@/hooks/useWizardProgressStream';

// ─── Types ───────────────────────────────────────────────────────────────────

interface ProcessingMonitorProps {
  gameId: string;
  gameTitle?: string;
}

type PipelineStepStatus = 'pending' | 'active' | 'completed' | 'failed';

interface PipelineStep {
  id: string;
  label: string;
  icon: React.ReactNode;
  status: PipelineStepStatus;
}

// ─── Pipeline Step Config ────────────────────────────────────────────────────

const PIPELINE_STATES = [
  'Pending',
  'Uploading',
  'Extracting',
  'Chunking',
  'Embedding',
  'Indexing',
  'Ready',
] as const;

function getStepIcon(stepId: string, className: string) {
  switch (stepId) {
    case 'Pending': return <ClockIcon className={className} />;
    case 'Uploading': return <UploadIcon className={className} />;
    case 'Extracting': return <FileTextIcon className={className} />;
    case 'Chunking': return <ScissorsIcon className={className} />;
    case 'Embedding': return <BrainIcon className={className} />;
    case 'Indexing': return <DatabaseIcon className={className} />;
    case 'Ready': return <CheckCircle2Icon className={className} />;
    default: return <ClockIcon className={className} />;
  }
}

function buildPipelineSteps(currentState: string, isFailed: boolean): PipelineStep[] {
  const currentIndex = PIPELINE_STATES.indexOf(
    currentState as typeof PIPELINE_STATES[number]
  );

  return PIPELINE_STATES.map((state, index) => {
    let status: PipelineStepStatus;

    if (isFailed && index === currentIndex) {
      status = 'failed';
    } else if (index < currentIndex) {
      status = 'completed';
    } else if (index === currentIndex) {
      status = currentState === 'Ready' ? 'completed' : 'active';
    } else {
      status = 'pending';
    }

    return {
      id: state,
      label: state,
      icon: getStepIcon(state, 'h-4 w-4'),
      status,
    };
  });
}

// ─── Step Component ──────────────────────────────────────────────────────────

function PipelineStepItem({ step }: { step: PipelineStep }) {
  const statusClasses = {
    pending: 'bg-slate-100 dark:bg-zinc-800 text-muted-foreground border-slate-200 dark:border-zinc-700',
    active: 'bg-amber-50 dark:bg-amber-950/30 text-amber-600 dark:text-amber-400 border-amber-300 dark:border-amber-700 ring-2 ring-amber-500/20',
    completed: 'bg-green-50 dark:bg-green-950/30 text-green-600 dark:text-green-400 border-green-300 dark:border-green-700',
    failed: 'bg-red-50 dark:bg-red-950/30 text-red-600 dark:text-red-400 border-red-300 dark:border-red-700',
  };

  return (
    <div className="flex items-center gap-3">
      <div
        className={`flex h-9 w-9 items-center justify-center rounded-full border transition-all duration-300 ${statusClasses[step.status]}`}
      >
        {step.status === 'active' ? (
          <LoaderCircleIcon className="h-4 w-4 animate-spin" />
        ) : step.status === 'completed' ? (
          <CheckCircle2Icon className="h-4 w-4" />
        ) : step.status === 'failed' ? (
          <XCircleIcon className="h-4 w-4" />
        ) : (
          step.icon
        )}
      </div>
      <span
        className={`text-sm font-medium ${
          step.status === 'active'
            ? 'text-amber-600 dark:text-amber-400'
            : step.status === 'completed'
              ? 'text-green-600 dark:text-green-400'
              : step.status === 'failed'
                ? 'text-red-600 dark:text-red-400'
                : 'text-muted-foreground'
        }`}
      >
        {step.label}
      </span>
    </div>
  );
}

// ─── Connector ───────────────────────────────────────────────────────────────

function PipelineConnector({ isCompleted }: { isCompleted: boolean }) {
  return (
    <div className="ml-4 flex items-center">
      <div
        className={`h-6 w-px transition-colors duration-300 ${
          isCompleted
            ? 'bg-green-400 dark:bg-green-600'
            : 'bg-slate-200 dark:bg-zinc-700'
        }`}
      />
    </div>
  );
}

// ─── Duration Timer ──────────────────────────────────────────────────────────

function DurationTimer({ startTime }: { startTime: Date }) {
  const [elapsed, setElapsed] = useState(0);

  useEffect(() => {
    const interval = setInterval(() => {
      setElapsed(Math.floor((Date.now() - startTime.getTime()) / 1000));
    }, 1000);
    return () => clearInterval(interval);
  }, [startTime]);

  const minutes = Math.floor(elapsed / 60);
  const seconds = elapsed % 60;

  return (
    <span className="text-sm text-muted-foreground tabular-nums">
      {minutes > 0 ? `${minutes}m ${seconds}s` : `${seconds}s`}
    </span>
  );
}

// ─── Main Component ──────────────────────────────────────────────────────────

export function ProcessingMonitor({ gameId, gameTitle }: ProcessingMonitorProps) {
  const { progress, connectionState, isComplete, isFailed, reconnect } =
    useWizardProgressStream(gameId);

  const [startTime] = useState(() => new Date());

  const pdfState = progress?.pdfState ?? 'Pending';
  const steps = buildPipelineSteps(pdfState, isFailed);

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center gap-3">
        <Link href="/admin/shared-games/all">
          <Button variant="ghost" size="icon" className="h-8 w-8">
            <ArrowLeftIcon className="h-4 w-4" />
          </Button>
        </Link>
        <div className="flex-1">
          <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
            Processing {gameTitle ?? 'Game'}
          </h1>
          <p className="text-sm text-muted-foreground mt-1">
            {progress?.message ?? 'Connecting to processing stream...'}
          </p>
        </div>
        <DurationTimer startTime={startTime} />
      </div>

      {/* Progress bar */}
      <div className="relative h-2 rounded-full bg-slate-100 dark:bg-zinc-800 overflow-hidden">
        <div
          className={`absolute inset-y-0 left-0 rounded-full transition-all duration-500 ease-out ${
            isFailed
              ? 'bg-red-500'
              : isComplete
                ? 'bg-green-500'
                : 'bg-gradient-to-r from-amber-500 to-orange-500'
          }`}
          style={{ width: `${progress?.overallPercent ?? 0}%` }}
        />
      </div>

      {/* Pipeline visualization */}
      <Card className="bg-white/90 dark:bg-zinc-800/90 backdrop-blur-xl border-slate-200/60 dark:border-zinc-700/60">
        <CardHeader className="pb-3">
          <CardTitle className="flex items-center justify-between text-base">
            <span className="flex items-center gap-2">
              <RocketIcon className="h-4 w-4 text-amber-500" />
              Processing Pipeline
            </span>
            {progress?.priority === 'Admin' && (
              <span className="text-xs font-medium text-amber-600 dark:text-amber-400 bg-amber-50 dark:bg-amber-950/30 px-2 py-0.5 rounded-full">
                Admin Priority
              </span>
            )}
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="space-y-0">
            {steps.map((step, index) => (
              <div key={step.id}>
                <PipelineStepItem step={step} />
                {index < steps.length - 1 && (
                  <PipelineConnector isCompleted={step.status === 'completed'} />
                )}
              </div>
            ))}
          </div>
        </CardContent>
      </Card>

      {/* Error state */}
      {isFailed && progress?.errorMessage && (
        <Card className="border-red-200 dark:border-red-900/40 bg-red-50 dark:bg-red-950/20">
          <CardContent className="p-4 space-y-3">
            <div className="flex items-center gap-2">
              <XCircleIcon className="h-5 w-5 text-red-600 dark:text-red-400" />
              <p className="font-medium text-red-700 dark:text-red-400">Processing Failed</p>
            </div>
            <p className="text-sm text-red-600 dark:text-red-400/80">
              {progress.errorMessage}
            </p>
          </CardContent>
        </Card>
      )}

      {/* Success state */}
      {isComplete && (
        <Card className="border-green-200 dark:border-green-800/40 bg-green-50 dark:bg-green-950/20">
          <CardContent className="p-4 space-y-3">
            <div className="flex items-center gap-2">
              <CheckCircle2Icon className="h-5 w-5 text-green-600 dark:text-green-400" />
              <p className="font-medium text-green-700 dark:text-green-400">
                Processing Complete!
              </p>
            </div>
            <p className="text-sm text-green-600 dark:text-green-400/80">
              The PDF has been fully processed and indexed. The AI agent can now answer questions about this game.
            </p>
            <Link
              href={`/admin/games/${gameId}/agent/test?title=${encodeURIComponent(gameTitle ?? '')}`}
              className="inline-flex items-center gap-2 rounded-md bg-green-600 hover:bg-green-700 text-white text-sm font-medium px-4 py-2 transition-colors"
            >
              <RocketIcon className="h-4 w-4" />
              Test Agent
            </Link>
          </CardContent>
        </Card>
      )}

      {/* Connection error state */}
      {connectionState === 'error' && (
        <div className="flex items-center justify-center gap-2 text-sm text-muted-foreground">
          <span>Connection lost.</span>
          <Button variant="ghost" size="sm" onClick={reconnect}>
            <RefreshCwIcon className="h-3.5 w-3.5 mr-1.5" />
            Reconnect
          </Button>
        </div>
      )}
    </div>
  );
}

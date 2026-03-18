'use client';

import { useState, useEffect, useRef, useMemo, useCallback } from 'react';

import { Activity, ChevronDown, AlertTriangle, RotateCcw, X } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';

import { usePipeline } from './contexts/PipelineContext';
import { PipelineDeepMetrics, type DeepMetricsData } from './PipelineDeepMetrics';
import { PipelineOverview } from './PipelineOverview';
import { PipelineStepCard, type StepDetails } from './PipelineStepCard';

import type { PipelineStepInfo } from './contexts/PipelineContext';

/** Timeout in ms before showing "stuck" warning */
const STUCK_TIMEOUT_MS = 15 * 60 * 1000; // 15 minutes

interface PipelinePanelProps {
  /** Whether a game is selected (drives empty state) */
  hasGameSelected?: boolean;
  /** All pipeline steps for the current document(s) */
  steps?: PipelineStepInfo[];
  /** Step-specific details keyed by step name */
  stepDetails?: Record<string, StepDetails>;
  /** Deep metrics data */
  deepMetrics?: DeepMetricsData;
  /** Total elapsed time in ms */
  elapsedMs?: number;
  /** Callback when user clicks retry on stuck detection */
  onRetry?: () => void;
  /** Callback when user clicks cancel on stuck detection */
  onCancel?: () => void;
}

export function PipelinePanel({
  hasGameSelected = false,
  steps = [],
  stepDetails,
  deepMetrics,
  elapsedMs,
  onRetry,
  onCancel,
}: PipelinePanelProps) {
  const { isAnyProcessing } = usePipeline();
  const [showDetails, setShowDetails] = useState(false);
  const [showMetrics, setShowMetrics] = useState(false);
  const [isStuck, setIsStuck] = useState(false);

  // Track how long processing has been active
  const processingStartRef = useRef<number | null>(null);

  useEffect(() => {
    if (isAnyProcessing) {
      if (processingStartRef.current === null) {
        processingStartRef.current = Date.now();
      }

      const timer = setInterval(() => {
        if (
          processingStartRef.current &&
          Date.now() - processingStartRef.current > STUCK_TIMEOUT_MS
        ) {
          setIsStuck(true);
        }
      }, 30_000); // check every 30s

      return () => clearInterval(timer);
    } else {
      processingStartRef.current = null;
      setIsStuck(false);
    }
  }, [isAnyProcessing]);

  const handleRetry = useCallback(() => {
    setIsStuck(false);
    processingStartRef.current = Date.now();
    onRetry?.();
  }, [onRetry]);

  const handleCancel = useCallback(() => {
    setIsStuck(false);
    processingStartRef.current = null;
    onCancel?.();
  }, [onCancel]);

  // Compute aggregated steps from context if not provided
  const resolvedSteps = useMemo(() => {
    if (steps.length > 0) return steps;
    return [];
  }, [steps]);

  // Empty state
  if (!hasGameSelected) {
    return (
      <PipelineShell>
        <div className="flex flex-col items-center justify-center py-10 text-center">
          <Activity className="h-10 w-10 text-gray-300 mb-3" />
          <p className="font-nunito text-sm text-muted-foreground">
            Seleziona un gioco per visualizzare la pipeline
          </p>
        </div>
      </PipelineShell>
    );
  }

  return (
    <PipelineShell>
      <div className="space-y-4">
        {/* Level 1: Overview */}
        <PipelineOverview steps={resolvedSteps} elapsedMs={elapsedMs} />

        {/* Stuck detection warning */}
        {isStuck && (
          <div
            data-testid="stuck-warning"
            className="flex items-center gap-3 rounded-lg border border-amber-300 bg-amber-50 px-4 py-3"
          >
            <AlertTriangle className="h-5 w-5 shrink-0 text-amber-600" />
            <p className="flex-1 font-nunito text-sm text-amber-800">
              Il processing sembra bloccato.
            </p>
            <div className="flex gap-2">
              <Button size="sm" variant="outline" onClick={handleRetry} data-testid="stuck-retry">
                <RotateCcw className="mr-1 h-3 w-3" />
                Riprova
              </Button>
              <Button size="sm" variant="outline" onClick={handleCancel} data-testid="stuck-cancel">
                <X className="mr-1 h-3 w-3" />
                Annulla
              </Button>
            </div>
          </div>
        )}

        {/* Level 2 toggle */}
        {resolvedSteps.length > 0 && (
          <Button
            variant="ghost"
            size="sm"
            onClick={() => setShowDetails(v => !v)}
            className="w-full justify-between font-nunito text-xs text-muted-foreground"
            data-testid="toggle-details"
          >
            {showDetails ? 'Nascondi dettagli' : 'Mostra dettagli'}
            <ChevronDown
              className={cn('ml-1 h-3 w-3 transition-transform', showDetails && 'rotate-180')}
            />
          </Button>
        )}

        {/* Level 2: Step cards */}
        {showDetails && (
          <div className="space-y-2" data-testid="step-details-section">
            {resolvedSteps.map(s => (
              <PipelineStepCard key={s.step} step={s} details={stepDetails?.[s.step]} />
            ))}
          </div>
        )}

        {/* Level 3 toggle (only when Level 2 is open) */}
        {showDetails && (
          <Button
            variant="ghost"
            size="sm"
            onClick={() => setShowMetrics(v => !v)}
            className="w-full justify-between font-nunito text-xs text-muted-foreground"
            data-testid="toggle-metrics"
          >
            {showMetrics ? 'Nascondi metriche' : 'Mostra metriche'}
            <ChevronDown
              className={cn('ml-1 h-3 w-3 transition-transform', showMetrics && 'rotate-180')}
            />
          </Button>
        )}

        {/* Level 3: Deep metrics */}
        {showDetails && showMetrics && (
          <div data-testid="deep-metrics-section">
            <PipelineDeepMetrics metrics={deepMetrics} />
          </div>
        )}
      </div>
    </PipelineShell>
  );
}

/** Shared panel chrome with title */
function PipelineShell({ children }: { children: React.ReactNode }) {
  return (
    <div className="rounded-xl border bg-white/70 backdrop-blur-sm p-4 space-y-4">
      <div className="flex items-center gap-2">
        <Activity className="h-4 w-4 text-amber-600" />
        <h3 className="font-quicksand text-sm font-semibold">Pipeline</h3>
      </div>
      {children}
    </div>
  );
}

'use client';

/**
 * Pipeline Preview Component
 *
 * Test and preview pipeline execution with dry-run and live modes.
 * Shows execution trace, metrics, and step-by-step visualization.
 *
 * @version 1.0.0
 * @see Issue #3429 - Pipeline Preview/Test
 */

import { useState, useCallback } from 'react';

import { motion, AnimatePresence } from 'framer-motion';
import {
  Play,
  Pause,
  Square,
  SkipForward,
  RotateCcw,
  Download,
  Zap,
  Clock,
  Coins,
  Hash,
  CheckCircle2,
  XCircle,
  Loader2,
  AlertCircle,
} from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Progress } from '@/components/ui/feedback/progress';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/navigation/tabs';
import { Button } from '@/components/ui/primitives/button';
import { ScrollArea } from '@/components/ui/primitives/scroll-area';
import { Textarea } from '@/components/ui/primitives/textarea';
import { cn } from '@/lib/utils';
import { usePipelineBuilderStore, selectExecutionTrace } from '@/stores/pipelineBuilderStore';

import { QUERY_PRESETS } from './types';

import type { ExecutionStep } from './types';

// =============================================================================
// Types
// =============================================================================

interface PipelinePreviewProps {
  className?: string;
}

// =============================================================================
// Metrics Display
// =============================================================================

interface MetricCardProps {
  icon: React.ReactNode;
  label: string;
  value: string | number;
  unit?: string;
  color?: string;
}

function MetricCard({ icon, label, value, unit, color }: MetricCardProps) {
  return (
    <div className="flex items-center gap-3 p-3 bg-card rounded-lg border">
      <div
        className="p-2 rounded-md"
        style={{ backgroundColor: color ? `${color}20` : 'hsl(var(--muted))' }}
      >
        {icon}
      </div>
      <div>
        <p className="text-xs text-muted-foreground">{label}</p>
        <p className="font-semibold text-lg">
          {value}
          {unit && <span className="text-xs text-muted-foreground ml-1">{unit}</span>}
        </p>
      </div>
    </div>
  );
}

// =============================================================================
// Execution Step Display
// =============================================================================

interface StepDisplayProps {
  step: ExecutionStep;
  index: number;
  isActive: boolean;
}

function StepDisplay({ step, index, isActive }: StepDisplayProps) {
  const statusConfig = {
    pending: { icon: <Clock className="h-4 w-4" />, color: 'text-muted-foreground', bg: 'bg-muted' },
    running: { icon: <Loader2 className="h-4 w-4 animate-spin" />, color: 'text-blue-500', bg: 'bg-blue-500/10' },
    success: { icon: <CheckCircle2 className="h-4 w-4" />, color: 'text-green-500', bg: 'bg-green-500/10' },
    error: { icon: <XCircle className="h-4 w-4" />, color: 'text-red-500', bg: 'bg-red-500/10' },
    skipped: { icon: <AlertCircle className="h-4 w-4" />, color: 'text-amber-500', bg: 'bg-amber-500/10' },
  };

  const config = statusConfig[step.status];

  return (
    <motion.div
      initial={{ opacity: 0, x: -20 }}
      animate={{ opacity: 1, x: 0 }}
      transition={{ delay: index * 0.1 }}
      className={cn(
        'flex items-start gap-3 p-3 rounded-lg border transition-all',
        isActive && 'ring-2 ring-primary',
        config.bg
      )}
    >
      {/* Step Number */}
      <div
        className={cn(
          'flex items-center justify-center w-6 h-6 rounded-full text-xs font-medium',
          config.bg,
          config.color
        )}
      >
        {index + 1}
      </div>

      {/* Step Content */}
      <div className="flex-1 min-w-0">
        <div className="flex items-center justify-between gap-2">
          <span className="font-medium text-sm truncate">{step.nodeName}</span>
          <div className={cn('flex items-center gap-1', config.color)}>
            {config.icon}
            <span className="text-xs capitalize">{step.status}</span>
          </div>
        </div>

        {/* Duration */}
        {step.durationMs !== undefined && step.durationMs !== null && (
          <p className="text-xs text-muted-foreground mt-1">
            Duration: {Number(step.durationMs).toFixed(0)}ms
          </p>
        )}

        {/* Output Preview */}
        {step.output !== undefined && step.output !== null && (
          <details className="mt-2">
            <summary className="text-xs text-muted-foreground cursor-pointer hover:text-foreground">
              View output
            </summary>
            <pre className="mt-1 p-2 bg-background rounded text-[10px] font-mono overflow-auto max-h-[100px]">
              {JSON.stringify(step.output, null, 2)}
            </pre>
          </details>
        )}

        {/* Error */}
        {step.error !== undefined && step.error !== null && (
          <div className="mt-2 p-2 bg-red-500/10 rounded text-xs text-red-500">
            {String(step.error)}
          </div>
        )}
      </div>
    </motion.div>
  );
}

// =============================================================================
// Main Component
// =============================================================================

export function PipelinePreview({ className }: PipelinePreviewProps) {
  const [query, setQuery] = useState('');
  const [mode, setMode] = useState<'dry' | 'live'>('dry');

  const {
    pipeline,
    isExecuting,
    stepMode,
    currentStepIndex,
    runPipeline,
    runDryRun,
    stepThrough,
    stopExecution,
    setStepMode,
    validatePipeline,
  } = usePipelineBuilderStore();

  const executionTrace = usePipelineBuilderStore(selectExecutionTrace);

  const handleRun = useCallback(async () => {
    if (!query.trim()) return;

    // Validate first
    const isValid = validatePipeline();
    if (!isValid) {
      return;
    }

    if (mode === 'dry') {
      await runDryRun(query);
    } else {
      await runPipeline(query);
    }
  }, [query, mode, validatePipeline, runDryRun, runPipeline]);

  const handleReset = useCallback(() => {
    setQuery('');
    stopExecution();
  }, [stopExecution]);

  const handleExportTrace = useCallback(() => {
    if (!executionTrace) return;

    const blob = new Blob([JSON.stringify(executionTrace, null, 2)], {
      type: 'application/json',
    });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `pipeline-trace-${executionTrace.id}.json`;
    a.click();
    URL.revokeObjectURL(url);
  }, [executionTrace]);

  const progress =
    executionTrace && executionTrace.steps.length > 0
      ? ((currentStepIndex + 1) / executionTrace.steps.length) * 100
      : 0;

  const canRun = pipeline && pipeline.nodes.length > 0 && query.trim() && !isExecuting;

  return (
    <div className={cn('flex flex-col h-full', className)}>
      {/* Header */}
      <div className="p-3 border-b">
        <h3 className="font-semibold text-sm flex items-center gap-2">
          <Zap className="h-4 w-4" />
          Test Pipeline
        </h3>
      </div>

      {/* Query Input */}
      <div className="p-3 border-b space-y-3">
        <Textarea
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder="Enter a test query..."
          className="min-h-[80px] resize-none"
          disabled={isExecuting}
        />

        {/* Query Presets */}
        <div className="flex flex-wrap gap-1.5">
          {QUERY_PRESETS.map((preset) => (
            <Button
              key={preset.id}
              variant="outline"
              size="sm"
              className="h-7 text-xs"
              onClick={() => setQuery(preset.query)}
              disabled={isExecuting}
            >
              {preset.name}
            </Button>
          ))}
        </div>

        {/* Mode Toggle */}
        <div className="flex items-center gap-2">
          <Tabs value={mode} onValueChange={(v) => setMode(v as 'dry' | 'live')}>
            <TabsList className="h-8">
              <TabsTrigger value="dry" className="text-xs h-7">
                Dry Run
              </TabsTrigger>
              <TabsTrigger value="live" className="text-xs h-7">
                Live Test
              </TabsTrigger>
            </TabsList>
          </Tabs>
          <label className="flex items-center gap-2 text-xs text-muted-foreground">
            <input
              type="checkbox"
              checked={stepMode}
              onChange={(e) => setStepMode(e.target.checked)}
              disabled={isExecuting}
              className="rounded"
            />
            Step-by-step
          </label>
        </div>

        {/* Control Buttons */}
        <div className="flex items-center gap-2">
          <Button
            onClick={handleRun}
            disabled={!canRun}
            className="flex-1"
          >
            {isExecuting ? (
              <>
                <Pause className="h-4 w-4 mr-2" />
                Running...
              </>
            ) : (
              <>
                <Play className="h-4 w-4 mr-2" />
                Run
              </>
            )}
          </Button>

          {isExecuting && stepMode && (
            <Button variant="outline" onClick={stepThrough}>
              <SkipForward className="h-4 w-4 mr-1" />
              Step
            </Button>
          )}

          {isExecuting && (
            <Button variant="destructive" onClick={stopExecution}>
              <Square className="h-4 w-4" />
            </Button>
          )}

          <Button variant="outline" onClick={handleReset} disabled={isExecuting}>
            <RotateCcw className="h-4 w-4" />
          </Button>
        </div>

        {/* Progress */}
        {isExecuting && (
          <Progress value={progress} className="h-2" />
        )}
      </div>

      {/* Results */}
      <ScrollArea className="flex-1">
        <AnimatePresence mode="wait">
          {executionTrace ? (
            <motion.div
              key="results"
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={{ opacity: 0 }}
              className="p-3 space-y-4"
            >
              {/* Metrics */}
              <div className="grid grid-cols-2 gap-2">
                <MetricCard
                  icon={<Hash className="h-4 w-4 text-blue-500" />}
                  label="Tokens"
                  value={executionTrace.metrics.totalTokens.toLocaleString()}
                  color="hsl(221 83% 53%)"
                />
                <MetricCard
                  icon={<Clock className="h-4 w-4 text-amber-500" />}
                  label="Latency"
                  value={executionTrace.metrics.totalLatencyMs.toFixed(0)}
                  unit="ms"
                  color="hsl(45 93% 47%)"
                />
                <MetricCard
                  icon={<Coins className="h-4 w-4 text-green-500" />}
                  label="Est. Cost"
                  value={`$${executionTrace.metrics.estimatedCost.toFixed(4)}`}
                  color="hsl(142 76% 36%)"
                />
                <MetricCard
                  icon={<CheckCircle2 className="h-4 w-4 text-purple-500" />}
                  label="Nodes"
                  value={`${executionTrace.metrics.nodesExecuted}/${executionTrace.steps.length}`}
                  color="hsl(262 83% 62%)"
                />
              </div>

              {/* Status Badge */}
              <div className="flex items-center justify-between">
                <Badge
                  variant={
                    executionTrace.status === 'completed'
                      ? 'default'
                      : executionTrace.status === 'running'
                        ? 'secondary'
                        : 'destructive'
                  }
                  className="capitalize"
                >
                  {executionTrace.status}
                </Badge>
                {executionTrace.completedAt && (
                  <span className="text-xs text-muted-foreground">
                    {new Date(executionTrace.completedAt).toLocaleTimeString()}
                  </span>
                )}
              </div>

              {/* Execution Trace */}
              <div className="space-y-2">
                <h4 className="text-sm font-medium">Execution Trace</h4>
                <div className="space-y-2">
                  {executionTrace.steps.map((step, index) => (
                    <StepDisplay
                      key={step.nodeId}
                      step={step}
                      index={index}
                      isActive={index === currentStepIndex && isExecuting}
                    />
                  ))}
                </div>
              </div>

              {/* Export */}
              <Button
                variant="outline"
                size="sm"
                onClick={handleExportTrace}
                className="w-full"
              >
                <Download className="h-4 w-4 mr-2" />
                Export Trace
              </Button>
            </motion.div>
          ) : (
            <motion.div
              key="empty"
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={{ opacity: 0 }}
              className="flex flex-col items-center justify-center h-full p-6 text-center"
            >
              <Play className="h-12 w-12 text-muted-foreground/30 mb-4" />
              <p className="text-sm text-muted-foreground">
                Enter a query and click Run to test your pipeline
              </p>
              {pipeline && pipeline.nodes.length === 0 && (
                <p className="text-xs text-amber-500 mt-2">
                  Add some nodes to your pipeline first
                </p>
              )}
            </motion.div>
          )}
        </AnimatePresence>
      </ScrollArea>
    </div>
  );
}

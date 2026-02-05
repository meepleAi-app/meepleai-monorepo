'use client';

/**
 * Validation Panel Component
 *
 * Real-time validation feedback display for strategy pipelines.
 * Shows errors, warnings, and metrics with visual indicators.
 *
 * @see #3462 - Implement strategy validation engine
 */

import { useMemo } from 'react';

import {
  AlertCircle,
  AlertTriangle,
  CheckCircle2,
  Info,
  Clock,
  Coins,
  Hash,
  Zap,
} from 'lucide-react';

import {
  Accordion,
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from '@/components/ui/data-display/accordion';
import { Badge } from '@/components/ui/data-display/badge';
import { ScrollArea } from '@/components/ui/primitives/scroll-area';
import { cn } from '@/lib/utils';

import { PIPELINE_CONSTRAINTS } from './block-metadata';

import type { DetailedValidation } from './validation-engine';

// =============================================================================
// Types
// =============================================================================

export interface ValidationPanelProps {
  /** Validation results */
  validation: DetailedValidation | null;
  /** Whether validation is in progress */
  isValidating?: boolean;
  /** Additional class names */
  className?: string;
  /** Compact mode for inline display */
  compact?: boolean;
}

// =============================================================================
// Sub-Components
// =============================================================================

interface MetricGaugeProps {
  label: string;
  value: number;
  max: number;
  unit: string;
  icon: React.ReactNode;
  warningThreshold?: number;
  errorThreshold?: number;
}

function MetricGauge({
  label,
  value,
  max,
  unit,
  icon,
  warningThreshold = 0.8,
  errorThreshold = 1.0,
}: MetricGaugeProps) {
  const percentage = Math.min((value / max) * 100, 100);
  const isWarning = value / max >= warningThreshold;
  const isError = value / max >= errorThreshold;

  return (
    <div className="space-y-1">
      <div className="flex items-center justify-between text-xs">
        <div className="flex items-center gap-1 text-muted-foreground">
          {icon}
          <span>{label}</span>
        </div>
        <span
          className={cn(
            'font-mono',
            isError && 'text-destructive',
            isWarning && !isError && 'text-yellow-600'
          )}
        >
          {typeof value === 'number' && value >= 1000
            ? value.toLocaleString()
            : typeof value === 'number' && value < 1
            ? value.toFixed(4)
            : value}
          {unit}
        </span>
      </div>
      <div className="relative h-1.5 bg-muted rounded overflow-hidden">
        <div
          className={cn(
            'absolute h-full transition-all',
            isError && 'bg-destructive',
            isWarning && !isError && 'bg-yellow-500',
            !isWarning && !isError && 'bg-green-500'
          )}
          style={{ width: `${percentage}%` }}
        />
        {/* Threshold marker */}
        <div
          className="absolute h-full w-0.5 bg-muted-foreground/30"
          style={{ left: `${warningThreshold * 100}%` }}
        />
      </div>
      <div className="flex justify-between text-[9px] text-muted-foreground">
        <span>0</span>
        <span>
          {typeof max === 'number' && max >= 1000 ? max.toLocaleString() : max}
          {unit}
        </span>
      </div>
    </div>
  );
}

interface ValidationItemProps {
  type: 'error' | 'warning';
  code: string;
  message: string;
  nodeId?: string;
  edgeId?: string;
}

function ValidationItem({ type, code, message, nodeId, edgeId }: ValidationItemProps) {
  const Icon = type === 'error' ? AlertCircle : AlertTriangle;
  const colorClass = type === 'error' ? 'text-destructive' : 'text-yellow-600';

  return (
    <div className="flex items-start gap-2 p-2 rounded bg-muted/50">
      <Icon className={cn('h-4 w-4 flex-shrink-0 mt-0.5', colorClass)} />
      <div className="flex-1 min-w-0">
        <p className="text-xs">{message}</p>
        <div className="flex items-center gap-2 mt-1">
          <Badge variant="outline" className="text-[9px]">
            {code}
          </Badge>
          {nodeId && (
            <span className="text-[9px] text-muted-foreground">
              Node: {nodeId.slice(0, 8)}...
            </span>
          )}
          {edgeId && (
            <span className="text-[9px] text-muted-foreground">
              Edge: {edgeId.slice(0, 8)}...
            </span>
          )}
        </div>
      </div>
    </div>
  );
}

// =============================================================================
// Main Component
// =============================================================================

export function ValidationPanel({
  validation,
  isValidating = false,
  className,
  compact = false,
}: ValidationPanelProps) {
  // Calculate summary
  const summary = useMemo(() => {
    if (!validation) return null;

    const { errors, warnings, metrics } = validation;

    return {
      errorCount: errors.length,
      warningCount: warnings.length,
      isValid: validation.isValid,
      metrics,
    };
  }, [validation]);

  // Compact mode - just show status badge
  if (compact) {
    if (!validation) {
      return (
        <Badge variant="outline" className={cn('text-xs', className)}>
          <Info className="h-3 w-3 mr-1" />
          No validation
        </Badge>
      );
    }

    if (isValidating) {
      return (
        <Badge variant="outline" className={cn('text-xs animate-pulse', className)}>
          Validating...
        </Badge>
      );
    }

    if (validation.isValid && validation.warnings.length === 0) {
      return (
        <Badge
          variant="default"
          className={cn('text-xs bg-green-500 hover:bg-green-600', className)}
        >
          <CheckCircle2 className="h-3 w-3 mr-1" />
          Valid
        </Badge>
      );
    }

    if (validation.isValid && validation.warnings.length > 0) {
      return (
        <Badge variant="outline" className={cn('text-xs text-yellow-600', className)}>
          <AlertTriangle className="h-3 w-3 mr-1" />
          {validation.warnings.length} warnings
        </Badge>
      );
    }

    return (
      <Badge variant="destructive" className={cn('text-xs', className)}>
        <AlertCircle className="h-3 w-3 mr-1" />
        {validation.errors.length} errors
      </Badge>
    );
  }

  // Full panel
  if (!validation) {
    return (
      <div
        className={cn(
          'p-4 text-center text-muted-foreground',
          className
        )}
      >
        <Info className="h-8 w-8 mx-auto mb-2 opacity-50" />
        <p className="text-sm">Add blocks to see validation</p>
      </div>
    );
  }

  return (
    <ScrollArea className={cn('h-full', className)}>
      <div className="p-4 space-y-4">
        {/* Status Header */}
        <div className="flex items-center gap-3">
          {validation.isValid ? (
            <div className="flex items-center gap-2 text-green-600">
              <CheckCircle2 className="h-5 w-5" />
              <span className="font-medium">Pipeline Valid</span>
            </div>
          ) : (
            <div className="flex items-center gap-2 text-destructive">
              <AlertCircle className="h-5 w-5" />
              <span className="font-medium">Validation Failed</span>
            </div>
          )}
          {summary && (
            <div className="flex items-center gap-2 ml-auto">
              {summary.errorCount > 0 && (
                <Badge variant="destructive" className="text-[10px]">
                  {summary.errorCount} errors
                </Badge>
              )}
              {summary.warningCount > 0 && (
                <Badge variant="outline" className="text-[10px] text-yellow-600">
                  {summary.warningCount} warnings
                </Badge>
              )}
            </div>
          )}
        </div>

        {/* Metrics */}
        <div className="space-y-3 p-3 bg-muted/30 rounded-lg">
          <div className="flex items-center gap-1 text-xs font-medium text-muted-foreground">
            <Zap className="h-3 w-3" />
            <span>RESOURCE USAGE</span>
          </div>

          <MetricGauge
            label="Tokens"
            value={validation.metrics.totalEstimatedTokens}
            max={PIPELINE_CONSTRAINTS.maxTokens}
            unit=""
            icon={<Hash className="h-3 w-3" />}
          />

          <MetricGauge
            label="Latency"
            value={validation.metrics.totalEstimatedLatencyMs}
            max={PIPELINE_CONSTRAINTS.maxLatencyMs}
            unit="ms"
            icon={<Clock className="h-3 w-3" />}
          />

          <MetricGauge
            label="Cost"
            value={validation.metrics.totalEstimatedCost}
            max={PIPELINE_CONSTRAINTS.maxCostUsd}
            unit="$"
            icon={<Coins className="h-3 w-3" />}
          />

          <div className="grid grid-cols-2 gap-2 pt-2 border-t">
            <div className="flex items-center justify-between text-xs">
              <span className="text-muted-foreground">Nodes</span>
              <Badge variant="outline" className="text-[10px]">
                {validation.metrics.nodeCount}/{PIPELINE_CONSTRAINTS.maxNodes}
              </Badge>
            </div>
            <div className="flex items-center justify-between text-xs">
              <span className="text-muted-foreground">Edges</span>
              <Badge variant="outline" className="text-[10px]">
                {validation.metrics.edgeCount}/{PIPELINE_CONSTRAINTS.maxEdges}
              </Badge>
            </div>
          </div>
        </div>

        {/* Errors & Warnings */}
        {(validation.errors.length > 0 || validation.warnings.length > 0) && (
          <Accordion type="multiple" defaultValue={['errors', 'warnings']}>
            {/* Errors */}
            {validation.errors.length > 0 && (
              <AccordionItem value="errors">
                <AccordionTrigger className="py-2">
                  <div className="flex items-center gap-2 text-destructive">
                    <AlertCircle className="h-4 w-4" />
                    <span className="text-sm font-medium">
                      Errors ({validation.errors.length})
                    </span>
                  </div>
                </AccordionTrigger>
                <AccordionContent>
                  <div className="space-y-2">
                    {validation.errors.map((error, i) => (
                      <ValidationItem
                        key={`error-${i}`}
                        type="error"
                        code={error.code}
                        message={error.message}
                        nodeId={error.nodeId}
                        edgeId={error.edgeId}
                      />
                    ))}
                  </div>
                </AccordionContent>
              </AccordionItem>
            )}

            {/* Warnings */}
            {validation.warnings.length > 0 && (
              <AccordionItem value="warnings">
                <AccordionTrigger className="py-2">
                  <div className="flex items-center gap-2 text-yellow-600">
                    <AlertTriangle className="h-4 w-4" />
                    <span className="text-sm font-medium">
                      Warnings ({validation.warnings.length})
                    </span>
                  </div>
                </AccordionTrigger>
                <AccordionContent>
                  <div className="space-y-2">
                    {validation.warnings.map((warning, i) => (
                      <ValidationItem
                        key={`warning-${i}`}
                        type="warning"
                        code={warning.code}
                        message={warning.message}
                        nodeId={warning.nodeId}
                        edgeId={warning.edgeId}
                      />
                    ))}
                  </div>
                </AccordionContent>
              </AccordionItem>
            )}
          </Accordion>
        )}

        {/* Timestamp */}
        <div className="text-[10px] text-muted-foreground text-center pt-2 border-t">
          Validated at {validation.validatedAt.toLocaleTimeString()}
        </div>
      </div>
    </ScrollArea>
  );
}

export default ValidationPanel;

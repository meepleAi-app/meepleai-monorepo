'use client';

import { DollarSign } from 'lucide-react';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui';
import type { CostBreakdown, CostEstimate } from '@/lib/agent/playground-sse-parser';
import { cn } from '@/lib/utils';

interface DebugPanelCostProps {
  costBreakdown: CostBreakdown | null;
  sessionTotalCost: number;
  costEstimate: CostEstimate | null;
}

export function DebugPanelCost({
  costBreakdown,
  sessionTotalCost,
  costEstimate,
}: DebugPanelCostProps) {
  if (!costBreakdown) return null;

  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-sm flex items-center gap-2">
          <DollarSign className="h-4 w-4" />
          Cost
          {costBreakdown.isFree && (
            <span className="text-[10px] bg-green-100 text-green-800 px-1 py-0.5 rounded leading-none">
              FREE
            </span>
          )}
        </CardTitle>
      </CardHeader>
      <CardContent>
        <div className="space-y-2 text-sm">
          <div className="flex justify-between">
            <span className="text-muted-foreground">Input</span>
            <span className="font-mono">${costBreakdown.inputCost.toFixed(6)}</span>
          </div>
          <div className="flex justify-between">
            <span className="text-muted-foreground">Output</span>
            <span className="font-mono">${costBreakdown.outputCost.toFixed(6)}</span>
          </div>
          <div className="border-t pt-2 flex justify-between font-medium">
            <span>This request</span>
            <span
              className={cn(
                'font-mono',
                costBreakdown.isFree
                  ? 'text-green-600'
                  : costBreakdown.totalCost > 0.01
                    ? 'text-red-600'
                    : 'text-amber-600'
              )}
            >
              ${costBreakdown.totalCost.toFixed(6)}
            </span>
          </div>
          {sessionTotalCost > 0 && (
            <div className="border-t pt-2 flex justify-between text-xs text-muted-foreground">
              <span>Session total</span>
              <span className="font-mono">${sessionTotalCost.toFixed(6)}</span>
            </div>
          )}
          {/* Cost Estimate vs Actual (Issue #4472) */}
          {costEstimate && !costEstimate.isFree && (
            <div className="border-t pt-2 space-y-1.5">
              <span className="text-xs font-medium text-muted-foreground">Estimate</span>
              <div className="flex justify-between text-xs">
                <span className="text-muted-foreground">Range</span>
                <span
                  className={cn(
                    'font-mono font-semibold',
                    costEstimate.maxCost < 0.001
                      ? 'text-green-600'
                      : costEstimate.maxCost < 0.01
                        ? 'text-amber-600'
                        : 'text-red-600'
                  )}
                >
                  ${costEstimate.minCost.toFixed(6)} – ${costEstimate.maxCost.toFixed(6)}
                </span>
              </div>
              {costBreakdown && (
                <div className="flex justify-between text-xs">
                  <span className="text-muted-foreground">Accuracy</span>
                  <span
                    className={cn(
                      'font-mono text-[11px]',
                      costBreakdown.totalCost >= costEstimate.minCost &&
                        costBreakdown.totalCost <= costEstimate.maxCost
                        ? 'text-green-600'
                        : 'text-amber-600'
                    )}
                  >
                    {costBreakdown.totalCost >= costEstimate.minCost &&
                    costBreakdown.totalCost <= costEstimate.maxCost
                      ? 'Within range'
                      : costBreakdown.totalCost < costEstimate.minCost
                        ? 'Below estimate'
                        : 'Above estimate'}
                  </span>
                </div>
              )}
            </div>
          )}
          {costEstimate?.isFree && (
            <div className="border-t pt-2">
              <div className="flex justify-between text-xs">
                <span className="text-muted-foreground">Estimate</span>
                <span className="text-[10px] bg-green-100 text-green-800 px-1.5 py-0.5 rounded leading-none font-semibold">
                  FREE
                </span>
              </div>
            </div>
          )}
        </div>
      </CardContent>
    </Card>
  );
}

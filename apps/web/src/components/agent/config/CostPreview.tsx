/**
 * Cost Preview - Display estimated cost before agent launch
 * Issue #3383: Cost Estimation Preview Before Launch
 *
 * Real-time cost preview with API integration:
 * - Fetches actual pricing from backend
 * - Per-query and per-session estimates
 * - Warning indicators for high costs
 * - Token breakdown tooltips
 */

'use client';

import { Calculator, AlertTriangle, TrendingUp, Info } from 'lucide-react';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
import {
  useCostEstimate,
  calculateSessionCost,
  getCostWarningLevel,
} from '@/hooks/useCostEstimate';
import { cn } from '@/lib/utils';
import { useAgentStore } from '@/stores/agentStore';

interface CostPreviewProps {
  className?: string;
  agentDefinitionId?: string | null;
  estimatedQueriesPerSession?: number;
}

export function CostPreview({
  className,
  agentDefinitionId,
  estimatedQueriesPerSession = 5,
}: CostPreviewProps) {
  const { selectedagentDefinitionId } = useAgentStore();

  // Use provided agentDefinitionId or fall back to store selection
  const activeagentDefinitionId = agentDefinitionId || selectedagentDefinitionId;

  // Fetch real-time cost estimate from API
  const { data: estimate, isLoading } = useCostEstimate(activeagentDefinitionId);

  // Early return if no typology selected
  if (!activeagentDefinitionId) {
    return null;
  }

  // Loading state
  if (isLoading) {
    return (
      <Card className={cn('border-slate-700 bg-slate-800/50', className)}>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-sm font-medium text-slate-200">
            <Calculator className="h-4 w-4 text-slate-400" />
            Cost Preview
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="text-sm text-slate-500 italic">Calculating costs...</div>
        </CardContent>
      </Card>
    );
  }

  // No estimate available
  if (!estimate?.costEstimate) {
    return (
      <Card className={cn('border-slate-700 bg-slate-800/50', className)}>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-sm font-medium text-slate-200">
            <Calculator className="h-4 w-4 text-slate-400" />
            Cost Preview
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="text-sm text-slate-500 italic">
            Select a configuration to see cost estimate
          </div>
        </CardContent>
      </Card>
    );
  }

  const { costEstimate } = estimate;
  const perQueryCost = costEstimate.estimatedCostPerQuery;
  const sessionCost = calculateSessionCost(perQueryCost, estimatedQueriesPerSession);
  const warningLevel = getCostWarningLevel(sessionCost);

  const borderColor =
    warningLevel === 'high'
      ? 'border-red-500/50'
      : warningLevel === 'medium'
        ? 'border-yellow-500/50'
        : 'border-slate-700';

  const bgColor =
    warningLevel === 'high'
      ? 'bg-red-500/10'
      : warningLevel === 'medium'
        ? 'bg-yellow-500/10'
        : 'bg-slate-800/50';

  if (!activeagentDefinitionId) {
    return null;
  }

  return (
    <Card className={cn(borderColor, bgColor, className)}>
      <CardHeader>
        <CardTitle className="flex items-center justify-between text-sm font-medium text-slate-200">
          <div className="flex items-center gap-2">
            <Calculator className="h-4 w-4 text-slate-400" />
            Cost Preview
          </div>
          {warningLevel === 'high' && <AlertTriangle className="h-4 w-4 text-red-400" />}
        </CardTitle>
      </CardHeader>

      <CardContent className="space-y-3">
        {/* Per-Query Cost */}
        <div className="flex justify-between items-center">
          <TooltipProvider>
            <Tooltip>
              <TooltipTrigger asChild>
                <div className="flex items-center gap-1 text-sm text-slate-400 cursor-help">
                  <span>Per query</span>
                  <Info className="h-3 w-3" />
                </div>
              </TooltipTrigger>
              <TooltipContent>
                <div className="space-y-1 text-xs">
                  <p className="font-medium">Token Breakdown</p>
                  <p>Total tokens: {costEstimate.estimatedTokensPerQuery.toLocaleString()}</p>
                  {Object.entries(costEstimate.costByPhase).length > 0 && (
                    <>
                      <p className="font-medium mt-2">Cost by Phase:</p>
                      {Object.entries(costEstimate.costByPhase).map(([phase, cost]) => (
                        <p key={phase}>
                          {phase}: ${cost.toFixed(4)}
                        </p>
                      ))}
                    </>
                  )}
                </div>
              </TooltipContent>
            </Tooltip>
          </TooltipProvider>
          <span className="text-lg font-bold text-white">${perQueryCost.toFixed(4)}</span>
        </div>

        {/* Per-Session Cost */}
        <div className="flex justify-between items-center text-sm">
          <span className="text-slate-500">Per session ({estimatedQueriesPerSession} queries)</span>
          <span className="text-slate-300">${sessionCost.toFixed(3)}</span>
        </div>

        {/* Monthly Cost (10K queries) */}
        <div className="flex justify-between items-center text-sm">
          <span className="text-slate-500">Monthly (10K queries)</span>
          <span
            className={cn(
              'font-medium',
              warningLevel === 'high'
                ? 'text-red-400'
                : warningLevel === 'medium'
                  ? 'text-yellow-400'
                  : 'text-green-400'
            )}
          >
            ${costEstimate.estimatedMonthlyCost10K.toFixed(2)}
          </span>
        </div>

        {/* Warning Messages */}
        {warningLevel === 'high' && (
          <div className="mt-3 flex items-start gap-2 text-xs text-red-300 bg-red-500/10 border border-red-500/30 rounded-md p-2">
            <AlertTriangle className="h-3 w-3 mt-0.5 flex-shrink-0" />
            <span>
              High cost detected. Session cost (${sessionCost.toFixed(2)}) exceeds $0.50. Consider
              optimizing your configuration.
            </span>
          </div>
        )}

        {warningLevel === 'medium' && (
          <div className="mt-3 flex items-start gap-2 text-xs text-yellow-300 bg-yellow-500/10 border border-yellow-500/30 rounded-md p-2">
            <TrendingUp className="h-3 w-3 mt-0.5 flex-shrink-0" />
            <span>Moderate cost. Monitor usage to stay within budget.</span>
          </div>
        )}

        {warningLevel === 'low' && (
          <div className="mt-3 text-xs text-green-300">✓ Cost-effective configuration</div>
        )}
      </CardContent>
    </Card>
  );
}

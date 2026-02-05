'use client';

/**
 * Confidence Scoring Node - Specialized ReactFlow node
 *
 * Enhanced visualization for answer confidence calculation.
 * Shows: scoring method, threshold, confidence meter.
 *
 * @see #3458 - Implement Tier 1 blocks (7 essential RAG blocks)
 */

import { memo } from 'react';

import { Gauge, TrendingUp } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Progress } from '@/components/ui/feedback/progress';

import { BaseBlockNode, type BaseBlockNodeProps } from './BaseBlockNode';

function ConfidenceScoringNodeComponent(props: BaseBlockNodeProps) {
  const { data } = props;
  const params = data.params as Record<string, unknown>;

  const method = (params.method as string) || 'logprobs';
  const threshold = (params.threshold as [number, number]) || [0.7, 1.0];

  const methodLabels: Record<string, { label: string; icon: string }> = {
    logprobs: { label: 'Log Probabilities', icon: '📊' },
    'multi-model': { label: 'Multi-Model', icon: '🤖' },
    citation: { label: 'Citation Coverage', icon: '📝' },
  };

  const methodInfo = methodLabels[method] || { label: method, icon: '📈' };

  return (
    <BaseBlockNode
      {...props}
      headerContent={
        <div className="flex items-center gap-1.5">
          <Gauge className="h-3.5 w-3.5 text-red-600" />
          <span className="text-sm font-medium">Confidence</span>
        </div>
      }
      bodyContent={
        <div className="space-y-2">
          <p className="text-xs text-muted-foreground">
            Assign confidence score to answers
          </p>

          {/* Method */}
          <div className="flex items-center gap-1 text-xs">
            <span>{methodInfo.icon}</span>
            <Badge variant="secondary" className="text-[10px]">
              {methodInfo.label}
            </Badge>
          </div>

          {/* Threshold visualization */}
          <div className="space-y-1">
            <div className="flex items-center justify-between text-[10px] text-muted-foreground">
              <span>Min Threshold</span>
              <span className="font-medium text-foreground">
                {(threshold[0] * 100).toFixed(0)}%
              </span>
            </div>
            <div className="relative">
              <Progress value={100} className="h-2" />
              <div
                className="absolute top-0 left-0 h-2 bg-red-400/50 rounded-l"
                style={{ width: `${threshold[0] * 100}%` }}
              />
              <div
                className="absolute top-0 h-full w-0.5 bg-foreground"
                style={{ left: `${threshold[0] * 100}%` }}
              />
            </div>
            <div className="flex justify-between text-[9px] text-muted-foreground">
              <span className="text-red-500">Reject</span>
              <span className="text-green-500">Accept</span>
            </div>
          </div>

          {/* Output indicator */}
          <div className="flex items-center gap-1 text-[10px]">
            <TrendingUp className="h-3 w-3 text-muted-foreground" />
            <span className="text-muted-foreground">
              Outputs: scores + evaluation
            </span>
          </div>
        </div>
      }
      footerContent={
        <div className="flex items-center justify-between text-[10px]">
          <span className="text-green-600 font-medium">⚡ Fast</span>
          <span className="text-muted-foreground">~30ms • ~100 tok</span>
        </div>
      }
    />
  );
}

export const ConfidenceScoringNode = memo(ConfidenceScoringNodeComponent);
export default ConfidenceScoringNode;

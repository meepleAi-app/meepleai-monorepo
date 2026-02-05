'use client';

/**
 * Self-RAG Reflection Node - Specialized ReactFlow node
 *
 * Enhanced visualization for self-reflective RAG with confidence-based retrieval.
 * Shows: confidence threshold, critique mode.
 *
 * @see #3466 - Implement Tier 3-4 experimental blocks
 */

import { memo } from 'react';

import { Eye, RefreshCcw, ThumbsUp } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Progress } from '@/components/ui/feedback/progress';

import { BaseBlockNode, type BaseBlockNodeProps } from './BaseBlockNode';

function SelfRagNodeComponent(props: BaseBlockNodeProps) {
  const { data } = props;
  const params = data.params as Record<string, unknown>;

  const threshold = (params.threshold as [number, number]) || [0.7, 1.0];
  const critiqueMode = (params.critiqueMode as string) || 'during';

  return (
    <BaseBlockNode
      {...props}
      headerContent={
        <div className="flex items-center gap-1.5">
          <Eye className="h-3.5 w-3.5 text-red-600" />
          <span className="text-sm font-medium">Self-RAG</span>
        </div>
      }
      bodyContent={
        <div className="space-y-2">
          <p className="text-xs text-muted-foreground">
            LLM self-assesses for adaptive retrieval (+10-20%)
          </p>

          {/* Critique mode */}
          <div className="flex items-center gap-2 text-xs">
            <RefreshCcw className="h-3 w-3 text-muted-foreground" />
            <span className="text-muted-foreground">Critique:</span>
            <Badge
              variant={critiqueMode === 'during' ? 'default' : 'secondary'}
              className="text-[10px] px-1"
            >
              {critiqueMode === 'during' ? '⚡ During Gen' : '📝 Post Gen'}
            </Badge>
          </div>

          {/* Confidence threshold */}
          <div className="space-y-1">
            <div className="flex items-center justify-between text-[10px] text-muted-foreground">
              <span>Confidence Threshold</span>
              <span>{(threshold[0] * 100).toFixed(0)}%</span>
            </div>
            <Progress value={threshold[0] * 100} className="h-1.5" />
          </div>

          {/* Visual self-reflection loop */}
          <div className="p-2 bg-muted/50 rounded">
            <div className="flex items-center justify-center gap-2 text-[9px]">
              <div className="flex flex-col items-center">
                <span className="text-muted-foreground">Generate</span>
                <div className="w-8 h-6 rounded bg-red-100 dark:bg-red-900/30 flex items-center justify-center">
                  ✍️
                </div>
              </div>
              <RefreshCcw className="h-4 w-4 text-red-400" />
              <div className="flex flex-col items-center">
                <span className="text-muted-foreground">Reflect</span>
                <div className="w-8 h-6 rounded bg-red-100 dark:bg-red-900/30 flex items-center justify-center">
                  🤔
                </div>
              </div>
              <span className="text-red-400">→</span>
              <div className="flex flex-col items-center">
                <ThumbsUp className="h-4 w-4 text-green-500" />
                <span className="text-green-600 font-medium">OK?</span>
              </div>
            </div>
          </div>
        </div>
      }
      footerContent={
        <div className="flex items-center justify-between text-[10px]">
          <span className="text-red-600 font-medium">🎭 Adaptive</span>
          <span className="text-muted-foreground">~400ms • ~800 tok</span>
        </div>
      }
    />
  );
}

export const SelfRagNode = memo(SelfRagNodeComponent);
export default SelfRagNode;

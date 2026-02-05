'use client';

/**
 * CRAG Evaluator Node - Specialized ReactFlow node
 *
 * Enhanced visualization for retrieval evaluation.
 * Shows: thresholds, correction actions, evaluation model.
 *
 * @see #3458 - Implement Tier 1 blocks (7 essential RAG blocks)
 */

import { memo } from 'react';

import { Scale, AlertTriangle, CheckCircle2, XCircle } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';

import { BaseBlockNode, type BaseBlockNodeProps } from './BaseBlockNode';

function CragEvaluatorNodeComponent(props: BaseBlockNodeProps) {
  const { data } = props;
  const params = data.params as Record<string, unknown>;

  const evaluatorModel = (params.evaluatorModel as string) || 't5-large';
  const correctThreshold = (params.correctThreshold as [number, number]) || [0.8, 1.0];
  const ambiguousThreshold = (params.ambiguousThreshold as [number, number]) || [0.5, 0.8];
  const correctionAction = (params.correctionAction as string) || 'web-search';

  const actionLabels: Record<string, string> = {
    'web-search': '🌐 Web Search',
    'rewrite-query': '✏️ Rewrite Query',
    'skip-doc': '⏭️ Skip Document',
  };

  return (
    <BaseBlockNode
      {...props}
      headerContent={
        <div className="flex items-center gap-1.5">
          <Scale className="h-3.5 w-3.5 text-red-600" />
          <span className="text-sm font-medium">CRAG Evaluator</span>
        </div>
      }
      bodyContent={
        <div className="space-y-2">
          <p className="text-xs text-muted-foreground">
            Quality gate with correction (+5-15% accuracy)
          </p>

          {/* Threshold visualization */}
          <div className="space-y-1">
            <div className="flex h-3 rounded overflow-hidden text-[9px]">
              <div
                className="bg-red-400 flex items-center justify-center text-white"
                style={{ width: `${ambiguousThreshold[0] * 100}%` }}
              >
                <XCircle className="h-2 w-2" />
              </div>
              <div
                className="bg-yellow-400 flex items-center justify-center"
                style={{ width: `${(correctThreshold[0] - ambiguousThreshold[0]) * 100}%` }}
              >
                <AlertTriangle className="h-2 w-2" />
              </div>
              <div
                className="bg-green-400 flex items-center justify-center text-white"
                style={{ width: `${(1 - correctThreshold[0]) * 100}%` }}
              >
                <CheckCircle2 className="h-2 w-2" />
              </div>
            </div>
            <div className="flex justify-between text-[9px] text-muted-foreground">
              <span>Incorrect</span>
              <span>Ambiguous</span>
              <span>Correct</span>
            </div>
          </div>

          {/* Thresholds */}
          <div className="grid grid-cols-2 gap-1 text-[10px]">
            <div className="flex items-center gap-1">
              <span className="text-green-600">✓</span>
              <span>&gt;{(correctThreshold[0] * 100).toFixed(0)}%</span>
            </div>
            <div className="flex items-center gap-1">
              <span className="text-yellow-600">?</span>
              <span>{(ambiguousThreshold[0] * 100).toFixed(0)}-{(correctThreshold[0] * 100).toFixed(0)}%</span>
            </div>
          </div>

          {/* Correction action */}
          <div className="flex items-center gap-1 text-xs">
            <span className="text-muted-foreground">On fail:</span>
            <Badge variant="outline" className="text-[10px]">
              {actionLabels[correctionAction] || correctionAction}
            </Badge>
          </div>
        </div>
      }
      footerContent={
        <div className="flex items-center justify-between text-[10px]">
          <Badge variant="outline" className="text-[9px] px-1">
            {evaluatorModel}
          </Badge>
          <span className="text-muted-foreground">~300ms</span>
        </div>
      }
    />
  );
}

export const CragEvaluatorNode = memo(CragEvaluatorNodeComponent);
export default CragEvaluatorNode;

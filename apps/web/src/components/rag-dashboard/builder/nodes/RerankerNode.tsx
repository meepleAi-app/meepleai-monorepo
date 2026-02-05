'use client';

/**
 * Cross-Encoder Reranking Node - Specialized ReactFlow node
 *
 * Enhanced visualization for document reranking.
 * Shows: model selection, input/output candidates, accuracy boost.
 *
 * @see #3458 - Implement Tier 1 blocks (7 essential RAG blocks)
 */

import { memo } from 'react';

import { ArrowUpDown, Filter, Sparkles } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';

import { BaseBlockNode, type BaseBlockNodeProps } from './BaseBlockNode';

function RerankerNodeComponent(props: BaseBlockNodeProps) {
  const { data } = props;
  const params = data.params as Record<string, unknown>;

  const model = (params.model as string) || 'mxbai-rerank-large';
  const topN = (params.topN as number) || 5;
  const inputTopK = (params.inputTopK as number) || 25;

  // Shorten model name
  const shortModel = model.replace('-rerank-', '-').replace('cohere-', '').replace('-v3', '');

  return (
    <BaseBlockNode
      {...props}
      headerContent={
        <div className="flex items-center gap-1.5">
          <ArrowUpDown className="h-3.5 w-3.5 text-yellow-600" />
          <span className="text-sm font-medium">Reranker</span>
        </div>
      }
      bodyContent={
        <div className="space-y-2">
          <p className="text-xs text-muted-foreground">
            Cross-encoder reranking (+20-40% precision)
          </p>

          {/* Model badge */}
          <div className="flex items-center gap-1 text-xs">
            <Sparkles className="h-3 w-3 text-yellow-500" />
            <Badge variant="secondary" className="text-[10px] px-1.5">
              {shortModel}
            </Badge>
          </div>

          {/* Funnel visualization */}
          <div className="flex items-center gap-2 text-xs">
            <div className="flex items-center gap-1 px-2 py-1 bg-muted rounded">
              <Filter className="h-3 w-3" />
              <span>{inputTopK}</span>
            </div>
            <span className="text-muted-foreground">→</span>
            <div className="flex items-center gap-1 px-2 py-1 bg-yellow-100 dark:bg-yellow-900/30 rounded text-yellow-700 dark:text-yellow-400">
              <span className="font-medium">{topN}</span>
            </div>
          </div>

          {/* Compression ratio */}
          <div className="text-[10px] text-muted-foreground">
            Compression: {((1 - topN / inputTopK) * 100).toFixed(0)}% reduction
          </div>
        </div>
      }
      footerContent={
        <div className="flex items-center justify-between text-[10px]">
          <span className="text-yellow-600 font-medium">📊 Ranking</span>
          <span className="text-muted-foreground">~300ms • 0 tok</span>
        </div>
      }
    />
  );
}

export const RerankerNode = memo(RerankerNodeComponent);
export default RerankerNode;

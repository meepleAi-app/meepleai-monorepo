'use client';

/**
 * Vector Search Node - Specialized ReactFlow node
 *
 * Enhanced visualization for semantic similarity search.
 * Shows: top-k setting, min score threshold, embedding model.
 *
 * @see #3458 - Implement Tier 1 blocks (7 essential RAG blocks)
 */

import { memo } from 'react';

import { Database, Search } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Progress } from '@/components/ui/feedback/progress';

import { BaseBlockNode, type BaseBlockNodeProps } from './BaseBlockNode';

function VectorSearchNodeComponent(props: BaseBlockNodeProps) {
  const { data } = props;
  const params = data.params as Record<string, unknown>;

  const topK = (params.topK as number) || 5;
  const minScore = (params.minScore as [number, number]) || [0.55, 1.0];
  const embeddingModel = (params.embeddingModel as string) || 'text-embedding-3-large';

  // Shorten model name for display
  const shortModel = embeddingModel.replace('text-embedding-', '').replace('-large', '-L').replace('-small', '-S');

  return (
    <BaseBlockNode
      {...props}
      headerContent={
        <div className="flex items-center gap-1.5">
          <Search className="h-3.5 w-3.5 text-green-600" />
          <span className="text-sm font-medium">Vector Search</span>
        </div>
      }
      bodyContent={
        <div className="space-y-2">
          <p className="text-xs text-muted-foreground">
            Semantic similarity search using embeddings
          </p>

          {/* Parameters visualization */}
          <div className="grid grid-cols-2 gap-2 text-xs">
            <div className="flex items-center gap-1">
              <Database className="h-3 w-3 text-muted-foreground" />
              <span className="text-muted-foreground">Top K:</span>
              <Badge variant="outline" className="text-[10px] px-1">
                {topK}
              </Badge>
            </div>
            <div className="flex items-center gap-1">
              <span className="text-muted-foreground">Model:</span>
              <Badge variant="secondary" className="text-[10px] px-1 truncate max-w-[80px]">
                {shortModel}
              </Badge>
            </div>
          </div>

          {/* Score threshold visualization */}
          <div className="space-y-1">
            <div className="flex items-center justify-between text-[10px] text-muted-foreground">
              <span>Min Score Threshold</span>
              <span>{(minScore[0] * 100).toFixed(0)}%</span>
            </div>
            <Progress value={minScore[0] * 100} className="h-1.5" />
          </div>
        </div>
      }
      footerContent={
        <div className="flex items-center justify-between text-[10px]">
          <span className="text-green-600 font-medium">⚡ Fast</span>
          <span className="text-muted-foreground">~100ms • ~1k tok</span>
        </div>
      }
    />
  );
}

export const VectorSearchNode = memo(VectorSearchNodeComponent);
export default VectorSearchNode;

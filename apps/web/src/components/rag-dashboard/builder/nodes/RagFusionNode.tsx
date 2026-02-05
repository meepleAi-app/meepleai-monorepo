'use client';

/**
 * RAG-Fusion Node - Specialized ReactFlow node
 *
 * Enhanced visualization for multi-query fusion retrieval.
 * Shows: number of queries, RRF K constant.
 *
 * @see #3466 - Implement Tier 3-4 experimental blocks
 */

import { memo } from 'react';

import { GitMerge, Layers, Search } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';

import { BaseBlockNode, type BaseBlockNodeProps } from './BaseBlockNode';

function RagFusionNodeComponent(props: BaseBlockNodeProps) {
  const { data } = props;
  const params = data.params as Record<string, unknown>;

  const numQueries = (params.numQueries as number) || 4;
  const rrfK = (params.rrfK as number) || 60;

  return (
    <BaseBlockNode
      {...props}
      headerContent={
        <div className="flex items-center gap-1.5">
          <GitMerge className="h-3.5 w-3.5 text-blue-600" />
          <span className="text-sm font-medium">RAG-Fusion</span>
        </div>
      }
      bodyContent={
        <div className="space-y-2">
          <p className="text-xs text-muted-foreground">
            Multiple queries + RRF fusion (+15-30%)
          </p>

          {/* Parameters */}
          <div className="grid grid-cols-2 gap-2 text-xs">
            <div className="flex items-center gap-1">
              <Layers className="h-3 w-3 text-muted-foreground" />
              <span className="text-muted-foreground">Queries:</span>
              <Badge variant="outline" className="text-[10px] px-1">
                {numQueries}
              </Badge>
            </div>
            <div className="flex items-center gap-1">
              <span className="text-muted-foreground">RRF K:</span>
              <Badge variant="secondary" className="text-[10px] px-1">
                {rrfK}
              </Badge>
            </div>
          </div>

          {/* Visual fusion representation */}
          <div className="p-2 bg-muted/50 rounded">
            <div className="flex items-center justify-center gap-2">
              <div className="flex flex-col gap-0.5">
                {Array.from({ length: Math.min(numQueries, 4) }).map((_, i) => (
                  <div
                    key={i}
                    className="flex items-center gap-1"
                  >
                    <Search className="h-2.5 w-2.5 text-blue-500" />
                    <div className="w-10 h-3 rounded bg-blue-100 dark:bg-blue-900/30 text-[7px] flex items-center justify-center text-blue-600">
                      Q{i + 1}
                    </div>
                  </div>
                ))}
              </div>
              <div className="flex flex-col items-center">
                <GitMerge className="h-4 w-4 text-blue-500" />
                <span className="text-[8px] text-blue-600">RRF</span>
              </div>
              <div className="w-12 h-8 rounded bg-blue-200 dark:bg-blue-800/50 flex items-center justify-center text-[8px] font-medium text-blue-700 dark:text-blue-300">
                Fused
              </div>
            </div>
          </div>
        </div>
      }
      footerContent={
        <div className="flex items-center justify-between text-[10px]">
          <span className="text-blue-600 font-medium">🔍 Comprehensive</span>
          <span className="text-muted-foreground">~1s • ~4k tok</span>
        </div>
      }
    />
  );
}

export const RagFusionNode = memo(RagFusionNodeComponent);
export default RagFusionNode;

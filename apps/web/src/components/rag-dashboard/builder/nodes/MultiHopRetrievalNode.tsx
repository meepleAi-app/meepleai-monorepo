'use client';

/**
 * Multi-Hop Retrieval Node - Specialized ReactFlow node
 *
 * Enhanced visualization for iterative retrieval following entity/concept chains.
 * Shows: number of hops, top-k per hop, traversal strategy.
 *
 * @see #3465 - Implement Tier 2 advanced blocks
 */

import { memo } from 'react';

import { ArrowRight, GitBranch, Layers } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';

import { BaseBlockNode, type BaseBlockNodeProps } from './BaseBlockNode';

function MultiHopRetrievalNodeComponent(props: BaseBlockNodeProps) {
  const { data } = props;
  const params = data.params as Record<string, unknown>;

  const hops = (params.hops as number) || 2;
  const topKPerHop = (params.topKPerHop as number) || 3;
  const strategy = (params.strategy as string) || 'breadth-first';

  // Display-friendly strategy name
  const strategyLabel = strategy === 'breadth-first' ? 'BFS' : 'DFS';

  return (
    <BaseBlockNode
      {...props}
      headerContent={
        <div className="flex items-center gap-1.5">
          <GitBranch className="h-3.5 w-3.5 text-green-600" />
          <span className="text-sm font-medium">Multi-Hop Retrieval</span>
        </div>
      }
      bodyContent={
        <div className="space-y-2">
          <p className="text-xs text-muted-foreground">
            Iterative retrieval following concept chains
          </p>

          {/* Parameters visualization */}
          <div className="grid grid-cols-2 gap-2 text-xs">
            <div className="flex items-center gap-1">
              <Layers className="h-3 w-3 text-muted-foreground" />
              <span className="text-muted-foreground">Hops:</span>
              <Badge variant="outline" className="text-[10px] px-1">
                {hops}
              </Badge>
            </div>
            <div className="flex items-center gap-1">
              <span className="text-muted-foreground">K/hop:</span>
              <Badge variant="outline" className="text-[10px] px-1">
                {topKPerHop}
              </Badge>
            </div>
          </div>

          {/* Strategy indicator */}
          <div className="flex items-center gap-2 text-xs">
            <span className="text-muted-foreground">Strategy:</span>
            <Badge variant="secondary" className="text-[10px] px-1.5">
              {strategyLabel}
            </Badge>
          </div>

          {/* Visual hop representation */}
          <div className="flex items-center justify-center gap-1 py-1">
            {Array.from({ length: hops }).map((_, i) => (
              <div key={i} className="flex items-center">
                <div className="w-6 h-6 rounded-full bg-green-100 dark:bg-green-900/30 flex items-center justify-center text-[10px] font-medium text-green-600">
                  {i + 1}
                </div>
                {i < hops - 1 && (
                  <ArrowRight className="h-3 w-3 text-muted-foreground mx-0.5" />
                )}
              </div>
            ))}
          </div>
        </div>
      }
      footerContent={
        <div className="flex items-center justify-between text-[10px]">
          <span className="text-amber-600 font-medium">🔄 Multi-Step</span>
          <span className="text-muted-foreground">~1.5s • ~5k tok</span>
        </div>
      }
    />
  );
}

export const MultiHopRetrievalNode = memo(MultiHopRetrievalNodeComponent);
export default MultiHopRetrievalNode;

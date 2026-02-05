'use client';

/**
 * Query Decomposition Node - Specialized ReactFlow node
 *
 * Enhanced visualization for breaking complex queries into sub-questions.
 * Shows: max sub-queries, parallel/sequential strategy.
 *
 * @see #3465 - Implement Tier 2 advanced blocks
 */

import { memo } from 'react';

import { GitFork, Layers, Workflow } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';

import { BaseBlockNode, type BaseBlockNodeProps } from './BaseBlockNode';

function QueryDecompositionNodeComponent(props: BaseBlockNodeProps) {
  const { data } = props;
  const params = data.params as Record<string, unknown>;

  const maxSubQueries = (params.maxSubQueries as number) || 5;
  const strategy = (params.strategy as string) || 'parallel';

  const isParallel = strategy === 'parallel';

  return (
    <BaseBlockNode
      {...props}
      headerContent={
        <div className="flex items-center gap-1.5">
          <GitFork className="h-3.5 w-3.5 text-blue-600" />
          <span className="text-sm font-medium">Query Decomposition</span>
        </div>
      }
      bodyContent={
        <div className="space-y-2">
          <p className="text-xs text-muted-foreground">
            Break complex query into sub-questions
          </p>

          {/* Parameters visualization */}
          <div className="grid grid-cols-2 gap-2 text-xs">
            <div className="flex items-center gap-1">
              <Layers className="h-3 w-3 text-muted-foreground" />
              <span className="text-muted-foreground">Max Q:</span>
              <Badge variant="outline" className="text-[10px] px-1">
                {maxSubQueries}
              </Badge>
            </div>
            <div className="flex items-center gap-1">
              <Workflow className="h-3 w-3 text-muted-foreground" />
              <Badge
                variant={isParallel ? 'default' : 'secondary'}
                className="text-[10px] px-1"
              >
                {isParallel ? '⚡ Parallel' : '→ Sequential'}
              </Badge>
            </div>
          </div>

          {/* Visual decomposition representation */}
          <div className="p-2 bg-muted/50 rounded">
            <div className="text-[10px] text-muted-foreground mb-1">Sub-questions:</div>
            <div className={`flex ${isParallel ? 'flex-row gap-1' : 'flex-col gap-0.5'}`}>
              {Array.from({ length: Math.min(maxSubQueries, 4) }).map((_, i) => (
                <div
                  key={i}
                  className="flex items-center gap-1 text-[9px] bg-background rounded px-1.5 py-0.5"
                >
                  <span className="text-blue-600 font-medium">Q{i + 1}</span>
                  {!isParallel && i < Math.min(maxSubQueries, 4) - 1 && (
                    <span className="text-muted-foreground">→</span>
                  )}
                </div>
              ))}
              {maxSubQueries > 4 && (
                <div className="text-[9px] text-muted-foreground px-1">
                  +{maxSubQueries - 4} more
                </div>
              )}
            </div>
          </div>
        </div>
      }
      footerContent={
        <div className="flex items-center justify-between text-[10px]">
          <span className="text-blue-600 font-medium">🔀 Decompose</span>
          <span className="text-muted-foreground">~300ms • ~600 tok</span>
        </div>
      }
    />
  );
}

export const QueryDecompositionNode = memo(QueryDecompositionNodeComponent);
export default QueryDecompositionNode;

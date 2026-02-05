'use client';

/**
 * Deduplication Node - Specialized ReactFlow node
 *
 * Enhanced visualization for removing duplicate/similar chunks.
 * Shows: similarity threshold, deduplication method.
 *
 * @see #3466 - Implement Tier 3-4 experimental blocks
 */

import { memo } from 'react';

import { Copy, Filter, Trash2 } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Progress } from '@/components/ui/feedback/progress';

import { BaseBlockNode, type BaseBlockNodeProps } from './BaseBlockNode';

function DeduplicationNodeComponent(props: BaseBlockNodeProps) {
  const { data } = props;
  const params = data.params as Record<string, unknown>;

  const similarityThreshold = (params.similarityThreshold as [number, number]) || [0.95, 1.0];
  const method = (params.method as string) || 'fuzzy';

  return (
    <BaseBlockNode
      {...props}
      headerContent={
        <div className="flex items-center gap-1.5">
          <Filter className="h-3.5 w-3.5 text-yellow-600" />
          <span className="text-sm font-medium">Deduplication</span>
        </div>
      }
      bodyContent={
        <div className="space-y-2">
          <p className="text-xs text-muted-foreground">
            Remove duplicate chunks (saves 10-30% tokens)
          </p>

          {/* Method */}
          <div className="flex items-center gap-2 text-xs">
            <Copy className="h-3 w-3 text-muted-foreground" />
            <span className="text-muted-foreground">Method:</span>
            <Badge
              variant={method === 'exact' ? 'outline' : 'secondary'}
              className="text-[10px] px-1"
            >
              {method === 'exact' ? '# Exact Hash' : '~ Fuzzy Match'}
            </Badge>
          </div>

          {/* Similarity threshold */}
          <div className="space-y-1">
            <div className="flex items-center justify-between text-[10px] text-muted-foreground">
              <span>Similarity Threshold</span>
              <span>{(similarityThreshold[0] * 100).toFixed(0)}%</span>
            </div>
            <Progress value={similarityThreshold[0] * 100} className="h-1.5" />
          </div>

          {/* Visual deduplication */}
          <div className="p-2 bg-muted/50 rounded">
            <div className="flex items-center justify-center gap-2 text-[9px]">
              <div className="flex flex-col gap-0.5">
                <div className="w-10 h-4 rounded bg-yellow-100 dark:bg-yellow-900/30 flex items-center justify-center text-yellow-700">
                  Doc A
                </div>
                <div className="w-10 h-4 rounded bg-yellow-100 dark:bg-yellow-900/30 flex items-center justify-center text-yellow-700 opacity-50">
                  Doc A&apos;
                </div>
                <div className="w-10 h-4 rounded bg-green-100 dark:bg-green-900/30 flex items-center justify-center text-green-700">
                  Doc B
                </div>
              </div>
              <div className="flex flex-col items-center">
                <Trash2 className="h-4 w-4 text-yellow-500" />
                <span className="text-yellow-600 font-medium">Dedup</span>
              </div>
              <div className="flex flex-col gap-0.5">
                <div className="w-10 h-4 rounded bg-yellow-200 dark:bg-yellow-800/50 flex items-center justify-center text-yellow-700 font-medium">
                  Doc A
                </div>
                <div className="w-10 h-4 rounded bg-green-200 dark:bg-green-800/50 flex items-center justify-center text-green-700 font-medium">
                  Doc B
                </div>
              </div>
            </div>
          </div>
        </div>
      }
      footerContent={
        <div className="flex items-center justify-between text-[10px]">
          <span className="text-yellow-600 font-medium">🗑️ Clean</span>
          <span className="text-muted-foreground">~50ms • 0 tok</span>
        </div>
      }
    />
  );
}

export const DeduplicationNode = memo(DeduplicationNodeComponent);
export default DeduplicationNode;

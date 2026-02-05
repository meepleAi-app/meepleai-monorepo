'use client';

/**
 * Query Rewriting Node - Specialized ReactFlow node
 *
 * Enhanced visualization for query rephrasing/expansion.
 * Shows: rewrite method, number of variations.
 *
 * @see #3465 - Implement Tier 2 advanced blocks
 */

import { memo } from 'react';

import { PenLine, RefreshCw, Sparkles } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';

import { BaseBlockNode, type BaseBlockNodeProps } from './BaseBlockNode';

function QueryRewritingNodeComponent(props: BaseBlockNodeProps) {
  const { data } = props;
  const params = data.params as Record<string, unknown>;

  const method = (params.method as string) || 'llm';
  const numVariations = (params.numVariations as number) || 3;

  // Display-friendly method name
  const methodLabel = method === 'llm' ? 'LLM Rewrite' : 'Template';

  return (
    <BaseBlockNode
      {...props}
      headerContent={
        <div className="flex items-center gap-1.5">
          <PenLine className="h-3.5 w-3.5 text-blue-600" />
          <span className="text-sm font-medium">Query Rewriting</span>
        </div>
      }
      bodyContent={
        <div className="space-y-2">
          <p className="text-xs text-muted-foreground">
            Rephrase query for better retrieval (+10-20%)
          </p>

          {/* Parameters visualization */}
          <div className="grid grid-cols-2 gap-2 text-xs">
            <div className="flex items-center gap-1">
              <Sparkles className="h-3 w-3 text-muted-foreground" />
              <span className="text-muted-foreground">Method:</span>
              <Badge variant="secondary" className="text-[10px] px-1">
                {methodLabel}
              </Badge>
            </div>
            <div className="flex items-center gap-1">
              <RefreshCw className="h-3 w-3 text-muted-foreground" />
              <span className="text-muted-foreground">Variants:</span>
              <Badge variant="outline" className="text-[10px] px-1">
                {numVariations}
              </Badge>
            </div>
          </div>

          {/* Visual variation representation */}
          <div className="space-y-1 p-2 bg-muted/50 rounded text-[10px]">
            <div className="text-muted-foreground italic">Original → {numVariations} variations</div>
            <div className="flex flex-wrap gap-1">
              {Array.from({ length: numVariations }).map((_, i) => (
                <Badge key={i} variant="outline" className="text-[9px] px-1 py-0">
                  v{i + 1}
                </Badge>
              ))}
            </div>
          </div>
        </div>
      }
      footerContent={
        <div className="flex items-center justify-between text-[10px]">
          <span className="text-blue-600 font-medium">✏️ Optimize</span>
          <span className="text-muted-foreground">~200ms • ~400 tok</span>
        </div>
      }
    />
  );
}

export const QueryRewritingNode = memo(QueryRewritingNodeComponent);
export default QueryRewritingNode;

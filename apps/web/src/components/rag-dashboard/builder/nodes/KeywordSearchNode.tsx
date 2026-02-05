'use client';

/**
 * Keyword Search Node - Specialized ReactFlow node
 *
 * Enhanced visualization for BM25 keyword matching.
 * Shows: top-k setting, boost factors.
 *
 * @see #3458 - Implement Tier 1 blocks (7 essential RAG blocks)
 */

import { memo } from 'react';

import { FileText, Hash } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';

import { BaseBlockNode, type BaseBlockNodeProps } from './BaseBlockNode';

function KeywordSearchNodeComponent(props: BaseBlockNodeProps) {
  const { data } = props;
  const params = data.params as Record<string, unknown>;

  const topK = (params.topK as number) || 10;
  const boostFactors = (params.boostFactors as string) || 'balanced';

  const boostLabel = {
    balanced: '⚖️ Balanced',
    'title-heavy': '📑 Title Heavy',
    'content-heavy': '📄 Content Heavy',
  }[boostFactors] || boostFactors;

  return (
    <BaseBlockNode
      {...props}
      headerContent={
        <div className="flex items-center gap-1.5">
          <FileText className="h-3.5 w-3.5 text-green-600" />
          <span className="text-sm font-medium">Keyword Search</span>
        </div>
      }
      bodyContent={
        <div className="space-y-2">
          <p className="text-xs text-muted-foreground">
            BM25 keyword matching for exact terms
          </p>

          {/* Parameters visualization */}
          <div className="flex flex-wrap gap-2 text-xs">
            <div className="flex items-center gap-1">
              <Hash className="h-3 w-3 text-muted-foreground" />
              <span className="text-muted-foreground">Top K:</span>
              <Badge variant="outline" className="text-[10px] px-1">
                {topK}
              </Badge>
            </div>
          </div>

          {/* Boost factors */}
          <div className="flex items-center gap-1 text-xs">
            <span className="text-muted-foreground">Boost:</span>
            <Badge variant="secondary" className="text-[10px] px-1.5">
              {boostLabel}
            </Badge>
          </div>
        </div>
      }
      footerContent={
        <div className="flex items-center justify-between text-[10px]">
          <span className="text-green-600 font-medium">⚡ Fastest</span>
          <span className="text-muted-foreground">~50ms • ~500 tok</span>
        </div>
      }
    />
  );
}

export const KeywordSearchNode = memo(KeywordSearchNodeComponent);
export default KeywordSearchNode;

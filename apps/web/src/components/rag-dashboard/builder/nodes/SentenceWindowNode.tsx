'use client';

/**
 * Sentence Window Node - Specialized ReactFlow node
 *
 * Enhanced visualization for sentence-level retrieval with context expansion.
 * Shows: search chunk size, context window size, overlap.
 *
 * @see #3466 - Implement Tier 3-4 experimental blocks
 */

import { memo } from 'react';

import { FileText, Maximize2, MoveHorizontal } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';

import { BaseBlockNode, type BaseBlockNodeProps } from './BaseBlockNode';

function SentenceWindowNodeComponent(props: BaseBlockNodeProps) {
  const { data } = props;
  const params = data.params as Record<string, unknown>;

  const searchChunkSize = (params.searchChunkSize as number) || 128;
  const contextWindowSize = (params.contextWindowSize as number) || 512;
  const overlap = (params.overlap as number) || 32;

  return (
    <BaseBlockNode
      {...props}
      headerContent={
        <div className="flex items-center gap-1.5">
          <FileText className="h-3.5 w-3.5 text-green-600" />
          <span className="text-sm font-medium">Sentence Window</span>
        </div>
      }
      bodyContent={
        <div className="space-y-2">
          <p className="text-xs text-muted-foreground">
            Small chunks for search, expand to larger context (+10-15%)
          </p>

          {/* Parameters */}
          <div className="grid grid-cols-3 gap-1 text-xs">
            <div className="flex flex-col items-center p-1 bg-muted/50 rounded">
              <span className="text-[8px] text-muted-foreground">Search</span>
              <Badge variant="outline" className="text-[9px] px-1">
                {searchChunkSize}
              </Badge>
            </div>
            <div className="flex flex-col items-center p-1 bg-muted/50 rounded">
              <span className="text-[8px] text-muted-foreground">Context</span>
              <Badge variant="secondary" className="text-[9px] px-1">
                {contextWindowSize}
              </Badge>
            </div>
            <div className="flex flex-col items-center p-1 bg-muted/50 rounded">
              <span className="text-[8px] text-muted-foreground">Overlap</span>
              <Badge variant="outline" className="text-[9px] px-1">
                {overlap}
              </Badge>
            </div>
          </div>

          {/* Visual window expansion */}
          <div className="p-2 bg-muted/50 rounded">
            <div className="flex items-center justify-center gap-2">
              {/* Small search chunk */}
              <div className="flex flex-col items-center">
                <div className="w-8 h-8 border-2 border-green-500 rounded flex items-center justify-center text-[8px] text-green-600 font-medium bg-green-50 dark:bg-green-900/30">
                  {searchChunkSize}
                </div>
                <span className="text-[8px] text-muted-foreground mt-0.5">Search</span>
              </div>

              <MoveHorizontal className="h-4 w-4 text-green-400" />

              {/* Expanded context */}
              <div className="flex flex-col items-center">
                <div className="w-16 h-8 border-2 border-dashed border-green-300 rounded flex items-center justify-center text-[8px] text-green-600 bg-green-50/50 dark:bg-green-900/20">
                  <Maximize2 className="h-3 w-3 mr-0.5" />
                  {contextWindowSize}
                </div>
                <span className="text-[8px] text-muted-foreground mt-0.5">Context</span>
              </div>
            </div>
          </div>
        </div>
      }
      footerContent={
        <div className="flex items-center justify-between text-[10px]">
          <span className="text-green-600 font-medium">📋 Precise</span>
          <span className="text-muted-foreground">~150ms • ~2k tok</span>
        </div>
      }
    />
  );
}

export const SentenceWindowNode = memo(SentenceWindowNodeComponent);
export default SentenceWindowNode;

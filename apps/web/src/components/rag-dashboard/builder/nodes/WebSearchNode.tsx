'use client';

/**
 * Web Search Node - Specialized ReactFlow node
 *
 * Enhanced visualization for external web search fallback.
 * Shows: max queries, source filters, fallback threshold.
 *
 * @see #3466 - Implement Tier 3-4 experimental blocks
 */

import { memo } from 'react';

import { Globe, Search, Wifi } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Progress } from '@/components/ui/feedback/progress';

import { BaseBlockNode, type BaseBlockNodeProps } from './BaseBlockNode';

function WebSearchNodeComponent(props: BaseBlockNodeProps) {
  const { data } = props;
  const params = data.params as Record<string, unknown>;

  const maxQueries = (params.maxQueries as number) || 3;
  const sources = (params.sources as string) || 'all';
  const fallbackThreshold = (params.fallbackThreshold as [number, number]) || [0.5, 1.0];

  // Source display
  const sourceLabels: Record<string, string> = {
    all: 'All Sources',
    official: 'Official',
    academic: 'Academic',
  };

  return (
    <BaseBlockNode
      {...props}
      headerContent={
        <div className="flex items-center gap-1.5">
          <Globe className="h-3.5 w-3.5 text-green-600" />
          <span className="text-sm font-medium">Web Search</span>
        </div>
      }
      bodyContent={
        <div className="space-y-2">
          <p className="text-xs text-muted-foreground">
            External web search when corpus insufficient
          </p>

          {/* Parameters */}
          <div className="grid grid-cols-2 gap-2 text-xs">
            <div className="flex items-center gap-1">
              <Search className="h-3 w-3 text-muted-foreground" />
              <span className="text-muted-foreground">Queries:</span>
              <Badge variant="outline" className="text-[10px] px-1">
                {maxQueries}
              </Badge>
            </div>
            <div className="flex items-center gap-1">
              <Wifi className="h-3 w-3 text-muted-foreground" />
              <Badge variant="secondary" className="text-[10px] px-1 truncate max-w-[70px]">
                {sourceLabels[sources]}
              </Badge>
            </div>
          </div>

          {/* Fallback threshold visualization */}
          <div className="space-y-1">
            <div className="flex items-center justify-between text-[10px] text-muted-foreground">
              <span>Fallback Threshold</span>
              <span>{(fallbackThreshold[0] * 100).toFixed(0)}%</span>
            </div>
            <Progress value={fallbackThreshold[0] * 100} className="h-1.5" />
          </div>

          {/* Visual web search */}
          <div className="p-2 bg-muted/50 rounded text-[9px]">
            <div className="flex items-center gap-2 justify-center">
              <div className="flex flex-col items-center">
                <span className="text-muted-foreground">Corpus</span>
                <span className="text-red-500">❌ Low</span>
              </div>
              <span className="text-green-500">→</span>
              <div className="flex flex-col items-center">
                <Globe className="h-4 w-4 text-green-500" />
                <span className="text-green-600 font-medium">Web</span>
              </div>
              <span className="text-green-500">→</span>
              <div className="flex flex-col items-center">
                <span className="text-muted-foreground">Results</span>
                <span className="text-green-500">✅</span>
              </div>
            </div>
          </div>
        </div>
      }
      footerContent={
        <div className="flex items-center justify-between text-[10px]">
          <span className="text-amber-600 font-medium">🌐 External</span>
          <span className="text-muted-foreground">~3s • ~3.5k tok</span>
        </div>
      }
    />
  );
}

export const WebSearchNode = memo(WebSearchNodeComponent);
export default WebSearchNode;

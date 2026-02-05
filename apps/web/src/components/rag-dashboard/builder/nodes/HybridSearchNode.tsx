'use client';

/**
 * Hybrid Search Node - Specialized ReactFlow node
 *
 * Enhanced visualization for combined dense+sparse retrieval.
 * Shows: alpha balance, fusion method, top-k settings.
 *
 * @see #3458 - Implement Tier 1 blocks (7 essential RAG blocks)
 */

import { memo } from 'react';

import { Combine, Scale } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';

import { BaseBlockNode, type BaseBlockNodeProps } from './BaseBlockNode';

function HybridSearchNodeComponent(props: BaseBlockNodeProps) {
  const { data } = props;
  const params = data.params as Record<string, unknown>;

  const alpha = (params.alpha as [number, number]) || [0.5, 0.5];
  const vectorTopK = (params.vectorTopK as number) || 10;
  const keywordTopK = (params.keywordTopK as number) || 10;
  const fusionMethod = (params.fusionMethod as string) || 'rrf';

  // Calculate visual balance (0 = keyword, 1 = vector)
  const vectorWeight = alpha[0];
  const keywordWeight = 1 - vectorWeight;

  return (
    <BaseBlockNode
      {...props}
      headerContent={
        <div className="flex items-center gap-1.5">
          <Combine className="h-3.5 w-3.5 text-green-600" />
          <span className="text-sm font-medium">Hybrid Search</span>
        </div>
      }
      bodyContent={
        <div className="space-y-2">
          <p className="text-xs text-muted-foreground">
            Dense + sparse fusion (+35-48% improvement)
          </p>

          {/* Alpha balance visualization */}
          <div className="space-y-1">
            <div className="flex items-center justify-between text-[10px] text-muted-foreground">
              <span>Keyword</span>
              <Scale className="h-3 w-3" />
              <span>Vector</span>
            </div>
            <div className="flex h-2 rounded overflow-hidden">
              <div
                className="bg-blue-400 transition-all"
                style={{ width: `${keywordWeight * 100}%` }}
              />
              <div
                className="bg-green-500 transition-all"
                style={{ width: `${vectorWeight * 100}%` }}
              />
            </div>
            <div className="flex justify-between text-[10px] text-muted-foreground">
              <span>{(keywordWeight * 100).toFixed(0)}%</span>
              <span>{(vectorWeight * 100).toFixed(0)}%</span>
            </div>
          </div>

          {/* Parameters */}
          <div className="grid grid-cols-2 gap-1.5 text-xs">
            <div className="flex items-center gap-1">
              <span className="text-muted-foreground">Vec K:</span>
              <Badge variant="outline" className="text-[10px] px-1">
                {vectorTopK}
              </Badge>
            </div>
            <div className="flex items-center gap-1">
              <span className="text-muted-foreground">Key K:</span>
              <Badge variant="outline" className="text-[10px] px-1">
                {keywordTopK}
              </Badge>
            </div>
          </div>

          {/* Fusion method */}
          <Badge variant="secondary" className="text-[10px]">
            {fusionMethod === 'rrf' ? '🔄 RRF Fusion' : '➕ Weighted Sum'}
          </Badge>
        </div>
      }
      footerContent={
        <div className="flex items-center justify-between text-[10px]">
          <span className="text-blue-600 font-medium">⚡ Standard</span>
          <span className="text-muted-foreground">~200ms • ~1.5k tok</span>
        </div>
      }
    />
  );
}

export const HybridSearchNode = memo(HybridSearchNodeComponent);
export default HybridSearchNode;

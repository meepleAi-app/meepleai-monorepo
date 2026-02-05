'use client';

/**
 * HyDE Node - Specialized ReactFlow node
 *
 * Enhanced visualization for Hypothetical Document Embeddings.
 * Shows: generation model, embedding usage.
 *
 * @see #3466 - Implement Tier 3-4 experimental blocks
 */

import { memo } from 'react';

import { FileText, Sparkles, Target } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';

import { BaseBlockNode, type BaseBlockNodeProps } from './BaseBlockNode';

function HydeNodeComponent(props: BaseBlockNodeProps) {
  const { data } = props;
  const params = data.params as Record<string, unknown>;

  const model = (params.model as string) || 'claude-3-haiku';
  const useForSearch = (params.useForSearch as boolean) ?? true;

  // Shorten model name
  const shortModel = model.replace('claude-3-', 'c3-').replace('gpt-4o-', 'gpt-').replace('llama-3.3-', 'llama-');

  return (
    <BaseBlockNode
      {...props}
      headerContent={
        <div className="flex items-center gap-1.5">
          <Target className="h-3.5 w-3.5 text-blue-600" />
          <span className="text-sm font-medium">HyDE</span>
        </div>
      }
      bodyContent={
        <div className="space-y-2">
          <p className="text-xs text-muted-foreground">
            Generate hypothetical answer, use for retrieval (+15-25%)
          </p>

          {/* Parameters */}
          <div className="grid grid-cols-2 gap-2 text-xs">
            <div className="flex items-center gap-1">
              <Sparkles className="h-3 w-3 text-muted-foreground" />
              <span className="text-muted-foreground">Model:</span>
              <Badge variant="secondary" className="text-[10px] px-1 truncate max-w-[60px]">
                {shortModel}
              </Badge>
            </div>
            <div className="flex items-center gap-1">
              <span className="text-muted-foreground">Search:</span>
              <Badge
                variant={useForSearch ? 'default' : 'outline'}
                className="text-[10px] px-1"
              >
                {useForSearch ? 'Yes' : 'No'}
              </Badge>
            </div>
          </div>

          {/* Visual HyDE flow */}
          <div className="p-2 bg-muted/50 rounded">
            <div className="flex items-center justify-center gap-1 text-[9px]">
              <div className="flex flex-col items-center">
                <span className="text-muted-foreground">Query</span>
                <div className="w-8 h-4 rounded bg-blue-100 dark:bg-blue-900/30" />
              </div>
              <span className="text-blue-500">→</span>
              <div className="flex flex-col items-center">
                <Sparkles className="h-3 w-3 text-blue-500" />
                <span className="text-blue-600 font-medium">LLM</span>
              </div>
              <span className="text-blue-500">→</span>
              <div className="flex flex-col items-center">
                <FileText className="h-3 w-3 text-blue-500" />
                <span className="text-muted-foreground">Fake Doc</span>
              </div>
              <span className="text-blue-500">→</span>
              <div className="flex flex-col items-center">
                <span className="text-muted-foreground">Embed</span>
                <div className="w-8 h-4 rounded bg-purple-100 dark:bg-purple-900/30" />
              </div>
            </div>
          </div>
        </div>
      }
      footerContent={
        <div className="flex items-center justify-between text-[10px]">
          <span className="text-amber-600 font-medium">💰 High Cost</span>
          <span className="text-muted-foreground">~800ms • ~1.5k tok</span>
        </div>
      }
    />
  );
}

export const HydeNode = memo(HydeNodeComponent);
export default HydeNode;

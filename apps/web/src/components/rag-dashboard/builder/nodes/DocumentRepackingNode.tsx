'use client';

/**
 * Document Repacking Node - Specialized ReactFlow node
 *
 * Enhanced visualization for reordering documents to optimize LLM attention.
 * Shows: repack method (reverse, sandwich, interleaved).
 *
 * @see #3465 - Implement Tier 2 advanced blocks
 */

import { memo } from 'react';

import { ArrowDownUp, PackageCheck } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';

import { BaseBlockNode, type BaseBlockNodeProps } from './BaseBlockNode';

function DocumentRepackingNodeComponent(props: BaseBlockNodeProps) {
  const { data } = props;
  const params = data.params as Record<string, unknown>;

  const method = (params.method as string) || 'reverse';

  // Method display info
  const methodInfo: Record<string, { label: string; icon: string; description: string }> = {
    reverse: {
      label: 'Reverse',
      icon: '⬆️',
      description: 'Best docs first & last'
    },
    sandwich: {
      label: 'Sandwich',
      icon: '🥪',
      description: 'Best at edges'
    },
    interleaved: {
      label: 'Interleaved',
      icon: '🔀',
      description: 'Alternating quality'
    },
  };

  const info = methodInfo[method] || methodInfo.reverse;

  // Visual representation of doc order
  const getDocOrder = (m: string) => {
    switch (m) {
      case 'reverse':
        return ['1', '5', '4', '3', '2', '1'];
      case 'sandwich':
        return ['1', '2', '5', '4', '3', '1'];
      case 'interleaved':
        return ['1', '3', '5', '4', '2', '1'];
      default:
        return ['1', '2', '3', '4', '5'];
    }
  };

  const docOrder = getDocOrder(method);

  return (
    <BaseBlockNode
      {...props}
      headerContent={
        <div className="flex items-center gap-1.5">
          <PackageCheck className="h-3.5 w-3.5 text-yellow-600" />
          <span className="text-sm font-medium">Document Repacking</span>
        </div>
      }
      bodyContent={
        <div className="space-y-2">
          <p className="text-xs text-muted-foreground">
            Reorder docs for LLM attention (+5-12%)
          </p>

          {/* Method selection indicator */}
          <div className="flex items-center gap-2 text-xs">
            <ArrowDownUp className="h-3 w-3 text-muted-foreground" />
            <span className="text-muted-foreground">Method:</span>
            <Badge variant="secondary" className="text-[10px] px-1.5">
              {info.icon} {info.label}
            </Badge>
          </div>

          {/* Visual reordering representation */}
          <div className="p-2 bg-muted/50 rounded">
            <div className="text-[10px] text-muted-foreground mb-1">{info.description}</div>
            <div className="flex items-center justify-center gap-0.5">
              {docOrder.slice(0, 5).map((rank, i) => (
                <div
                  key={i}
                  className={`w-5 h-5 rounded flex items-center justify-center text-[9px] font-medium ${
                    rank === '1'
                      ? 'bg-green-100 dark:bg-green-900/30 text-green-600'
                      : rank === '2'
                        ? 'bg-blue-100 dark:bg-blue-900/30 text-blue-600'
                        : 'bg-muted text-muted-foreground'
                  }`}
                >
                  {rank}
                </div>
              ))}
            </div>
            <div className="text-[9px] text-center text-muted-foreground mt-1">
              Position in context window
            </div>
          </div>
        </div>
      }
      footerContent={
        <div className="flex items-center justify-between text-[10px]">
          <span className="text-yellow-600 font-medium">⚡ Instant</span>
          <span className="text-muted-foreground">~5ms • 0 tok</span>
        </div>
      }
    />
  );
}

export const DocumentRepackingNode = memo(DocumentRepackingNodeComponent);
export default DocumentRepackingNode;

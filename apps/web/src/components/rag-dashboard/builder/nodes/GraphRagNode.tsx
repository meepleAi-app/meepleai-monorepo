'use client';

/**
 * GraphRAG Node - Specialized ReactFlow node
 *
 * Enhanced visualization for knowledge graph traversal.
 * Shows: max depth, relationship types.
 *
 * @see #3466 - Implement Tier 3-4 experimental blocks
 */

import { memo } from 'react';

import { GitBranch, Share2 } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';

import { BaseBlockNode, type BaseBlockNodeProps } from './BaseBlockNode';

function GraphRagNodeComponent(props: BaseBlockNodeProps) {
  const { data } = props;
  const params = data.params as Record<string, unknown>;

  const maxDepth = (params.maxDepth as number) || 2;
  const relationshipTypes = (params.relationshipTypes as string) || 'all';

  // Relationship type display
  const relLabels: Record<string, string> = {
    all: 'All',
    hierarchical: 'Hierarchical',
    semantic: 'Semantic',
  };

  return (
    <BaseBlockNode
      {...props}
      headerContent={
        <div className="flex items-center gap-1.5">
          <Share2 className="h-3.5 w-3.5 text-green-600" />
          <span className="text-sm font-medium">GraphRAG</span>
        </div>
      }
      bodyContent={
        <div className="space-y-2">
          <p className="text-xs text-muted-foreground">
            Knowledge graph traversal (up to 99% precision)
          </p>

          {/* Parameters */}
          <div className="grid grid-cols-2 gap-2 text-xs">
            <div className="flex items-center gap-1">
              <GitBranch className="h-3 w-3 text-muted-foreground" />
              <span className="text-muted-foreground">Depth:</span>
              <Badge variant="outline" className="text-[10px] px-1">
                {maxDepth}
              </Badge>
            </div>
            <div className="flex items-center gap-1">
              <span className="text-muted-foreground">Rels:</span>
              <Badge variant="secondary" className="text-[10px] px-1">
                {relLabels[relationshipTypes]}
              </Badge>
            </div>
          </div>

          {/* Visual graph representation */}
          <div className="p-2 bg-muted/50 rounded">
            <div className="flex items-center justify-center">
              <svg width="100" height="50" className="text-green-500">
                {/* Central node */}
                <circle cx="50" cy="25" r="6" fill="currentColor" />

                {/* Connected nodes - depth 1 */}
                <circle cx="20" cy="15" r="4" fill="currentColor" opacity="0.7" />
                <circle cx="80" cy="15" r="4" fill="currentColor" opacity="0.7" />
                <circle cx="30" cy="40" r="4" fill="currentColor" opacity="0.7" />
                <circle cx="70" cy="40" r="4" fill="currentColor" opacity="0.7" />

                {/* Edges */}
                <line x1="50" y1="25" x2="20" y2="15" stroke="currentColor" strokeWidth="1" opacity="0.5" />
                <line x1="50" y1="25" x2="80" y2="15" stroke="currentColor" strokeWidth="1" opacity="0.5" />
                <line x1="50" y1="25" x2="30" y2="40" stroke="currentColor" strokeWidth="1" opacity="0.5" />
                <line x1="50" y1="25" x2="70" y2="40" stroke="currentColor" strokeWidth="1" opacity="0.5" />

                {/* Depth 2 nodes if maxDepth >= 2 */}
                {maxDepth >= 2 && (
                  <>
                    <circle cx="10" cy="5" r="3" fill="currentColor" opacity="0.4" />
                    <circle cx="90" cy="5" r="3" fill="currentColor" opacity="0.4" />
                    <line x1="20" y1="15" x2="10" y2="5" stroke="currentColor" strokeWidth="1" opacity="0.3" />
                    <line x1="80" y1="15" x2="90" y2="5" stroke="currentColor" strokeWidth="1" opacity="0.3" />
                  </>
                )}
              </svg>
            </div>
            <div className="text-[9px] text-center text-muted-foreground">
              Entity relationship traversal
            </div>
          </div>
        </div>
      }
      footerContent={
        <div className="flex items-center justify-between text-[10px]">
          <span className="text-green-600 font-medium">🕸️ Graph</span>
          <span className="text-muted-foreground">~500ms • ~3k tok</span>
        </div>
      }
    />
  );
}

export const GraphRagNode = memo(GraphRagNodeComponent);
export default GraphRagNode;

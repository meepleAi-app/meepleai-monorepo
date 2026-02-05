'use client';

/**
 * Metadata Filtering Node - Specialized ReactFlow node
 *
 * Enhanced visualization for pre-filtering documents by metadata.
 * Shows: filter fields, combine mode (AND/OR).
 *
 * @see #3465 - Implement Tier 2 advanced blocks
 */

import { memo } from 'react';

import { Filter, Tag, ToggleLeft } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';

import { BaseBlockNode, type BaseBlockNodeProps } from './BaseBlockNode';

function MetadataFilteringNodeComponent(props: BaseBlockNodeProps) {
  const { data } = props;
  const params = data.params as Record<string, unknown>;

  const filters = (params.filters as string) || 'category';
  const combineMode = (params.combineMode as string) || 'and';

  // Filter field display
  const filterLabels: Record<string, { label: string; icon: string }> = {
    category: { label: 'Category', icon: '📁' },
    date: { label: 'Date', icon: '📅' },
    author: { label: 'Author', icon: '👤' },
    language: { label: 'Language', icon: '🌐' },
  };

  const filterInfo = filterLabels[filters] || { label: filters, icon: '🏷️' };

  return (
    <BaseBlockNode
      {...props}
      headerContent={
        <div className="flex items-center gap-1.5">
          <Filter className="h-3.5 w-3.5 text-yellow-600" />
          <span className="text-sm font-medium">Metadata Filter</span>
        </div>
      }
      bodyContent={
        <div className="space-y-2">
          <p className="text-xs text-muted-foreground">
            Pre-filter docs by metadata before retrieval
          </p>

          {/* Parameters visualization */}
          <div className="grid grid-cols-2 gap-2 text-xs">
            <div className="flex items-center gap-1">
              <Tag className="h-3 w-3 text-muted-foreground" />
              <span className="text-muted-foreground">Field:</span>
              <Badge variant="outline" className="text-[10px] px-1">
                {filterInfo.icon} {filterInfo.label}
              </Badge>
            </div>
            <div className="flex items-center gap-1">
              <ToggleLeft className="h-3 w-3 text-muted-foreground" />
              <Badge
                variant={combineMode === 'and' ? 'default' : 'secondary'}
                className="text-[10px] px-1"
              >
                {combineMode.toUpperCase()}
              </Badge>
            </div>
          </div>

          {/* Visual filter representation */}
          <div className="p-2 bg-muted/50 rounded text-[10px]">
            <div className="flex items-center gap-2">
              <span className="text-muted-foreground">Docs</span>
              <span className="text-yellow-600">→</span>
              <div className="flex items-center gap-1 bg-yellow-100 dark:bg-yellow-900/30 px-1.5 py-0.5 rounded">
                <Filter className="h-2.5 w-2.5 text-yellow-600" />
                <span className="text-yellow-700 dark:text-yellow-400">{filterInfo.label}</span>
              </div>
              <span className="text-yellow-600">→</span>
              <span className="text-muted-foreground">Filtered</span>
            </div>
          </div>
        </div>
      }
      footerContent={
        <div className="flex items-center justify-between text-[10px]">
          <span className="text-yellow-600 font-medium">⚡ Instant</span>
          <span className="text-muted-foreground">~10ms • 0 tok</span>
        </div>
      }
    />
  );
}

export const MetadataFilteringNode = memo(MetadataFilteringNodeComponent);
export default MetadataFilteringNode;

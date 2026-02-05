'use client';

/**
 * RAG Pipeline Builder - Block Palette
 *
 * Scrollable sidebar with categorized blocks for drag-and-drop into canvas.
 * Features: search, categories, drag handles, tier indicators.
 *
 * @see #3456 - Implement block palette UI component with 23 blocks
 */

import { useState, useMemo, useCallback } from 'react';

import { Search, ChevronDown, ChevronRight, Lock, Zap, Clock, Coins } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import {
  Collapsible,
  CollapsibleContent,
  CollapsibleTrigger,
} from '@/components/ui/data-display/collapsible';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
import { Input } from '@/components/ui/primitives/input';
import { ScrollArea } from '@/components/ui/primitives/scroll-area';
import { cn } from '@/lib/utils';

import { generatePaletteGroups, filterPaletteGroups, CATEGORY_META, getPerformanceTier } from './block-metadata';

import type { RagBlock, UserTier, PaletteGroup } from './types';


// =============================================================================
// Types
// =============================================================================

export interface BlockPaletteProps {
  /** Current user tier for access control */
  userTier: UserTier;
  /** Callback when block drag starts */
  onBlockDragStart?: (block: RagBlock, event: React.DragEvent) => void;
  /** Callback when block is clicked (for mobile/accessibility) */
  onBlockClick?: (block: RagBlock) => void;
  /** Additional class names */
  className?: string;
  /** Whether palette is collapsed */
  collapsed?: boolean;
  /** Callback when collapse state changes */
  onCollapseChange?: (collapsed: boolean) => void;
}

// =============================================================================
// Sub-Components
// =============================================================================

interface BlockItemProps {
  block: RagBlock;
  disabled?: boolean;
  disabledReason?: string;
  onDragStart?: (block: RagBlock, event: React.DragEvent) => void;
  onClick?: (block: RagBlock) => void;
}

function BlockItem({
  block,
  disabled,
  disabledReason,
  onDragStart,
  onClick,
}: BlockItemProps) {
  const performanceTier = getPerformanceTier(block.type);

  const handleDragStart = useCallback(
    (e: React.DragEvent) => {
      if (disabled) {
        e.preventDefault();
        return;
      }
      // Set drag data
      e.dataTransfer.setData('application/rag-block', JSON.stringify({
        type: block.type,
        id: block.id,
      }));
      e.dataTransfer.effectAllowed = 'copy';
      onDragStart?.(block, e);
    },
    [block, disabled, onDragStart]
  );

  const handleClick = useCallback(() => {
    if (!disabled) {
      onClick?.(block);
    }
  }, [block, disabled, onClick]);

  const performanceIcon = {
    fast: <Zap className="h-3 w-3 text-green-500" />,
    standard: <Clock className="h-3 w-3 text-blue-500" />,
    slow: <Clock className="h-3 w-3 text-yellow-500" />,
    expensive: <Coins className="h-3 w-3 text-red-500" />,
  }[performanceTier];

  return (
    <TooltipProvider>
      <Tooltip>
        <TooltipTrigger asChild>
          <div
            draggable={!disabled}
            onDragStart={handleDragStart}
            onClick={handleClick}
            className={cn(
              'flex items-center gap-2 p-2 rounded-md border transition-all',
              'hover:border-primary/50 hover:bg-accent/50',
              'cursor-grab active:cursor-grabbing',
              disabled && 'opacity-50 cursor-not-allowed hover:border-border hover:bg-transparent'
            )}
            style={{
              borderLeftColor: block.color,
              borderLeftWidth: '3px',
            }}
            role="button"
            aria-disabled={disabled}
            tabIndex={disabled ? -1 : 0}
            onKeyDown={(e) => {
              if (e.key === 'Enter' || e.key === ' ') {
                handleClick();
              }
            }}
          >
            <span className="text-lg flex-shrink-0" aria-hidden="true">
              {block.icon}
            </span>
            <div className="flex-1 min-w-0">
              <div className="flex items-center gap-1">
                <span className="text-sm font-medium truncate">{block.name}</span>
                {disabled && <Lock className="h-3 w-3 text-muted-foreground" />}
              </div>
            </div>
            <div className="flex items-center gap-1 flex-shrink-0">
              {performanceIcon}
              {block.requiredTier !== 'User' && (
                <Badge
                  variant="outline"
                  className={cn(
                    'text-[10px] px-1 py-0',
                    block.requiredTier === 'Admin' && 'border-red-500/50 text-red-500',
                    block.requiredTier === 'Editor' && 'border-yellow-500/50 text-yellow-500'
                  )}
                >
                  {block.requiredTier}
                </Badge>
              )}
            </div>
          </div>
        </TooltipTrigger>
        <TooltipContent side="right" className="max-w-xs">
          <div className="space-y-1.5">
            <p className="font-medium">{block.name}</p>
            <p className="text-xs text-muted-foreground">{block.description}</p>
            {disabled && disabledReason && (
              <p className="text-xs text-red-500">{disabledReason}</p>
            )}
            <div className="flex gap-2 text-xs text-muted-foreground pt-1 border-t">
              <span>~{block.estimatedTokens} tokens</span>
              <span>~{block.estimatedLatencyMs}ms</span>
              <span>${block.estimatedCost.toFixed(4)}</span>
            </div>
          </div>
        </TooltipContent>
      </Tooltip>
    </TooltipProvider>
  );
}

interface CategoryGroupProps {
  group: PaletteGroup;
  onBlockDragStart?: (block: RagBlock, event: React.DragEvent) => void;
  onBlockClick?: (block: RagBlock) => void;
}

function CategoryGroup({ group, onBlockDragStart, onBlockClick }: CategoryGroupProps) {
  const [isOpen, setIsOpen] = useState(true);
  const enabledCount = group.items.filter((item) => !item.disabled).length;

  return (
    <Collapsible open={isOpen} onOpenChange={setIsOpen}>
      <CollapsibleTrigger className="flex items-center gap-2 w-full p-2 hover:bg-accent/50 rounded-md transition-colors">
        {isOpen ? (
          <ChevronDown className="h-4 w-4 text-muted-foreground" />
        ) : (
          <ChevronRight className="h-4 w-4 text-muted-foreground" />
        )}
        <span className="text-lg" aria-hidden="true">
          {group.icon}
        </span>
        <span className="font-medium text-sm flex-1 text-left">{group.label}</span>
        <Badge variant="secondary" className="text-xs">
          {enabledCount}/{group.items.length}
        </Badge>
      </CollapsibleTrigger>
      <CollapsibleContent>
        <div className="pl-6 pr-2 pb-2 space-y-1">
          {group.items.map((item) => (
            <BlockItem
              key={item.block.id}
              block={item.block}
              disabled={item.disabled}
              disabledReason={item.disabledReason}
              onDragStart={onBlockDragStart}
              onClick={onBlockClick}
            />
          ))}
        </div>
      </CollapsibleContent>
    </Collapsible>
  );
}

// =============================================================================
// Main Component
// =============================================================================

export function BlockPalette({
  userTier,
  onBlockDragStart,
  onBlockClick,
  className,
  collapsed = false,
  onCollapseChange,
}: BlockPaletteProps) {
  const [searchQuery, setSearchQuery] = useState('');

  // Generate palette groups based on user tier
  const paletteGroups = useMemo(
    () => generatePaletteGroups(userTier),
    [userTier]
  );

  // Filter groups by search query
  const filteredGroups = useMemo(
    () => filterPaletteGroups(paletteGroups, searchQuery),
    [paletteGroups, searchQuery]
  );

  // Calculate stats
  const totalBlocks = paletteGroups.reduce((sum, g) => sum + g.items.length, 0);
  const availableBlocks = paletteGroups.reduce(
    (sum, g) => sum + g.items.filter((i) => !i.disabled).length,
    0
  );

  if (collapsed) {
    return (
      <div
        className={cn(
          'w-12 bg-card border-r flex flex-col items-center py-4 gap-2',
          className
        )}
      >
        <button
          onClick={() => onCollapseChange?.(false)}
          className="p-2 hover:bg-accent rounded-md transition-colors"
          aria-label="Expand palette"
        >
          <ChevronRight className="h-4 w-4" />
        </button>
        {Object.values(CATEGORY_META).map((cat) => (
          <TooltipProvider key={cat.id}>
            <Tooltip>
              <TooltipTrigger asChild>
                <button
                  onClick={() => onCollapseChange?.(false)}
                  className="p-2 hover:bg-accent rounded-md transition-colors text-lg"
                >
                  {cat.icon}
                </button>
              </TooltipTrigger>
              <TooltipContent side="right">{cat.label}</TooltipContent>
            </Tooltip>
          </TooltipProvider>
        ))}
      </div>
    );
  }

  return (
    <div
      className={cn(
        'w-72 bg-card border-r flex flex-col',
        className
      )}
    >
      {/* Header */}
      <div className="p-3 border-b space-y-3">
        <div className="flex items-center justify-between">
          <h2 className="font-semibold text-sm">Block Palette</h2>
          <Badge variant="outline" className="text-xs">
            {availableBlocks}/{totalBlocks}
          </Badge>
        </div>

        {/* Search */}
        <div className="relative">
          <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
          <Input
            placeholder="Search blocks..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="pl-8 h-9"
          />
        </div>
      </div>

      {/* Block Categories */}
      <ScrollArea className="flex-1">
        <div className="p-2 space-y-1">
          {filteredGroups.length > 0 ? (
            filteredGroups.map((group) => (
              <CategoryGroup
                key={group.category}
                group={group}
                onBlockDragStart={onBlockDragStart}
                onBlockClick={onBlockClick}
              />
            ))
          ) : (
            <div className="text-center py-8 text-muted-foreground">
              <p className="text-sm">No blocks found</p>
              <p className="text-xs mt-1">Try a different search term</p>
            </div>
          )}
        </div>
      </ScrollArea>

      {/* Footer */}
      <div className="p-3 border-t text-xs text-muted-foreground">
        <p>Drag blocks to canvas or click to add</p>
        <p className="mt-1">
          Tier: <span className="font-medium text-foreground">{userTier}</span>
        </p>
      </div>
    </div>
  );
}

export default BlockPalette;

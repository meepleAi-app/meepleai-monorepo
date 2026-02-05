'use client';

/**
 * Plugin Palette Component
 *
 * Browse, search, and drag plugins onto the canvas.
 * Features: category grouping, search filter, favorites, drag-to-canvas.
 *
 * @version 1.0.0
 * @see Issue #3426 - Plugin Palette Component
 */

import { useState, useMemo, useCallback, type DragEvent } from 'react';

import { motion, AnimatePresence } from 'framer-motion';
import {
  Search,
  Star,
  ChevronDown,
  ChevronRight,
  Info,
  GripVertical,
} from 'lucide-react';

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
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { ScrollArea } from '@/components/ui/primitives/scroll-area';
import { cn } from '@/lib/utils';
import { usePipelineBuilderStore } from '@/stores/pipelineBuilderStore';

import {
  BUILT_IN_PLUGINS,
  PLUGIN_CATEGORY_COLORS,
  PLUGIN_CATEGORY_ICONS,
} from './types';

import type { PluginDefinition, PluginCategory } from './types';

// =============================================================================
// Types
// =============================================================================

interface PluginPaletteProps {
  className?: string;
  plugins?: PluginDefinition[];
}

// =============================================================================
// Category Order
// =============================================================================

const CATEGORY_ORDER: PluginCategory[] = [
  'routing',
  'cache',
  'retrieval',
  'evaluation',
  'generation',
  'validation',
  'transform',
];

const CATEGORY_LABELS: Record<PluginCategory, string> = {
  routing: 'Routing',
  cache: 'Caching',
  retrieval: 'Retrieval',
  evaluation: 'Evaluation',
  generation: 'Generation',
  validation: 'Validation',
  transform: 'Transform',
};

// =============================================================================
// Plugin Card
// =============================================================================

interface PluginCardProps {
  plugin: PluginDefinition;
  onDragStart: (plugin: PluginDefinition) => void;
  onToggleFavorite?: (pluginId: string) => void;
}

function PluginCard({ plugin, onDragStart, onToggleFavorite }: PluginCardProps) {
  const categoryColor = PLUGIN_CATEGORY_COLORS[plugin.category];

  const handleDragStart = (e: DragEvent<HTMLDivElement>) => {
    e.dataTransfer.effectAllowed = 'move';
    e.dataTransfer.setData('application/json', JSON.stringify(plugin));
    onDragStart(plugin);
  };

  return (
    <div
      draggable
      onDragStart={handleDragStart}
    >
      <motion.div
        layout
        initial={{ opacity: 0, y: 10 }}
        animate={{ opacity: 1, y: 0 }}
        exit={{ opacity: 0, y: -10 }}
        className={cn(
          'group relative p-3 rounded-lg cursor-grab active:cursor-grabbing',
          'bg-card border border-border hover:border-primary/50',
          'transition-all duration-200 hover:shadow-md'
        )}
        style={{
          borderLeftColor: categoryColor,
          borderLeftWidth: '3px',
        }}
      >
      {/* Drag Handle */}
      <div className="absolute left-1 top-1/2 -translate-y-1/2 opacity-0 group-hover:opacity-50 transition-opacity">
        <GripVertical className="h-4 w-4 text-muted-foreground" />
      </div>

      {/* Header */}
      <div className="flex items-start justify-between gap-2 mb-2">
        <div className="flex items-center gap-2 flex-1 min-w-0">
          <span className="text-base shrink-0" role="img" aria-label={plugin.category}>
            {plugin.icon || PLUGIN_CATEGORY_ICONS[plugin.category]}
          </span>
          <span className="font-medium text-sm truncate">{plugin.name}</span>
        </div>
        <div className="flex items-center gap-1 shrink-0">
          {onToggleFavorite && (
            <Button
              variant="ghost"
              size="sm"
              className="h-6 w-6 p-0"
              onClick={(e) => {
                e.stopPropagation();
                onToggleFavorite(plugin.id);
              }}
            >
              <Star
                className={cn(
                  'h-3.5 w-3.5',
                  plugin.isFavorite
                    ? 'fill-yellow-400 text-yellow-400'
                    : 'text-muted-foreground'
                )}
              />
            </Button>
          )}
          <TooltipProvider>
            <Tooltip>
              <TooltipTrigger asChild>
                <Button
                  variant="ghost"
                  size="sm"
                  className="h-6 w-6 p-0"
                  onClick={(e) => e.stopPropagation()}
                >
                  <Info className="h-3.5 w-3.5 text-muted-foreground" />
                </Button>
              </TooltipTrigger>
              <TooltipContent side="right" className="max-w-xs">
                <div className="space-y-2">
                  <p className="font-medium">{plugin.name}</p>
                  <p className="text-xs text-muted-foreground">{plugin.description}</p>
                  <div className="flex items-center gap-2 text-xs">
                    <Badge variant="outline" className="text-[10px]">
                      v{plugin.version}
                    </Badge>
                    <Badge
                      variant="secondary"
                      className="text-[10px] capitalize"
                      style={{ backgroundColor: `${categoryColor}20`, color: categoryColor }}
                    >
                      {plugin.category}
                    </Badge>
                  </div>
                </div>
              </TooltipContent>
            </Tooltip>
          </TooltipProvider>
        </div>
      </div>

      {/* Description */}
      <p className="text-xs text-muted-foreground line-clamp-2">{plugin.description}</p>

      {/* Footer */}
      <div className="mt-2 flex items-center gap-2">
        <Badge
          variant="secondary"
          className="text-[10px] capitalize"
          style={{ backgroundColor: `${categoryColor}15`, color: categoryColor }}
        >
          {plugin.category}
        </Badge>
        <span className="text-[10px] text-muted-foreground/60">v{plugin.version}</span>
      </div>
      </motion.div>
    </div>
  );
}

// =============================================================================
// Category Section
// =============================================================================

interface CategorySectionProps {
  category: PluginCategory;
  plugins: PluginDefinition[];
  onDragStart: (plugin: PluginDefinition) => void;
  onToggleFavorite?: (pluginId: string) => void;
  defaultOpen?: boolean;
}

function CategorySection({
  category,
  plugins,
  onDragStart,
  onToggleFavorite,
  defaultOpen = true,
}: CategorySectionProps) {
  const [isOpen, setIsOpen] = useState(defaultOpen);
  // eslint-disable-next-line security/detect-object-injection -- category is typed PluginCategory
  const categoryIcon = PLUGIN_CATEGORY_ICONS[category];

  return (
    <Collapsible open={isOpen} onOpenChange={setIsOpen}>
      <CollapsibleTrigger asChild>
        <button
          className={cn(
            'w-full flex items-center justify-between py-2 px-3 rounded-md',
            'hover:bg-muted/50 transition-colors',
            'text-left'
          )}
        >
          <div className="flex items-center gap-2">
            <span className="text-base" role="img" aria-label={category}>
              {categoryIcon}
            </span>
            {/* eslint-disable-next-line security/detect-object-injection -- category is typed */}
            <span className="font-medium text-sm">{CATEGORY_LABELS[category]}</span>
            <Badge variant="secondary" className="text-[10px] h-5 px-1.5">
              {plugins.length}
            </Badge>
          </div>
          {isOpen ? (
            <ChevronDown className="h-4 w-4 text-muted-foreground" />
          ) : (
            <ChevronRight className="h-4 w-4 text-muted-foreground" />
          )}
        </button>
      </CollapsibleTrigger>
      <CollapsibleContent>
        <motion.div
          initial={false}
          animate={{ height: 'auto' }}
          className="pl-2 space-y-2 pb-2"
        >
          <AnimatePresence mode="popLayout">
            {plugins.map((plugin) => (
              <PluginCard
                key={plugin.id}
                plugin={plugin}
                onDragStart={onDragStart}
                onToggleFavorite={onToggleFavorite}
              />
            ))}
          </AnimatePresence>
        </motion.div>
      </CollapsibleContent>
    </Collapsible>
  );
}

// =============================================================================
// Main Component
// =============================================================================

export function PluginPalette({
  className,
  plugins = BUILT_IN_PLUGINS,
}: PluginPaletteProps) {
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedCategory, setSelectedCategory] = useState<PluginCategory | 'all'>('all');
  const [favorites, setFavorites] = useState<Set<string>>(new Set());

  const { startDrag } = usePipelineBuilderStore();

  // Filter plugins based on search and category
  const filteredPlugins = useMemo(() => {
    let result = plugins;

    // Filter by search query
    if (searchQuery.trim()) {
      const query = searchQuery.toLowerCase();
      result = result.filter(
        (p) =>
          p.name.toLowerCase().includes(query) ||
          p.description.toLowerCase().includes(query) ||
          p.category.toLowerCase().includes(query)
      );
    }

    // Filter by category
    if (selectedCategory !== 'all') {
      result = result.filter((p) => p.category === selectedCategory);
    }

    // Add favorite status
    result = result.map((p) => ({
      ...p,
      isFavorite: favorites.has(p.id),
    }));

    return result;
  }, [plugins, searchQuery, selectedCategory, favorites]);

  // Group plugins by category
  const groupedPlugins = useMemo(() => {
    const groups = new Map<PluginCategory, PluginDefinition[]>();

    CATEGORY_ORDER.forEach((category) => {
      const categoryPlugins = filteredPlugins.filter((p) => p.category === category);
      if (categoryPlugins.length > 0) {
        groups.set(category, categoryPlugins);
      }
    });

    return groups;
  }, [filteredPlugins]);

  // Favorite plugins
  const favoritePlugins = useMemo(
    () => filteredPlugins.filter((p) => favorites.has(p.id)),
    [filteredPlugins, favorites]
  );

  // Handlers
  const handleDragStart = useCallback(
    (plugin: PluginDefinition) => {
      startDrag(plugin);
    },
    [startDrag]
  );

  const handleToggleFavorite = useCallback((pluginId: string) => {
    setFavorites((prev) => {
      const next = new Set(prev);
      if (next.has(pluginId)) {
        next.delete(pluginId);
      } else {
        next.add(pluginId);
      }
      return next;
    });
  }, []);

  return (
    <div className={cn('flex flex-col h-full', className)}>
      {/* Search */}
      <div className="p-3 border-b">
        <div className="relative">
          <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
          <Input
            type="search"
            placeholder="Search plugins..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="pl-8 h-9"
          />
        </div>
      </div>

      {/* Category Filter */}
      <div className="p-2 border-b overflow-x-auto">
        <div className="flex gap-1.5 min-w-max">
          <Button
            variant={selectedCategory === 'all' ? 'default' : 'ghost'}
            size="sm"
            className="h-7 text-xs"
            onClick={() => setSelectedCategory('all')}
          >
            All
          </Button>
          {CATEGORY_ORDER.map((category) => (
            <Button
              key={category}
              variant={selectedCategory === category ? 'default' : 'ghost'}
              size="sm"
              className="h-7 text-xs capitalize"
              onClick={() => setSelectedCategory(category)}
            >
              {/* eslint-disable-next-line security/detect-object-injection -- category is typed */}
              {PLUGIN_CATEGORY_ICONS[category]} {category}
            </Button>
          ))}
        </div>
      </div>

      {/* Plugin List */}
      <ScrollArea className="flex-1">
        <div className="p-2 space-y-1">
          {/* Favorites Section */}
          {favoritePlugins.length > 0 && (
            <CategorySection
              category="routing" // Just for styling, we override the header
              plugins={favoritePlugins}
              onDragStart={handleDragStart}
              onToggleFavorite={handleToggleFavorite}
              defaultOpen
            />
          )}

          {/* Favorites Header Override */}
          {favoritePlugins.length > 0 && (
            <div className="px-3 py-2">
              <div className="flex items-center gap-2 text-xs text-muted-foreground">
                <Star className="h-3.5 w-3.5 fill-yellow-400 text-yellow-400" />
                <span>Favorites</span>
                <div className="flex-1 h-px bg-border" />
              </div>
            </div>
          )}

          {/* Category Sections */}
          {Array.from(groupedPlugins.entries()).map(([category, categoryPlugins]) => (
            <CategorySection
              key={category}
              category={category}
              plugins={categoryPlugins}
              onDragStart={handleDragStart}
              onToggleFavorite={handleToggleFavorite}
            />
          ))}

          {/* Empty State */}
          {filteredPlugins.length === 0 && (
            <div className="p-8 text-center">
              <p className="text-sm text-muted-foreground">
                No plugins found matching &quot;{searchQuery}&quot;
              </p>
            </div>
          )}
        </div>
      </ScrollArea>

      {/* Footer */}
      <div className="p-3 border-t bg-muted/30">
        <p className="text-[10px] text-muted-foreground text-center">
          Drag plugins onto the canvas to build your pipeline
        </p>
      </div>
    </div>
  );
}

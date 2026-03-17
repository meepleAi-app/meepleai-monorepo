/**
 * AdvancedFilterPanel Component (Issue #2873)
 *
 * Collapsible filter panel with slide-in animation for SharedGameCatalog.
 * Features: Players, Complexity, Duration, Categories filters with orange active states.
 *
 * @see Issue #2873: Advanced Filter Panel Component
 */

'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';

import {
  Check,
  ChevronDown,
  RotateCcw,
  SlidersHorizontal,
  Users,
  Clock,
  Puzzle,
  Grid3X3,
} from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import {
  Collapsible,
  CollapsibleContent,
  CollapsibleTrigger,
} from '@/components/ui/data-display/collapsible';
import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetDescription,
  SheetFooter,
} from '@/components/ui/navigation/sheet';
import { Button } from '@/components/ui/primitives/button';
import { Checkbox } from '@/components/ui/primitives/checkbox';
import { Slider } from '@/components/ui/primitives/slider';
import { api } from '@/lib/api';
import type { GameCategory, GameMechanic } from '@/lib/api/schemas/shared-games.schemas';
import { logger } from '@/lib/logger';
import { cn } from '@/lib/utils';

import { type SearchFilters, DEFAULT_FILTERS } from './SharedGameSearchFilters';

// ============================================================================
// Types
// ============================================================================

export interface AdvancedFilterPanelProps {
  /** Whether the panel is open */
  open: boolean;
  /** Called when panel open state changes */
  onOpenChange: (open: boolean) => void;
  /** Current filter values */
  filters: SearchFilters;
  /** Called when filters are applied */
  onApplyFilters: (filters: SearchFilters) => void;
  /** Additional CSS classes */
  className?: string;
}

// ============================================================================
// Constants
// ============================================================================

const COMPLEXITY_MIN = 1.0;
const COMPLEXITY_MAX = 5.0;
const COMPLEXITY_STEP = 0.1;

const PLAYERS_MIN = 1;
const PLAYERS_MAX = 12;

const DURATION_MIN = 5;
const DURATION_MAX = 300;
const DURATION_STEP = 5;

// ============================================================================
// Helper Components
// ============================================================================

interface FilterSectionProps {
  title: string;
  icon: React.ReactNode;
  activeCount: number;
  children: React.ReactNode;
  defaultOpen?: boolean;
}

function FilterSection({
  title,
  icon,
  activeCount,
  children,
  defaultOpen = true,
}: FilterSectionProps) {
  const [isOpen, setIsOpen] = useState(defaultOpen);

  return (
    <Collapsible open={isOpen} onOpenChange={setIsOpen} className="space-y-2">
      <CollapsibleTrigger asChild>
        <Button
          variant="ghost"
          className="w-full justify-between px-2 py-1.5 h-auto font-medium text-sm hover:bg-muted/50"
        >
          <span className="flex items-center gap-2">
            {icon}
            {title}
            {activeCount > 0 && (
              <Badge className="h-5 min-w-5 px-1.5 text-xs bg-orange-500 hover:bg-orange-500 text-white border-0">
                {activeCount}
              </Badge>
            )}
          </span>
          <ChevronDown
            className={cn(
              'h-4 w-4 text-muted-foreground transition-transform duration-200',
              isOpen && 'rotate-180'
            )}
          />
        </Button>
      </CollapsibleTrigger>
      <CollapsibleContent className="px-2 pb-2 space-y-3">{children}</CollapsibleContent>
    </Collapsible>
  );
}

// ============================================================================
// Component
// ============================================================================

export function AdvancedFilterPanel({
  open,
  onOpenChange,
  filters,
  onApplyFilters,
  className,
}: AdvancedFilterPanelProps) {
  // Local state for draft filters (applied on "Apply" button click)
  const [draftFilters, setDraftFilters] = useState<SearchFilters>(filters);

  // Reference data
  const [categories, setCategories] = useState<GameCategory[]>([]);
  const [mechanics, setMechanics] = useState<GameMechanic[]>([]);
  const [loading, setLoading] = useState(true);

  // Sync draft filters when panel opens or external filters change
  useEffect(() => {
    if (open) {
      setDraftFilters(filters);
    }
  }, [open, filters]);

  // ============================================================================
  // Load Reference Data
  // ============================================================================

  useEffect(() => {
    const loadReferenceData = async () => {
      setLoading(true);
      try {
        const [categoriesData, mechanicsData] = await Promise.all([
          api.sharedGames.getCategories(),
          api.sharedGames.getMechanics(),
        ]);
        setCategories(categoriesData);
        setMechanics(mechanicsData);
      } catch (error) {
        logger.error('Failed to load filter reference data:', error);
      } finally {
        setLoading(false);
      }
    };

    if (open) {
      void loadReferenceData();
    }
  }, [open]);

  // ============================================================================
  // Filter Counts per Section
  // ============================================================================

  const playersCount = useMemo(() => {
    return draftFilters.minPlayers !== null || draftFilters.maxPlayers !== null ? 1 : 0;
  }, [draftFilters.minPlayers, draftFilters.maxPlayers]);

  const complexityCount = useMemo(() => {
    return draftFilters.minComplexity !== null || draftFilters.maxComplexity !== null ? 1 : 0;
  }, [draftFilters.minComplexity, draftFilters.maxComplexity]);

  const durationCount = useMemo(() => {
    return draftFilters.minPlayingTime !== null || draftFilters.maxPlayingTime !== null ? 1 : 0;
  }, [draftFilters.minPlayingTime, draftFilters.maxPlayingTime]);

  const categoriesCount = useMemo(() => {
    return draftFilters.categoryIds.length;
  }, [draftFilters.categoryIds]);

  const mechanicsCount = useMemo(() => {
    return draftFilters.mechanicIds.length;
  }, [draftFilters.mechanicIds]);

  const totalActiveCount = useMemo(() => {
    let count = 0;
    if (playersCount > 0) count++;
    if (complexityCount > 0) count++;
    if (durationCount > 0) count++;
    if (categoriesCount > 0) count++;
    if (mechanicsCount > 0) count++;
    if (draftFilters.catalogOnly) count++;
    return count;
  }, [
    playersCount,
    complexityCount,
    durationCount,
    categoriesCount,
    mechanicsCount,
    draftFilters.catalogOnly,
  ]);

  // ============================================================================
  // Handlers
  // ============================================================================

  const handlePlayersChange = useCallback((values: number[]) => {
    setDraftFilters(prev => ({
      ...prev,
      minPlayers: values[0] === PLAYERS_MIN ? null : values[0],
      maxPlayers: values[1] === PLAYERS_MAX ? null : values[1],
    }));
  }, []);

  const handleComplexityChange = useCallback((values: number[]) => {
    setDraftFilters(prev => ({
      ...prev,
      minComplexity: values[0] === COMPLEXITY_MIN ? null : values[0],
      maxComplexity: values[1] === COMPLEXITY_MAX ? null : values[1],
    }));
  }, []);

  const handleDurationChange = useCallback((values: number[]) => {
    setDraftFilters(prev => ({
      ...prev,
      minPlayingTime: values[0] === DURATION_MIN ? null : values[0],
      maxPlayingTime: values[1] === DURATION_MAX ? null : values[1],
    }));
  }, []);

  const handleCategoryToggle = useCallback((categoryId: string, checked: boolean) => {
    setDraftFilters(prev => ({
      ...prev,
      categoryIds: checked
        ? [...prev.categoryIds, categoryId]
        : prev.categoryIds.filter(id => id !== categoryId),
    }));
  }, []);

  const handleMechanicToggle = useCallback((mechanicId: string, checked: boolean) => {
    setDraftFilters(prev => ({
      ...prev,
      mechanicIds: checked
        ? [...prev.mechanicIds, mechanicId]
        : prev.mechanicIds.filter(id => id !== mechanicId),
    }));
  }, []);

  const handleCatalogOnlyChange = useCallback((checked: boolean) => {
    setDraftFilters(prev => ({
      ...prev,
      catalogOnly: checked,
    }));
  }, []);

  const handleApply = useCallback(() => {
    onApplyFilters(draftFilters);
    onOpenChange(false);
  }, [draftFilters, onApplyFilters, onOpenChange]);

  const handleReset = useCallback(() => {
    setDraftFilters(DEFAULT_FILTERS);
  }, []);

  // ============================================================================
  // Get Current Slider Values
  // ============================================================================

  const playersValue = useMemo(
    () => [draftFilters.minPlayers ?? PLAYERS_MIN, draftFilters.maxPlayers ?? PLAYERS_MAX],
    [draftFilters.minPlayers, draftFilters.maxPlayers]
  );

  const complexityValue = useMemo(
    () => [
      draftFilters.minComplexity ?? COMPLEXITY_MIN,
      draftFilters.maxComplexity ?? COMPLEXITY_MAX,
    ],
    [draftFilters.minComplexity, draftFilters.maxComplexity]
  );

  const durationValue = useMemo(
    () => [
      draftFilters.minPlayingTime ?? DURATION_MIN,
      draftFilters.maxPlayingTime ?? DURATION_MAX,
    ],
    [draftFilters.minPlayingTime, draftFilters.maxPlayingTime]
  );

  // ============================================================================
  // Render
  // ============================================================================

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent side="right" className={cn('w-full sm:max-w-md flex flex-col', className)}>
        <SheetHeader className="pb-4 border-b">
          <SheetTitle className="flex items-center gap-2">
            <SlidersHorizontal className="h-5 w-5" />
            Filtri Avanzati
            {totalActiveCount > 0 && (
              <Badge className="bg-orange-500 hover:bg-orange-500 text-white border-0">
                {totalActiveCount} attivi
              </Badge>
            )}
          </SheetTitle>
          <SheetDescription>Personalizza la ricerca con filtri specifici</SheetDescription>
        </SheetHeader>

        {/* Scrollable Content */}
        <div className="flex-1 overflow-y-auto py-4 space-y-1">
          {loading ? (
            <div className="flex items-center justify-center py-8">
              <span className="text-sm text-muted-foreground">Caricamento filtri...</span>
            </div>
          ) : (
            <>
              {/* Players Section */}
              <FilterSection
                title="Giocatori"
                icon={<Users className="h-4 w-4" />}
                activeCount={playersCount}
              >
                <div className="space-y-4">
                  <Slider
                    value={playersValue}
                    min={PLAYERS_MIN}
                    max={PLAYERS_MAX}
                    step={1}
                    onValueChange={handlePlayersChange}
                    className="mt-2"
                    aria-label="Numero giocatori"
                  />
                  <div className="flex justify-between text-xs text-muted-foreground">
                    <span>Min: {playersValue[0]}</span>
                    <span>Max: {playersValue[1] === PLAYERS_MAX ? '12+' : playersValue[1]}</span>
                  </div>
                </div>
              </FilterSection>

              {/* Complexity Section */}
              <FilterSection
                title="Complessit&agrave;"
                icon={<Puzzle className="h-4 w-4" />}
                activeCount={complexityCount}
              >
                <div className="space-y-4">
                  <Slider
                    value={complexityValue}
                    min={COMPLEXITY_MIN}
                    max={COMPLEXITY_MAX}
                    step={COMPLEXITY_STEP}
                    onValueChange={handleComplexityChange}
                    className="mt-2"
                    aria-label="Complessit&agrave; del gioco"
                  />
                  <div className="flex justify-between text-xs text-muted-foreground">
                    <span>Semplice: {complexityValue[0].toFixed(1)}</span>
                    <span>Complesso: {complexityValue[1].toFixed(1)}</span>
                  </div>
                </div>
              </FilterSection>

              {/* Duration Section */}
              <FilterSection
                title="Durata"
                icon={<Clock className="h-4 w-4" />}
                activeCount={durationCount}
              >
                <div className="space-y-4">
                  <Slider
                    value={durationValue}
                    min={DURATION_MIN}
                    max={DURATION_MAX}
                    step={DURATION_STEP}
                    onValueChange={handleDurationChange}
                    className="mt-2"
                    aria-label="Durata del gioco"
                  />
                  <div className="flex justify-between text-xs text-muted-foreground">
                    <span>Min: {durationValue[0]} min</span>
                    <span>
                      Max: {durationValue[1] === DURATION_MAX ? '300+' : durationValue[1]} min
                    </span>
                  </div>
                </div>
              </FilterSection>

              {/* Categories Section */}
              <FilterSection
                title="Categorie"
                icon={<Grid3X3 className="h-4 w-4" />}
                activeCount={categoriesCount}
                defaultOpen={false}
              >
                <div className="max-h-48 overflow-y-auto space-y-1 pr-2">
                  {categories.map(category => (
                    <label
                      key={category.id}
                      className={cn(
                        'flex items-center gap-2 py-1.5 px-2 rounded cursor-pointer transition-colors',
                        draftFilters.categoryIds.includes(category.id)
                          ? 'bg-orange-500/10 hover:bg-orange-500/20'
                          : 'hover:bg-muted'
                      )}
                    >
                      <Checkbox
                        checked={draftFilters.categoryIds.includes(category.id)}
                        onCheckedChange={checked =>
                          handleCategoryToggle(category.id, checked as boolean)
                        }
                        className={cn(
                          draftFilters.categoryIds.includes(category.id) &&
                            'border-orange-500 bg-orange-500 text-white data-[state=checked]:bg-orange-500 data-[state=checked]:border-orange-500'
                        )}
                      />
                      <span className="text-sm">{category.name}</span>
                    </label>
                  ))}
                  {categories.length === 0 && (
                    <p className="text-sm text-muted-foreground p-2">
                      Nessuna categoria disponibile
                    </p>
                  )}
                </div>
              </FilterSection>

              {/* Mechanics Section */}
              <FilterSection
                title="Meccaniche"
                icon={<Puzzle className="h-4 w-4" />}
                activeCount={mechanicsCount}
                defaultOpen={false}
              >
                <div className="max-h-48 overflow-y-auto space-y-1 pr-2">
                  {mechanics.map(mechanic => (
                    <label
                      key={mechanic.id}
                      className={cn(
                        'flex items-center gap-2 py-1.5 px-2 rounded cursor-pointer transition-colors',
                        draftFilters.mechanicIds.includes(mechanic.id)
                          ? 'bg-orange-500/10 hover:bg-orange-500/20'
                          : 'hover:bg-muted'
                      )}
                    >
                      <Checkbox
                        checked={draftFilters.mechanicIds.includes(mechanic.id)}
                        onCheckedChange={checked =>
                          handleMechanicToggle(mechanic.id, checked as boolean)
                        }
                        className={cn(
                          draftFilters.mechanicIds.includes(mechanic.id) &&
                            'border-orange-500 bg-orange-500 text-white data-[state=checked]:bg-orange-500 data-[state=checked]:border-orange-500'
                        )}
                      />
                      <span className="text-sm">{mechanic.name}</span>
                    </label>
                  ))}
                  {mechanics.length === 0 && (
                    <p className="text-sm text-muted-foreground p-2">
                      Nessuna meccanica disponibile
                    </p>
                  )}
                </div>
              </FilterSection>

              {/* Catalog Only Toggle */}
              <div className="pt-4 mt-4 border-t">
                <label
                  className={cn(
                    'flex items-center justify-between py-2 px-2 rounded cursor-pointer transition-colors',
                    draftFilters.catalogOnly ? 'bg-orange-500/10' : 'hover:bg-muted'
                  )}
                >
                  <div className="space-y-0.5">
                    <span className="text-sm font-medium">Solo dal catalogo</span>
                    <p className="text-xs text-muted-foreground">
                      Mostra solo giochi nel nostro database
                    </p>
                  </div>
                  <Checkbox
                    checked={draftFilters.catalogOnly}
                    onCheckedChange={checked => handleCatalogOnlyChange(checked as boolean)}
                    className={cn(
                      draftFilters.catalogOnly &&
                        'border-orange-500 bg-orange-500 text-white data-[state=checked]:bg-orange-500 data-[state=checked]:border-orange-500'
                    )}
                  />
                </label>
              </div>
            </>
          )}
        </div>

        {/* Footer with Apply/Reset Buttons */}
        <SheetFooter className="pt-4 border-t gap-2 sm:gap-2">
          <Button
            variant="ghost"
            onClick={handleReset}
            className="flex-1 sm:flex-none"
            disabled={loading}
          >
            <RotateCcw className="h-4 w-4 mr-2" />
            Resetta
          </Button>
          <Button
            onClick={handleApply}
            className="flex-1 sm:flex-none bg-orange-500 hover:bg-orange-600 text-white"
            disabled={loading}
          >
            <Check className="h-4 w-4 mr-2" />
            Applica Filtri
          </Button>
        </SheetFooter>
      </SheetContent>
    </Sheet>
  );
}

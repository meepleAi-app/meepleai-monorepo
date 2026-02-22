/**
 * CollectionDashboard - User Game Collection Management
 * Issue #3632 - EPIC #3475: User Private Library & Collections Management
 *
 * A comprehensive dashboard for managing user's game collection with:
 * - Hero stats section with key metrics
 * - Advanced filtering and search
 * - Grid/List view toggle
 * - Quick actions (add game, create collection)
 * - Responsive design with "Warm Tabletop" aesthetic
 *
 * @example
 * ```tsx
 * <CollectionDashboard />
 * ```
 */

'use client';

import { useState, useMemo, useCallback, useRef } from 'react';

import { motion, AnimatePresence } from 'framer-motion';
import {
  Library,
  Dices,
  Heart,
  Plus,
  Search,
  TrendingUp,
  Grid3X3,
  List,
  SortAsc,
  X,
  Sparkles,
  Share2,
  Package,
  Star,
  LayoutGrid,
  GalleryHorizontalEnd,
  BookOpen,
  Gamepad2,
  ChevronDown,
  type LucideIcon,
} from 'lucide-react';
import Link from 'next/link';

import { Badge } from '@/components/ui/data-display/badge';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/navigation/dropdown-menu';
import { GameCarousel, type CarouselGame } from '@/components/ui/data-display/game-carousel';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import {
  useLibrary,
  useLibraryStats,
  useLibraryQuota,
} from '@/hooks/queries/useLibrary';
import type { GameStateType, GetUserLibraryParams } from '@/lib/api/schemas/library.schemas';
import { useTranslation } from '@/hooks/useTranslation';
import { COLLECTION_TEST_IDS } from '@/lib/test-ids';
import { cn } from '@/lib/utils';


// ============================================================================
// Types
// ============================================================================

export type ViewMode = 'grid' | 'list' | 'carousel';
export type SortOption = 'addedAt' | 'title' | 'favorite' | 'playCount';

export interface CollectionDashboardProps {
  /** Additional className */
  className?: string;
}

interface HeroStat {
  id: string;
  icon: LucideIcon;
  value: number;
  label: string;
  trend?: number;
  color: 'amber' | 'emerald' | 'teal' | 'purple';
}

interface FilterChip {
  id: 'all' | 'favorites' | GameStateType;
  label: string;
  icon: React.ReactNode;
  color: string;
  activeColor: string;
}

// ============================================================================
// Constants
// ============================================================================

const FILTER_CHIPS: FilterChip[] = [
  {
    id: 'all',
    label: 'Tutti',
    icon: <LayoutGrid className="h-3 w-3" />,
    color: 'bg-muted text-muted-foreground hover:bg-muted/80',
    activeColor: 'bg-primary text-primary-foreground',
  },
  {
    id: 'favorites',
    label: 'Preferiti',
    icon: <Heart className="h-3 w-3" />,
    color: 'bg-muted text-muted-foreground hover:bg-muted/80',
    activeColor: 'bg-purple-600 text-white',
  },
  {
    id: 'Nuovo',
    label: 'Nuovo',
    icon: <Sparkles className="h-3 w-3" />,
    color: 'bg-muted text-muted-foreground hover:bg-muted/80',
    activeColor: 'bg-green-600 text-white',
  },
  {
    id: 'InPrestito',
    label: 'In Prestito',
    icon: <Share2 className="h-3 w-3" />,
    color: 'bg-muted text-muted-foreground hover:bg-muted/80',
    activeColor: 'bg-red-600 text-white',
  },
  {
    id: 'Wishlist',
    label: 'Wishlist',
    icon: <Star className="h-3 w-3" />,
    color: 'bg-muted text-muted-foreground hover:bg-muted/80',
    activeColor: 'bg-yellow-600 text-white',
  },
  {
    id: 'Owned',
    label: 'Posseduto',
    icon: <Package className="h-3 w-3" />,
    color: 'bg-muted text-muted-foreground hover:bg-muted/80',
    activeColor: 'bg-teal-600 text-white',
  },
];

const COLOR_MAP = {
  amber: {
    bg: 'bg-amber-500/15 dark:bg-amber-500/20',
    icon: 'text-amber-600 dark:text-amber-400',
    border: 'border-amber-500/30',
    glow: 'shadow-amber-500/20',
  },
  emerald: {
    bg: 'bg-emerald-500/15 dark:bg-emerald-500/20',
    icon: 'text-emerald-600 dark:text-emerald-400',
    border: 'border-emerald-500/30',
    glow: 'shadow-emerald-500/20',
  },
  teal: {
    bg: 'bg-teal-500/15 dark:bg-teal-500/20',
    icon: 'text-teal-600 dark:text-teal-400',
    border: 'border-teal-500/30',
    glow: 'shadow-teal-500/20',
  },
  purple: {
    bg: 'bg-purple-500/15 dark:bg-purple-500/20',
    icon: 'text-purple-600 dark:text-purple-400',
    border: 'border-purple-500/30',
    glow: 'shadow-purple-500/20',
  },
};

// ============================================================================
// Sub-Components
// ============================================================================

/** Hero stat card with trend indicator */
function HeroStatCard({ stat, index }: { stat: HeroStat; index: number }) {
  const colors = COLOR_MAP[stat.color];
  const Icon = stat.icon;

  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ delay: index * 0.05 }}
      className={cn(
        'relative flex flex-col gap-1 p-4 rounded-2xl',
        'border backdrop-blur-sm',
        'transition-all duration-300',
        'hover:scale-[1.02] hover:shadow-lg',
        colors.bg,
        colors.border
      )}
      data-testid={COLLECTION_TEST_IDS.heroStat(stat.id)}
    >
      <div className="flex items-center justify-between">
        <Icon className={cn('h-5 w-5', colors.icon)} />
        {stat.trend !== undefined && stat.trend > 0 && (
          <span className="flex items-center gap-0.5 text-xs text-emerald-600 dark:text-emerald-400">
            <TrendingUp className="h-3 w-3" />
            +{stat.trend}
          </span>
        )}
      </div>
      <div className="mt-1">
        <span className="font-heading text-2xl font-bold tracking-tight">
          {stat.value}
        </span>
      </div>
      <span className="text-xs text-muted-foreground">{stat.label}</span>
    </motion.div>
  );
}

/** Skeleton for hero stat card */
function HeroStatSkeleton() {
  return (
    <div className="flex flex-col gap-2 p-4 rounded-2xl border bg-muted/20">
      <Skeleton className="h-5 w-5 rounded" />
      <Skeleton className="h-8 w-16 mt-1" />
      <Skeleton className="h-3 w-12" />
    </div>
  );
}

/** Collection toolbar with search, filters, and view toggle */
function CollectionToolbar({
  searchQuery,
  onSearchChange,
  viewMode,
  onViewModeChange,
  sortBy,
  sortDescending,
  onSortChange,
  favoritesOnly,
  onFavoritesChange,
  stateFilter,
  onStateFilterChange,
  onClearFilters,
  stateCounts,
}: {
  searchQuery: string;
  onSearchChange: (query: string) => void;
  viewMode: ViewMode;
  onViewModeChange: (mode: ViewMode) => void;
  sortBy: SortOption;
  sortDescending: boolean;
  onSortChange: (sortBy: SortOption, descending: boolean) => void;
  favoritesOnly: boolean;
  onFavoritesChange: (enabled: boolean) => void;
  stateFilter: GameStateType[];
  onStateFilterChange: (states: GameStateType[]) => void;
  onClearFilters: () => void;
  stateCounts?: {
    total: number;
    favorites: number;
    nuovo: number;
    inPrestito: number;
    wishlist: number;
    owned: number;
  };
}) {
  const { t } = useTranslation();
  const [localSearch, setLocalSearch] = useState(searchQuery);
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Debounce search input (300ms)
  const handleSearchInput = useCallback((value: string) => {
    setLocalSearch(value);
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => {
      onSearchChange(value);
    }, 300);
  }, [onSearchChange]);

  const handleSortChange = (value: string) => {
    const [field, order] = value.split('-') as [SortOption, 'asc' | 'desc'];
    onSortChange(field, order === 'desc');
  };

  const handleFilterChipClick = (chipId: FilterChip['id']) => {
    if (chipId === 'all') {
      onFavoritesChange(false);
      onStateFilterChange([]);
    } else if (chipId === 'favorites') {
      onFavoritesChange(!favoritesOnly);
      if (!favoritesOnly) onStateFilterChange([]);
    } else {
      const state = chipId as GameStateType;
      if (stateFilter.includes(state)) {
        onStateFilterChange(stateFilter.filter(s => s !== state));
      } else {
        onStateFilterChange([...stateFilter, state]);
        onFavoritesChange(false);
      }
    }
  };

  const isChipActive = (chipId: FilterChip['id']): boolean => {
    if (chipId === 'all') return !favoritesOnly && stateFilter.length === 0;
    if (chipId === 'favorites') return favoritesOnly;
    return stateFilter.includes(chipId as GameStateType);
  };

  const getChipCount = (chipId: FilterChip['id']): number | undefined => {
    if (!stateCounts) return undefined;
    switch (chipId) {
      case 'all': return stateCounts.total;
      case 'favorites': return stateCounts.favorites;
      case 'Nuovo': return stateCounts.nuovo;
      case 'InPrestito': return stateCounts.inPrestito;
      case 'Wishlist': return stateCounts.wishlist;
      case 'Owned': return stateCounts.owned;
      default: return undefined;
    }
  };

  const hasActiveFilters = Boolean(searchQuery) || favoritesOnly || stateFilter.length > 0;

  return (
    <div className="space-y-4" data-testid={COLLECTION_TEST_IDS.toolbar}>
      {/* Search and Actions Row */}
      <div className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
        {/* Search Input */}
        <div className="relative flex-1 max-w-md">
          <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            type="search"
            placeholder="Cerca nella collezione..."
            value={localSearch}
            onChange={e => handleSearchInput(e.target.value)}
            className="pl-9 pr-9"
            aria-label="Search collection"
            data-testid={COLLECTION_TEST_IDS.search}
          />
          {localSearch && (
            <Button
              variant="ghost"
              size="icon"
              className="absolute right-1 top-1/2 h-7 w-7 -translate-y-1/2"
              onClick={() => {
                setLocalSearch('');
                onSearchChange('');
              }}
              aria-label="Clear search"
            >
              <X className="h-4 w-4" />
            </Button>
          )}
        </div>

        {/* Actions */}
        <div className="flex items-center gap-2">
          {/* Sort Dropdown */}
          <Select
            value={`${sortBy}-${sortDescending ? 'desc' : 'asc'}`}
            onValueChange={handleSortChange}
          >
            <SelectTrigger className="w-[160px]" aria-label="Sort collection">
              <SortAsc className="mr-2 h-4 w-4" />
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="addedAt-desc">Più recenti</SelectItem>
              <SelectItem value="addedAt-asc">Meno recenti</SelectItem>
              <SelectItem value="title-asc">Titolo (A-Z)</SelectItem>
              <SelectItem value="title-desc">Titolo (Z-A)</SelectItem>
              <SelectItem value="favorite-desc">Preferiti prima</SelectItem>
            </SelectContent>
          </Select>

          {/* View Mode Toggle */}
          <div className="flex items-center rounded-lg border bg-muted/50 p-1">
            <Button
              variant={viewMode === 'grid' ? 'default' : 'ghost'}
              size="icon"
              className="h-8 w-8"
              onClick={() => onViewModeChange('grid')}
              aria-label="Grid view"
              data-testid={COLLECTION_TEST_IDS.viewModeGrid}
            >
              <Grid3X3 className="h-4 w-4" />
            </Button>
            <Button
              variant={viewMode === 'list' ? 'default' : 'ghost'}
              size="icon"
              className="h-8 w-8"
              onClick={() => onViewModeChange('list')}
              aria-label="List view"
              data-testid={COLLECTION_TEST_IDS.viewModeList}
            >
              <List className="h-4 w-4" />
            </Button>
            <Button
              variant={viewMode === 'carousel' ? 'default' : 'ghost'}
              size="icon"
              className="h-8 w-8"
              onClick={() => onViewModeChange('carousel')}
              aria-label="Carousel view"
              data-testid="view-mode-carousel"
            >
              <GalleryHorizontalEnd className="h-4 w-4" />
            </Button>
          </div>

          {/* Add Game Dropdown */}
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button className="gap-2">
                <Plus className="h-4 w-4" />
                <span className="hidden sm:inline">Aggiungi</span>
                <ChevronDown className="h-3 w-3 opacity-60" />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              <DropdownMenuItem asChild>
                <Link href="/games/add" className="flex items-center gap-2 cursor-pointer">
                  <BookOpen className="h-4 w-4" />
                  {t('collection.addFromCatalog')}
                </Link>
              </DropdownMenuItem>
              <DropdownMenuItem asChild>
                <Link href="/library/private/add" className="flex items-center gap-2 cursor-pointer">
                  <Gamepad2 className="h-4 w-4" />
                  {t('collection.addPrivateGame')}
                </Link>
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </div>
      </div>

      {/* Filter Chips Row */}
      <div className="flex flex-wrap items-center gap-2">
        {FILTER_CHIPS.map(chip => {
          const isActive = isChipActive(chip.id);
          const count = getChipCount(chip.id);

          return (
            <Badge
              key={chip.id}
              variant="outline"
              className={cn(
                'cursor-pointer px-3 py-1.5 text-sm font-medium transition-all',
                'hover:scale-105',
                isActive ? chip.activeColor : chip.color
              )}
              onClick={() => handleFilterChipClick(chip.id)}
              role="button"
              aria-pressed={isActive}
              data-testid={COLLECTION_TEST_IDS.filterChip(chip.id)}
            >
              {chip.icon && <span className="mr-1">{chip.icon}</span>}
              {chip.label}
              {count !== undefined && (
                <span
                  className={cn(
                    'ml-1.5 rounded-full px-1.5 text-xs',
                    isActive ? 'bg-white/20' : 'bg-muted-foreground/20'
                  )}
                >
                  {count}
                </span>
              )}
            </Badge>
          );
        })}

        {hasActiveFilters && (
          <Button
            variant="ghost"
            size="sm"
            onClick={onClearFilters}
            className="h-8 text-xs"
          >
            <X className="mr-1 h-3 w-3" />
            Pulisci
          </Button>
        )}
      </div>
    </div>
  );
}

/** Empty state when no games in collection */
function EmptyState({ hasFilters }: { hasFilters: boolean }) {
  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      className="flex flex-col items-center justify-center py-16 text-center"
      data-testid={COLLECTION_TEST_IDS.emptyState}
    >
      <div className="rounded-full bg-muted/50 p-6 mb-4">
        <Library className="h-12 w-12 text-muted-foreground" />
      </div>
      <h3 className="font-heading text-xl font-semibold mb-2">
        {hasFilters ? 'Nessun gioco trovato' : 'La tua collezione è vuota'}
      </h3>
      <p className="text-muted-foreground max-w-md mb-6">
        {hasFilters
          ? 'Prova a modificare i filtri o cerca qualcos\'altro.'
          : 'Inizia ad aggiungere i tuoi giochi da tavolo preferiti per costruire la tua collezione.'}
      </p>
      {!hasFilters && (
        <Button asChild size="lg" className="gap-2">
          <Link href="/games/discover">
            <Plus className="h-5 w-5" />
            Aggiungi il tuo primo gioco
          </Link>
        </Button>
      )}
    </motion.div>
  );
}

/** Loading skeleton for game grid */
function GameGridSkeleton({ count = 8 }: { count?: number }) {
  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
      {Array.from({ length: count }).map((_, i) => (
        <div key={i} className="rounded-xl border bg-card/80 overflow-hidden">
          <Skeleton className="aspect-[4/3]" />
          <div className="p-3 space-y-2">
            <Skeleton className="h-4 w-3/4" />
            <Skeleton className="h-3 w-1/2" />
            <div className="grid grid-cols-2 gap-2">
              <Skeleton className="h-3 w-full" />
              <Skeleton className="h-3 w-full" />
            </div>
          </div>
        </div>
      ))}
    </div>
  );
}

// ============================================================================
// Main Component
// ============================================================================

export function CollectionDashboard({ className }: CollectionDashboardProps) {
  const { t } = useTranslation();
  // Local state
  const [viewMode, setViewMode] = useState<ViewMode>('grid');
  const [searchQuery, setSearchQuery] = useState('');
  const [favoritesOnly, setFavoritesOnly] = useState(false);
  const [stateFilter, setStateFilter] = useState<GameStateType[]>([]);
  const [sortBy, setSortBy] = useState<SortOption>('addedAt');
  const [sortDescending, setSortDescending] = useState(true);
  const [page, setPage] = useState(1);
  const pageSize = 20;

  // Query params
  const queryParams: GetUserLibraryParams = useMemo(() => ({
    page,
    pageSize,
    search: searchQuery || undefined,
    favoritesOnly: favoritesOnly || undefined,
    stateFilter: stateFilter.length > 0 ? stateFilter : undefined,
    sortBy: sortBy === 'playCount' ? 'addedAt' : sortBy, // API doesn't support playCount yet
    sortDescending,
  }), [page, pageSize, searchQuery, favoritesOnly, stateFilter, sortBy, sortDescending]);

  // Data fetching
  const { data: libraryData, isLoading: isLoadingLibrary, error: libraryError } = useLibrary(queryParams);
  const { data: statsData, isLoading: isLoadingStats } = useLibraryStats();
  const { data: quotaData } = useLibraryQuota();

  // Derived state
  const heroStats: HeroStat[] = useMemo(() => {
    if (!statsData) return [];
    return [
      {
        id: 'total',
        icon: Library,
        value: statsData.totalGames,
        label: 'Giochi',
        color: 'amber',
      },
      {
        id: 'favorites',
        icon: Heart,
        value: statsData.favoriteGames,
        label: 'Preferiti',
        color: 'purple',
      },
      {
        id: 'quota',
        icon: TrendingUp,
        value: quotaData?.remainingSlots ?? 0,
        label: 'Slot liberi',
        color: 'teal',
      },
      {
        id: 'usage',
        icon: Dices,
        value: quotaData?.currentCount ?? statsData.totalGames,
        label: 'In uso',
        color: 'emerald',
      },
    ];
  }, [statsData, quotaData]);

  const stateCounts = useMemo(() => {
    if (!statsData) return undefined;
    // Note: State counts would need backend API enhancement to provide byState breakdown
    // For now, return only what the API provides
    return {
      total: statsData.totalGames,
      favorites: statsData.favoriteGames,
      nuovo: 0, // TODO: API enhancement needed
      inPrestito: 0, // TODO: API enhancement needed
      wishlist: 0, // TODO: API enhancement needed
      owned: statsData.totalGames - statsData.favoriteGames, // Approximation
    };
  }, [statsData]);

  const hasActiveFilters = Boolean(searchQuery) || favoritesOnly || stateFilter.length > 0;

  // Handlers
  const handleSortChange = useCallback((newSortBy: SortOption, descending: boolean) => {
    setSortBy(newSortBy);
    setSortDescending(descending);
    setPage(1);
  }, []);

  const handleClearFilters = useCallback(() => {
    setSearchQuery('');
    setFavoritesOnly(false);
    setStateFilter([]);
    setSortBy('addedAt');
    setSortDescending(true);
    setPage(1);
  }, []);

  const handleSearchChange = useCallback((query: string) => {
    setSearchQuery(query);
    setPage(1);
  }, []);

  const handleFavoritesChange = useCallback((enabled: boolean) => {
    setFavoritesOnly(enabled);
    setPage(1);
  }, []);

  const handleStateFilterChange = useCallback((states: GameStateType[]) => {
    setStateFilter(states);
    setPage(1);
  }, []);

  // Map library entries to MeepleCard format (CollectionGame type)
  const games = useMemo(() => {
    if (!libraryData?.items) return [];
    return libraryData.items.map(entry => {
      const stateToStatus: Record<string, 'owned' | 'wishlisted' | 'borrowed'> = {
        Owned: 'owned',
        Nuovo: 'owned',
        Wishlist: 'wishlisted',
        InPrestito: 'borrowed',
      };
      return {
        id: entry.gameId,
        title: entry.gameTitle,
        thumbnailUrl: entry.gameIconUrl ?? undefined,
        imageUrl: entry.gameImageUrl ?? undefined,
        yearPublished: entry.gameYearPublished ?? undefined,
        addedAt: entry.addedAt,
        playCount: 0,
        hasPdf: entry.hasKb,
        hasActiveChat: false,
        chatCount: 0,
        status: stateToStatus[entry.currentState] ?? 'owned',
      };
    });
  }, [libraryData]);

  // Transform games to CarouselGame format for carousel view
  const carouselGames: CarouselGame[] = useMemo(() => {
    if (!libraryData?.items) return [];
    return libraryData.items.map(entry => ({
      id: entry.gameId,
      title: entry.gameTitle,
      subtitle: entry.gamePublisher ?? undefined,
      imageUrl: entry.gameImageUrl ?? entry.gameIconUrl ?? undefined,
      hasKb: entry.hasKb,
    }));
  }, [libraryData]);

  return (
    <div className={cn('space-y-6', className)} data-testid={COLLECTION_TEST_IDS.dashboard}>
      {/* Hero Stats Section */}
      <section aria-label="Collection statistics">
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
          {isLoadingStats ? (
            Array.from({ length: 4 }).map((_, i) => <HeroStatSkeleton key={i} />)
          ) : (
            heroStats.map((stat, index) => (
              <HeroStatCard key={stat.id} stat={stat} index={index} />
            ))
          )}
        </div>
      </section>

      {/* Toolbar Section */}
      <section aria-label="Collection filters and actions">
        <CollectionToolbar
          searchQuery={searchQuery}
          onSearchChange={handleSearchChange}
          viewMode={viewMode}
          onViewModeChange={setViewMode}
          sortBy={sortBy}
          sortDescending={sortDescending}
          onSortChange={handleSortChange}
          favoritesOnly={favoritesOnly}
          onFavoritesChange={handleFavoritesChange}
          stateFilter={stateFilter}
          onStateFilterChange={handleStateFilterChange}
          onClearFilters={handleClearFilters}
          stateCounts={stateCounts}
        />
      </section>

      {/* Games Section */}
      <section aria-label="Game collection">
        {isLoadingLibrary ? (
          <GameGridSkeleton count={8} />
        ) : libraryError ? (
          <div className="flex flex-col items-center justify-center py-16 text-center">
            <p className="text-destructive mb-4">
              Errore nel caricamento della collezione.
            </p>
            <Button onClick={() => window.location.reload()}>Riprova</Button>
          </div>
        ) : games.length === 0 ? (
          <EmptyState hasFilters={hasActiveFilters} />
        ) : (
          <>
            {/* Results count */}
            <div className="flex items-center justify-between mb-4">
              <p className="text-sm text-muted-foreground">
                {libraryData?.totalCount === 1
                  ? '1 gioco'
                  : `${libraryData?.totalCount || 0} giochi`}
                {hasActiveFilters && ' trovati'}
              </p>
            </div>

            {/* Game Grid / Carousel */}
            {viewMode === 'carousel' ? (
              <GameCarousel
                games={carouselGames}
                flippable={false}
                showDots
                data-testid="collection-carousel"
              />
            ) : (
              <AnimatePresence mode="wait">
                <motion.div
                  key={`${viewMode}-${page}`}
                  initial={{ opacity: 0 }}
                  animate={{ opacity: 1 }}
                  exit={{ opacity: 0 }}
                  transition={{ duration: 0.2 }}
                  className={cn(
                    viewMode === 'grid'
                      ? 'grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4'
                      : 'flex flex-col gap-3'
                  )}
                  data-testid={COLLECTION_TEST_IDS.grid}
                >
                  {games.map(game => (
                    <MeepleCard
                      key={game.id}
                      id={game.id}
                      entity="game"
                      title={game.title}
                      imageUrl={game.imageUrl}
                      variant={viewMode === 'list' ? 'list' : 'grid'}
                      status={game.status}
                      metadata={[
                        { label: t('collection.year'), value: game.yearPublished?.toString() },
                        { label: t('collection.plays'), value: game.playCount.toString() },
                      ].filter(m => m.value) as Array<{ label: string; value: string }>}
                    />
                  ))}
                </motion.div>
              </AnimatePresence>
            )}

            {/* Pagination */}
            {libraryData && libraryData.totalPages > 1 && (
              <div className="flex items-center justify-center gap-2 mt-8">
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setPage(p => Math.max(1, p - 1))}
                  disabled={page === 1}
                >
                  Precedente
                </Button>
                <span className="text-sm text-muted-foreground px-4">
                  Pagina {page} di {libraryData.totalPages}
                </span>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setPage(p => Math.min(libraryData.totalPages, p + 1))}
                  disabled={page === libraryData.totalPages}
                >
                  Successiva
                </Button>
              </div>
            )}
          </>
        )}
      </section>
    </div>
  );
}

export default CollectionDashboard;

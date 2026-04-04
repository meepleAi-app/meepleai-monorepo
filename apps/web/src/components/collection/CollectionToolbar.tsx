'use client';

import { useCallback, useRef, useState } from 'react';

import {
  BookOpen,
  ChevronDown,
  GalleryHorizontalEnd,
  Gamepad2,
  Grid3X3,
  Heart,
  LayoutGrid,
  List,
  Package,
  Plus,
  Search,
  Share2,
  SortAsc,
  Sparkles,
  Star,
  X,
} from 'lucide-react';
import Link from 'next/link';

import { Badge } from '@/components/ui/data-display/badge';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/navigation/dropdown-menu';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { useTranslation } from '@/hooks/useTranslation';
import type { GameStateType } from '@/lib/api/schemas/library.schemas';
import { COLLECTION_TEST_IDS } from '@/lib/test-ids';
import { cn } from '@/lib/utils';

export type ViewMode = 'grid' | 'list' | 'carousel';
export type SortOption = 'addedAt' | 'title' | 'favorite' | 'playCount';

interface FilterChip {
  id: 'all' | 'favorites' | GameStateType;
  label: string;
  icon: React.ReactNode;
  color: string;
  activeColor: string;
}

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

export interface CollectionToolbarProps {
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
}

export function CollectionToolbar({
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
}: CollectionToolbarProps) {
  const { t } = useTranslation();
  const [localSearch, setLocalSearch] = useState(searchQuery);
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const handleSearchInput = useCallback(
    (value: string) => {
      setLocalSearch(value);
      if (debounceRef.current) clearTimeout(debounceRef.current);
      debounceRef.current = setTimeout(() => {
        onSearchChange(value);
      }, 300);
    },
    [onSearchChange]
  );

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
      case 'all':
        return stateCounts.total;
      case 'favorites':
        return stateCounts.favorites;
      case 'Nuovo':
        return stateCounts.nuovo;
      case 'InPrestito':
        return stateCounts.inPrestito;
      case 'Wishlist':
        return stateCounts.wishlist;
      case 'Owned':
        return stateCounts.owned;
      default:
        return undefined;
    }
  };

  const hasActiveFilters = Boolean(searchQuery) || favoritesOnly || stateFilter.length > 0;

  return (
    <section aria-label="Collection filters and actions">
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
                  <Link href="/library" className="flex items-center gap-2 cursor-pointer">
                    <BookOpen className="h-4 w-4" />
                    {t('collection.addFromCatalog')}
                  </Link>
                </DropdownMenuItem>
                <DropdownMenuItem asChild>
                  <Link
                    href="/library/private/add"
                    className="flex items-center gap-2 cursor-pointer"
                  >
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
            <Button variant="ghost" size="sm" onClick={onClearFilters} className="h-8 text-xs">
              <X className="mr-1 h-3 w-3" />
              Pulisci
            </Button>
          )}
        </div>
      </div>
    </section>
  );
}

/**
 * CatalogFilters Component (Issue #2518)
 *
 * Sidebar filters for shared games catalog with:
 * - Search bar
 * - Complexity slider (1-5)
 * - Players count dropdown
 * - Playtime dropdown
 * - Categories multi-select
 * - Mechanics multi-select
 * - Sort options
 * - Clear filters button
 */

'use client';

import { useState, useEffect } from 'react';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Checkbox } from '@/components/ui/checkbox';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Search, X, ArrowUpDown } from 'lucide-react';

import type { GameCategory, GameMechanic } from '@/lib/api';

interface CatalogFiltersProps {
  // Search
  searchTerm: string;
  onSearchChange: (term: string) => void;

  // Categories
  categories: GameCategory[];
  selectedCategories: string[];
  onCategoryChange: (categoryIds: string[]) => void;

  // Mechanics
  mechanics: GameMechanic[];
  selectedMechanics: string[];
  onMechanicChange: (mechanicIds: string[]) => void;

  // Players
  minPlayers?: number;
  maxPlayers?: number;
  onPlayersChange: (min?: number, max?: number) => void;

  // Playtime
  maxPlayingTime?: number;
  onPlaytimeChange: (max?: number) => void;

  // Sort
  sortBy: 'title' | 'complexity' | 'playingTime';
  sortDescending: boolean;
  onSortChange: (sortBy: 'title' | 'complexity' | 'playingTime', descending: boolean) => void;

  // Clear
  onClearFilters: () => void;
}

export function CatalogFilters({
  searchTerm,
  onSearchChange,
  categories,
  selectedCategories,
  onCategoryChange,
  mechanics,
  selectedMechanics,
  onMechanicChange,
  minPlayers,
  maxPlayers,
  onPlayersChange,
  maxPlayingTime,
  onPlaytimeChange,
  sortBy,
  sortDescending,
  onSortChange,
  onClearFilters,
}: CatalogFiltersProps) {
  // Local search with debounce
  const [localSearch, setLocalSearch] = useState(searchTerm);

  useEffect(() => {
    const timer = setTimeout(() => {
      onSearchChange(localSearch);
    }, 300); // 300ms debounce

    return () => clearTimeout(timer);
  }, [localSearch, onSearchChange]);

  // Category toggle
  const toggleCategory = (categoryId: string) => {
    if (selectedCategories.includes(categoryId)) {
      onCategoryChange(selectedCategories.filter((id) => id !== categoryId));
    } else {
      onCategoryChange([...selectedCategories, categoryId]);
    }
  };

  // Mechanic toggle
  const toggleMechanic = (mechanicId: string) => {
    if (selectedMechanics.includes(mechanicId)) {
      onMechanicChange(selectedMechanics.filter((id) => id !== mechanicId));
    } else {
      onMechanicChange([...selectedMechanics, mechanicId]);
    }
  };

  // Has active filters
  const hasFilters =
    searchTerm ||
    selectedCategories.length > 0 ||
    selectedMechanics.length > 0 ||
    minPlayers !== undefined ||
    maxPlayers !== undefined ||
    maxPlayingTime !== undefined;

  return (
    <div className="space-y-4">
      {/* Search */}
      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Ricerca</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="relative">
            <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
            <Input
              placeholder="Cerca gioco..."
              value={localSearch}
              onChange={(e) => setLocalSearch(e.target.value)}
              className="pl-8"
            />
            {localSearch && (
              <Button
                variant="ghost"
                size="sm"
                className="absolute right-1 top-1 h-7 w-7 p-0"
                onClick={() => setLocalSearch('')}
              >
                <X className="h-4 w-4" />
              </Button>
            )}
          </div>
        </CardContent>
      </Card>

      {/* Sort */}
      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base flex items-center">
            <ArrowUpDown className="mr-2 h-4 w-4" />
            Ordinamento
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          <div>
            <Label htmlFor="sort-by" className="text-sm">
              Ordina per
            </Label>
            <Select
              value={sortBy}
              onValueChange={(value) =>
                onSortChange(value as typeof sortBy, sortDescending)
              }
            >
              <SelectTrigger id="sort-by">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="title">Nome</SelectItem>
                <SelectItem value="complexity">Complessità</SelectItem>
                <SelectItem value="playingTime">Durata</SelectItem>
              </SelectContent>
            </Select>
          </div>
          <div>
            <Label htmlFor="sort-order" className="text-sm">
              Direzione
            </Label>
            <Select
              value={sortDescending ? 'desc' : 'asc'}
              onValueChange={(value) => onSortChange(sortBy, value === 'desc')}
            >
              <SelectTrigger id="sort-order">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="asc">Crescente</SelectItem>
                <SelectItem value="desc">Decrescente</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </CardContent>
      </Card>

      {/* Players */}
      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Numero Giocatori</CardTitle>
        </CardHeader>
        <CardContent>
          <Select
            value={
              minPlayers !== undefined && maxPlayers !== undefined
                ? `${minPlayers}-${maxPlayers}`
                : 'any'
            }
            onValueChange={(value) => {
              if (value === 'any') {
                onPlayersChange(undefined, undefined);
              } else {
                const [min, max] = value.split('-').map(Number);
                onPlayersChange(min, max);
              }
            }}
          >
            <SelectTrigger>
              <SelectValue placeholder="Qualsiasi" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="any">Qualsiasi</SelectItem>
              <SelectItem value="1-1">Solo</SelectItem>
              <SelectItem value="2-2">2 giocatori</SelectItem>
              <SelectItem value="3-4">3-4 giocatori</SelectItem>
              <SelectItem value="5-6">5-6 giocatori</SelectItem>
              <SelectItem value="7-99">7+ giocatori</SelectItem>
            </SelectContent>
          </Select>
        </CardContent>
      </Card>

      {/* Playtime */}
      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Durata di Gioco</CardTitle>
        </CardHeader>
        <CardContent>
          <Select
            value={maxPlayingTime?.toString() || 'any'}
            onValueChange={(value) => {
              if (value === 'any') {
                onPlaytimeChange(undefined);
              } else {
                onPlaytimeChange(Number(value));
              }
            }}
          >
            <SelectTrigger>
              <SelectValue placeholder="Qualsiasi" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="any">Qualsiasi</SelectItem>
              <SelectItem value="30">&lt; 30 min</SelectItem>
              <SelectItem value="60">30-60 min</SelectItem>
              <SelectItem value="120">60-120 min</SelectItem>
              <SelectItem value="180">120-180 min</SelectItem>
              <SelectItem value="999">180+ min</SelectItem>
            </SelectContent>
          </Select>
        </CardContent>
      </Card>

      {/* Categories */}
      <Card>
        <CardHeader className="pb-3">
          <div className="flex items-center justify-between">
            <CardTitle className="text-base">Categorie</CardTitle>
            {selectedCategories.length > 0 && (
              <Badge variant="secondary" className="text-xs">
                {selectedCategories.length}
              </Badge>
            )}
          </div>
        </CardHeader>
        <CardContent>
          <ScrollArea className="h-48">
            <div className="space-y-2">
              {categories.map((category) => (
                <div key={category.id} className="flex items-center space-x-2">
                  <Checkbox
                    id={`category-${category.id}`}
                    checked={selectedCategories.includes(category.id)}
                    onCheckedChange={() => toggleCategory(category.id)}
                  />
                  <Label
                    htmlFor={`category-${category.id}`}
                    className="text-sm font-normal cursor-pointer"
                  >
                    {category.name}
                  </Label>
                </div>
              ))}
            </div>
          </ScrollArea>
        </CardContent>
      </Card>

      {/* Mechanics */}
      <Card>
        <CardHeader className="pb-3">
          <div className="flex items-center justify-between">
            <CardTitle className="text-base">Meccaniche</CardTitle>
            {selectedMechanics.length > 0 && (
              <Badge variant="secondary" className="text-xs">
                {selectedMechanics.length}
              </Badge>
            )}
          </div>
        </CardHeader>
        <CardContent>
          <ScrollArea className="h-48">
            <div className="space-y-2">
              {mechanics.map((mechanic) => (
                <div key={mechanic.id} className="flex items-center space-x-2">
                  <Checkbox
                    id={`mechanic-${mechanic.id}`}
                    checked={selectedMechanics.includes(mechanic.id)}
                    onCheckedChange={() => toggleMechanic(mechanic.id)}
                  />
                  <Label
                    htmlFor={`mechanic-${mechanic.id}`}
                    className="text-sm font-normal cursor-pointer"
                  >
                    {mechanic.name}
                  </Label>
                </div>
              ))}
            </div>
          </ScrollArea>
        </CardContent>
      </Card>

      {/* Clear Filters */}
      {hasFilters && (
        <Button variant="outline" onClick={onClearFilters} className="w-full">
          <X className="mr-2 h-4 w-4" />
          Rimuovi Tutti i Filtri
        </Button>
      )}
    </div>
  );
}

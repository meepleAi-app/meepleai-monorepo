/**
 * FilterBar Component - Issue #4581
 * Category and sort filters for game collection
 */

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';

interface FilterBarProps {
  categories: string[];
  currentCategory: string;
  currentSort: string;
  onCategoryChange: (category: string) => void;
  onSortChange: (sort: string) => void;
}

export function FilterBar({
  categories,
  currentCategory,
  currentSort,
  onCategoryChange,
  onSortChange,
}: FilterBarProps) {
  const categoryLabels: Record<string, string> = {
    all: 'Tutti',
    strategy: 'Strategia',
    family: 'Famiglia',
    party: 'Party',
    solo: 'Solo',
    cooperative: 'Cooperativi',
  };

  const sortLabels: Record<string, string> = {
    alphabetical: 'Alfabetico',
    playCount: 'Più Giocati',
    rating: 'Rating',
    lastPlayed: 'Ultimi Giocati',
  };

  return (
    <div className="flex flex-col sm:flex-row gap-4 mb-6">
      {/* Category filters */}
      <div className="flex flex-wrap gap-2">
        {categories.map(cat => (
          <Button
            key={cat}
            variant={currentCategory === cat ? 'default' : 'outline'}
            size="sm"
            onClick={() => onCategoryChange(cat)}
            className={
              currentCategory === cat
                ? 'bg-amber-100 text-amber-900 hover:bg-amber-200 border-amber-300'
                : ''
            }
          >
            {categoryLabels[cat] || cat}
          </Button>
        ))}
      </div>

      {/* Sort dropdown */}
      <Select value={currentSort} onValueChange={onSortChange}>
        <SelectTrigger className="w-full sm:w-48">
          <SelectValue placeholder="Ordina per..." />
        </SelectTrigger>
        <SelectContent>
          {Object.entries(sortLabels).map(([value, label]) => (
            <SelectItem key={value} value={value}>
              {label}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  );
}

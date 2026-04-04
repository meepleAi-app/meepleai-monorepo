'use client';

import { Search } from 'lucide-react';

import type { ComponentCategory } from '@/components/showcase/types';
import { Badge } from '@/components/ui/data-display/badge';
import { Input } from '@/components/ui/primitives/input';
import type { AppArea } from '@/config/component-registry';

export interface LibraryFilters {
  category?: ComponentCategory;
  area?: AppArea;
  tier?: 'interactive' | 'static';
  search?: string;
}

interface SearchFilterProps {
  categories: { category: ComponentCategory; count: number }[];
  areas: { area: AppArea; count: number }[];
  filters: LibraryFilters;
  onChange: (filters: LibraryFilters) => void;
  totalCount: number;
  filteredCount: number;
}

const ALL_VALUE = '__all__';

export function SearchFilter({
  categories,
  areas,
  filters,
  onChange,
  totalCount,
  filteredCount,
}: SearchFilterProps) {
  const handleSearch = (e: React.ChangeEvent<HTMLInputElement>) => {
    onChange({ ...filters, search: e.target.value || undefined });
  };

  const handleCategory = (e: React.ChangeEvent<HTMLSelectElement>) => {
    const val = e.target.value;
    onChange({ ...filters, category: val === ALL_VALUE ? undefined : (val as ComponentCategory) });
  };

  const handleArea = (e: React.ChangeEvent<HTMLSelectElement>) => {
    const val = e.target.value;
    onChange({ ...filters, area: val === ALL_VALUE ? undefined : (val as AppArea) });
  };

  const handleTier = (e: React.ChangeEvent<HTMLSelectElement>) => {
    const val = e.target.value;
    onChange({
      ...filters,
      tier: val === ALL_VALUE ? undefined : (val as 'interactive' | 'static'),
    });
  };

  const isFiltered = filteredCount < totalCount;

  return (
    <div className="flex flex-wrap items-center gap-3">
      {/* Search */}
      <div className="relative min-w-[220px] flex-1">
        <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
        <Input
          placeholder="Search components..."
          value={filters.search ?? ''}
          onChange={handleSearch}
          className="pl-9 font-nunito"
          aria-label="Search components"
        />
      </div>

      {/* Category */}
      <select
        value={filters.category ?? ALL_VALUE}
        onChange={handleCategory}
        aria-label="Filter by category"
        className="h-9 rounded-md border border-input bg-background px-3 py-1 text-sm font-nunito shadow-sm focus:outline-none focus:ring-1 focus:ring-ring"
      >
        <option value={ALL_VALUE}>All Categories</option>
        {categories.map(({ category, count }) => (
          <option key={category} value={category}>
            {category} ({count})
          </option>
        ))}
      </select>

      {/* Area */}
      <select
        value={filters.area ?? ALL_VALUE}
        onChange={handleArea}
        aria-label="Filter by area"
        className="h-9 rounded-md border border-input bg-background px-3 py-1 text-sm font-nunito shadow-sm focus:outline-none focus:ring-1 focus:ring-ring"
      >
        <option value={ALL_VALUE}>All Areas</option>
        {areas.map(({ area, count }) => (
          <option key={area} value={area}>
            {area} ({count})
          </option>
        ))}
      </select>

      {/* Tier */}
      <select
        value={filters.tier ?? ALL_VALUE}
        onChange={handleTier}
        aria-label="Filter by tier"
        className="h-9 rounded-md border border-input bg-background px-3 py-1 text-sm font-nunito shadow-sm focus:outline-none focus:ring-1 focus:ring-ring"
      >
        <option value={ALL_VALUE}>All Tiers</option>
        <option value="interactive">Interactive</option>
        <option value="static">Static</option>
      </select>

      {/* Count badge */}
      <Badge
        variant={isFiltered ? 'default' : 'outline'}
        className={isFiltered ? 'bg-amber-100 text-amber-900 border-amber-200' : ''}
        aria-live="polite"
        aria-label={`Showing ${filteredCount} of ${totalCount} components`}
      >
        {filteredCount} / {totalCount}
      </Badge>
    </div>
  );
}

'use client';

import { useState } from 'react';

import { Layers } from 'lucide-react';
import Link from 'next/link';

import { LibraryGrid } from '@/components/admin/ui-library/LibraryGrid';
import { LibrarySidebar } from '@/components/admin/ui-library/LibrarySidebar';
import { SearchFilter } from '@/components/admin/ui-library/SearchFilter';
import type { LibraryFilters } from '@/components/admin/ui-library/SearchFilter';
import {
  COMPONENT_REGISTRY,
  filterRegistry,
  getCategories,
  getAreas,
} from '@/config/component-registry';

const ALL_CATEGORIES = getCategories();
const ALL_AREAS = getAreas();
const TOTAL_COUNT = COMPONENT_REGISTRY.length;

export default function UILibraryPage() {
  const [filters, setFilters] = useState<LibraryFilters>({});

  const filtered = filterRegistry(filters);
  const hasActiveFilters =
    Boolean(filters.category) ||
    Boolean(filters.area) ||
    Boolean(filters.tier) ||
    Boolean(filters.search);

  return (
    <div className="space-y-6 p-6">
      {/* Page header */}
      <div>
        <h1 className="font-quicksand text-2xl font-bold text-foreground">UI Library</h1>
        <p className="mt-1 font-nunito text-sm text-muted-foreground">
          Browse all {TOTAL_COUNT} components, view live previews, and explore usage patterns.
        </p>
      </div>

      {/* Search and filters */}
      <SearchFilter
        categories={ALL_CATEGORIES}
        areas={ALL_AREAS}
        filters={filters}
        onChange={setFilters}
        totalCount={TOTAL_COUNT}
        filteredCount={filtered.length}
      />

      {/* Main content: sidebar + grid */}
      <div className="flex gap-6">
        <LibrarySidebar
          categories={ALL_CATEGORIES}
          areas={ALL_AREAS}
          filters={filters}
          onChange={setFilters}
        />

        <div className="min-w-0 flex-1">
          <LibraryGrid entries={filtered} />
        </div>
      </div>

      {/* Compositions section — visible only when no filters active */}
      {!hasActiveFilters && (
        <section className="space-y-3 rounded-xl border border-border/60 bg-muted/20 p-6">
          <div className="flex items-center gap-2">
            <Layers className="h-5 w-5 text-muted-foreground" aria-hidden="true" />
            <h2 className="font-quicksand text-lg font-semibold text-foreground">Compositions</h2>
          </div>
          <p className="font-nunito text-sm text-muted-foreground">
            Explore how components are composed together to build complete UI patterns.
          </p>
          <Link
            href="/admin/ui-library/compositions"
            className="inline-flex items-center rounded-lg border border-border/60 bg-background px-4 py-2 font-nunito text-sm text-muted-foreground transition-colors hover:border-amber-200 hover:bg-amber-50 hover:text-amber-900 dark:hover:bg-amber-950/20 dark:hover:text-amber-300"
          >
            View Compositions
          </Link>
        </section>
      )}
    </div>
  );
}

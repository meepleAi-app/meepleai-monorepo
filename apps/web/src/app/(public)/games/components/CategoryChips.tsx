/**
 * CategoryChips Component
 *
 * Client component wrapper around FilterChipsRow for game category filtering.
 * Updates URL search params to filter the catalog by category.
 *
 * Categories are presentational quick-filters (not tied to backend category IDs).
 * They map to search terms or known category slugs for server-side filtering.
 */

'use client';

import { useCallback } from 'react';

import { usePathname, useRouter, useSearchParams } from 'next/navigation';

import { FilterChipsRow } from '@/components/ui/FilterChipsRow';

const CATEGORY_CHIPS = [
  { id: 'all', label: 'Tutti' },
  { id: 'strategici', label: 'Strategici' },
  { id: 'party', label: 'Party' },
  { id: 'cooperativi', label: 'Cooperativi' },
  { id: 'solo', label: 'Solo' },
  { id: 'nuove-uscite', label: 'Nuove uscite' },
];

export function CategoryChips() {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();

  const activeCategory = searchParams?.get('category') || 'all';

  const handleSelect = useCallback(
    (id: string) => {
      const params = new URLSearchParams(searchParams?.toString() || '');

      if (id === 'all') {
        params.delete('category');
      } else {
        params.set('category', id);
      }

      // Reset to page 1 when changing category
      params.delete('page');

      const qs = params.toString();
      router.push(`${pathname}${qs ? `?${qs}` : ''}`, { scroll: false });
    },
    [router, pathname, searchParams]
  );

  return (
    <FilterChipsRow
      chips={CATEGORY_CHIPS}
      activeId={activeCategory}
      onSelect={handleSelect}
      className="mb-4"
    />
  );
}

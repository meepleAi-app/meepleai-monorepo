/**
 * Shared Games Catalog Page (Issue #2518, #2876)
 *
 * Route: /games/catalog
 * Features:
 * - Hero section with Top 5 BGG Rating + Latest 5 Added
 * - Responsive grid (1→2→3 columns)
 * - Search with debounce
 * - Advanced filters (complexity, players, playtime, categories, mechanics)
 * - Pagination with results display: 'Page X of Y • N results'
 * - URL query params for deep linking (#2876)
 * - Add to library functionality
 * - "Already in library" badges
 *
 * @see Issue #2518 User Library - Catalog, Library & Agent Configuration UI
 * @see Issue #2876 Pagination Component enhancements
 */

import { Suspense } from 'react';

import { MeepleGameCatalogCardSkeleton } from '@/components/catalog/MeepleGameCatalogCard';

import { CatalogContent } from './_content';

export default function SharedGamesCatalogPage() {
  return (
    <Suspense
      fallback={
        <div className="min-h-screen bg-background pb-24 md:pb-0 md:pt-16">
          <div className="container mx-auto px-4 py-8 max-w-7xl">
            <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-3 md:gap-4">
              {Array.from({ length: 6 }).map((_, i) => (
                <MeepleGameCatalogCardSkeleton key={i} />
              ))}
            </div>
          </div>
        </div>
      }
    >
      <CatalogContent />
    </Suspense>
  );
}

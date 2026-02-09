/**
 * EntityListView Complete Demo - Phase 6 Integration Example
 *
 * Demonstrates full EntityListView functionality:
 * - All 3 view modes (Grid/List/Carousel)
 * - Search with debounce
 * - Sort with multiple options
 * - Filter panel (MVP stub)
 * - localStorage persistence
 *
 * This serves as reference for dashboard integration (#3881).
 */

'use client';

import { useState, useEffect } from 'react';

import { Star, TrendingUp, Calendar, ArrowDownAZ } from 'lucide-react';

import { EntityListView } from '@/components/ui/data-display/entity-list-view';
import type { SortOption, FilterConfig } from '@/components/ui/data-display/entity-list-view/entity-list-view.types';
import { createComparator } from '@/components/ui/data-display/entity-list-view/utils/sort-utils';

// Mock game data
interface Game {
  id: string;
  title: string;
  publisher: string;
  yearPublished: number;
  rating: number;
  imageUrl?: string;
}

const mockGames: Game[] = [
  {
    id: '1',
    title: 'Gloomhaven',
    publisher: 'Cephalofair Games',
    yearPublished: 2017,
    rating: 8.8,
    imageUrl: '/images/games/gloomhaven.jpg',
  },
  {
    id: '2',
    title: 'Twilight Imperium (Fourth Edition)',
    publisher: 'Fantasy Flight Games',
    yearPublished: 2017,
    rating: 8.7,
  },
  {
    id: '3',
    title: 'Wingspan',
    publisher: 'Stonemaier Games',
    yearPublished: 2019,
    rating: 8.1,
  },
  {
    id: '4',
    title: 'Azul',
    publisher: 'Plan B Games',
    yearPublished: 2017,
    rating: 7.8,
  },
  {
    id: '5',
    title: 'Terraforming Mars',
    publisher: 'FryxGames',
    yearPublished: 2016,
    rating: 8.4,
  },
];

// Sort options for games
const gameSortOptions: SortOption<Game>[] = [
  {
    value: 'rating',
    label: 'Rating (High to Low)',
    icon: Star,
    compareFn: createComparator('rating', 'rating'),
  },
  {
    value: 'name',
    label: 'Name (A-Z)',
    icon: ArrowDownAZ,
    compareFn: createComparator('title', 'alphabetical'),
  },
  {
    value: 'year',
    label: 'Year Published',
    icon: Calendar,
    compareFn: createComparator('yearPublished', 'numeric'),
  },
  {
    value: 'popularity',
    label: 'Popularity',
    icon: TrendingUp,
    compareFn: (a, b) => b.rating * 10 - a.rating * 10, // Mock popularity
  },
];

// Filter configuration for games
const gameFilters: FilterConfig<Game>[] = [
  {
    id: 'players',
    type: 'range',
    label: 'Number of Players',
    field: 'maxPlayers',
    min: 1,
    max: 8,
    unit: ' players',
    description: 'Filter by max player count',
  },
  {
    id: 'rating',
    type: 'range',
    label: 'Minimum Rating',
    field: 'rating',
    min: 1,
    max: 10,
    step: 0.1,
    unit: '/10',
  },
  {
    id: 'year',
    type: 'select',
    label: 'Year Published',
    field: 'yearPublished',
    options: [
      { value: '2019', label: '2019' },
      { value: '2017', label: '2017' },
      { value: '2016', label: '2016' },
    ],
  },
  {
    id: 'hasImage',
    type: 'checkbox',
    label: 'Has Cover Image',
    field: 'imageUrl',
    description: 'Only show games with cover art',
  },
];

export default function EntityListCompleteDemoPage() {
  const [isClient, setIsClient] = useState(false);

  useEffect(() => {
    setIsClient(true);
  }, []);

  if (!isClient) {
    return (
      <div className="container mx-auto p-6">
        <div className="text-center text-muted-foreground">Loading...</div>
      </div>
    );
  }

  return (
    <div className="container mx-auto p-6 space-y-8">
      <div className="space-y-2">
        <h1 className="text-4xl font-quicksand font-bold">EntityListView Demo</h1>
        <p className="text-muted-foreground">
          Complete demonstration of EntityListView with search, sort, and all view modes.
        </p>
        <p className="text-sm text-muted-foreground">
          Epic #3875 • Phase 6 Integration Example
        </p>
      </div>

      {/* Main Demo */}
      <EntityListView
        items={mockGames}
        entity="game"
        persistenceKey="demo-complete"
        renderItem={(game) => ({
          id: game.id,
          title: game.title,
          subtitle: game.publisher,
          rating: game.rating,
          ratingMax: 10,
          imageUrl: game.imageUrl,
          metadata: [
            { label: 'Year', value: game.yearPublished.toString(), icon: Calendar },
          ],
        })}
        // eslint-disable-next-line no-console
        onItemClick={(game) => console.log('Clicked:', game.title)}
        title="Featured Games"
        subtitle="Browse, search, and sort your collection"
        searchable
        searchPlaceholder="Search games by title or publisher..."
        searchFields={['title', 'publisher']}
        sortOptions={gameSortOptions}
        defaultSort="rating"
        filters={gameFilters}
        carouselOptions={{
          autoPlay: false,
          showDots: true,
        }}
      />

      {/* Usage Example */}
      <div className="mt-12 p-6 bg-muted/50 rounded-lg">
        <h2 className="text-xl font-semibold mb-4">Integration Example</h2>
        <pre className="text-sm overflow-x-auto">
{`<EntityListView
  items={games}
  entity="game"
  persistenceKey="games-dashboard"
  renderItem={(game) => ({
    id: game.id,
    title: game.title,
    subtitle: game.publisher,
    rating: game.averageRating,
    ratingMax: 10,
  })}
  searchable
  searchFields={['title', 'publisher']}
  sortOptions={gameSortOptions}
  title="My Games"
/>`}
        </pre>
      </div>

      {/* Feature Checklist */}
      <div className="grid md:grid-cols-2 gap-6">
        <div className="space-y-2">
          <h3 className="font-semibold">✅ Implemented (Phase 1-3)</h3>
          <ul className="text-sm space-y-1 text-muted-foreground">
            <li>✓ Grid view with responsive columns</li>
            <li>✓ List view with compact cards</li>
            <li>✓ Carousel view with GameCarousel</li>
            <li>✓ View mode persistence (localStorage)</li>
            <li>✓ Search with 300ms debounce</li>
            <li>✓ Sort with configurable options</li>
            <li>✓ Keyboard navigation (Arrow keys, Cmd/Ctrl+K)</li>
            <li>✓ Empty and loading states</li>
          </ul>
        </div>

        <div className="space-y-2">
          <h3 className="font-semibold">⏳ Planned (Issue #3894)</h3>
          <ul className="text-sm space-y-1 text-muted-foreground">
            <li>○ Filter system (4 types)</li>
            <li>○ Filter persistence</li>
            <li>○ Comprehensive test coverage (100%)</li>
            <li>○ Full accessibility audit</li>
            <li>○ Performance optimization (virtualization)</li>
            <li>○ Complete Storybook documentation</li>
            <li>○ Dashboard-wide migration</li>
          </ul>
        </div>
      </div>
    </div>
  );
}

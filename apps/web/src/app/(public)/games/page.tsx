/**
 * Game Catalog Page (Issue #1838: PAGE-003)
 *
 * Public showcase for SharedGames catalog - browse all available board games.
 * Server Component for SEO-friendly rendering with client components for interactivity.
 *
 * Route: /games
 * Features:
 * - Server-side rendering (SEO-friendly)
 * - URL state persistence (shareable links)
 * - Browser back/forward support
 * - Responsive grid (2→3→4 columns)
 * - Search with debounce (300ms)
 * - Pagination (20 games/page)
 * - "Aggiungi Gioco" button visible only for Editor/Admin roles
 *
 * Data Source: SharedGameCatalog bounded context
 *
 * @see docs/wireframes page 3 "Catalogo Giochi (Hybrid View)"
 * @see Issue #1838 PAGE-003
 */

import { Metadata } from 'next';

import { type GameStatus, type SharedGame } from '@/lib/api';

import { AddGameButton } from './components/AddGameButton';
import { GameGrid } from './components/GameGrid';
import { Pagination } from './components/Pagination';
import { SearchBar } from './components/SearchBar';
import { ViewToggle } from './components/ViewToggle';

// API base URL - prioritize internal Docker network URL for SSR
const API_BASE =
  process.env.API_BASE_URL || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// ============================================================================
// Types
// ============================================================================

interface SearchParams {
  view?: 'grid' | 'list' | 'carousel';
  search?: string;
  page?: string;
  // Filter params
  minPlayers?: string;
  maxPlayers?: string;
  maxPlayingTime?: string;
  minComplexity?: string;
  maxComplexity?: string;
  categoryIds?: string; // comma-separated GUIDs from URL
  mechanicIds?: string; // comma-separated GUIDs from URL
  sortBy?: string;
  sortDesc?: string; // "true" or absent
}

interface FetchGamesParams {
  search?: string;
  page?: number;
  pageSize?: number;
  minPlayers?: number;
  maxPlayers?: number;
  maxPlayingTime?: number;
  minComplexity?: number;
  maxComplexity?: number;
  categoryIds?: string[];
  mechanicIds?: string[];
  sortBy?: string;
  sortDescending?: boolean;
}

/** Raw API response from shared-games endpoint */
interface SharedGamesApiItem {
  id: string;
  title: string;
  yearPublished: number | null;
  minPlayers: number | null;
  maxPlayers: number | null;
  playingTimeMinutes: number | null;
  bggId: number | null;
  complexityRating?: number | null;
  createdAt: string;
  imageUrl?: string | null;
  thumbnailUrl?: string | null;
  averageRating?: number | null;
  description?: string | null;
  status?: string;
}

interface SharedGamesApiResponse {
  items: SharedGamesApiItem[];
  total: number;
  page: number;
  pageSize: number;
}

interface PaginatedResponse {
  games: SharedGame[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

// ============================================================================
// Metadata
// ============================================================================

export const metadata: Metadata = {
  title: 'Catalogo Giochi - MeepleAI',
  description:
    'Esplora il nostro catalogo di giochi da tavolo. Cerca, filtra e scopri le regole dei tuoi giochi preferiti.',
  keywords: ['giochi da tavolo', 'board games', 'catalogo', 'regole', 'MeepleAI'],
  openGraph: {
    title: 'Catalogo Giochi - MeepleAI',
    description: 'Esplora il nostro catalogo di giochi da tavolo',
    type: 'website',
  },
};

// ============================================================================
// Server Actions / Data Fetching
// ============================================================================

/** Map API response item to SharedGame type expected by GameGrid */
function mapApiItemToGame(item: SharedGamesApiItem): SharedGame {
  return {
    id: item.id,
    bggId: item.bggId,
    title: item.title,
    yearPublished: item.yearPublished ?? new Date().getFullYear(),
    description: item.description ?? '',
    minPlayers: item.minPlayers ?? 1,
    maxPlayers: item.maxPlayers ?? 8,
    playingTimeMinutes: item.playingTimeMinutes ?? 60,
    minAge: 0, // Not provided by API
    complexityRating: item.complexityRating ?? null,
    averageRating: item.averageRating ?? null,
    imageUrl: item.imageUrl ?? '',
    thumbnailUrl: item.thumbnailUrl ?? '',
    status: (item.status as GameStatus) ?? 'Published',
    isRagPublic: false,
    createdAt: item.createdAt,
    modifiedAt: null,
  };
}

async function fetchGames(params: FetchGamesParams): Promise<PaginatedResponse> {
  try {
    const sp = new URLSearchParams();
    if (params.search) sp.set('search', params.search);
    sp.set('pageNumber', (params.page ?? 1).toString());
    sp.set('pageSize', (params.pageSize ?? 20).toString());
    if (params.minPlayers) sp.set('minPlayers', params.minPlayers.toString());
    if (params.maxPlayers) sp.set('maxPlayers', params.maxPlayers.toString());
    if (params.maxPlayingTime) sp.set('maxPlayingTime', params.maxPlayingTime.toString());
    if (params.minComplexity) sp.set('minComplexity', params.minComplexity.toString());
    if (params.maxComplexity) sp.set('maxComplexity', params.maxComplexity.toString());
    if (params.sortBy) sp.set('sortBy', params.sortBy);
    if (params.sortDescending) sp.set('sortDescending', 'true');
    // Backend expects repeated params for arrays: categoryIds=x&categoryIds=y
    params.categoryIds?.forEach(id => sp.append('categoryIds', id));
    params.mechanicIds?.forEach(id => sp.append('mechanicIds', id));

    const url = `${API_BASE}/api/v1/shared-games?${sp.toString()}`;
    const response = await fetch(url, {
      next: { revalidate: 60 },
    });

    if (!response.ok) {
      throw new Error(`Failed to fetch games: ${response.status}`);
    }

    const data: SharedGamesApiResponse = await response.json();

    return {
      games: data.items.map(mapApiItemToGame),
      total: data.total,
      page: data.page,
      pageSize: data.pageSize,
      totalPages: Math.ceil(data.total / data.pageSize),
    };
  } catch (error) {
    console.error('Error fetching games:', error);
    return {
      games: [],
      total: 0,
      page: 1,
      pageSize: 20,
      totalPages: 0,
    };
  }
}

// ============================================================================
// Page Component
// ============================================================================

export default async function GamesPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  // Await searchParams (Next.js 15 pattern)
  const params = await searchParams;

  // Parse URL parameters
  const view = params.view || 'grid';
  const search = params.search || '';
  const currentPage = parseInt(params.page || '1', 10);

  // Fetch games server-side with all filter params
  const { games, total, totalPages } = await fetchGames({
    search: search || undefined,
    page: currentPage,
    pageSize: 20,
    minPlayers: params.minPlayers ? parseInt(params.minPlayers, 10) : undefined,
    maxPlayers: params.maxPlayers ? parseInt(params.maxPlayers, 10) : undefined,
    maxPlayingTime: params.maxPlayingTime ? parseInt(params.maxPlayingTime, 10) : undefined,
    minComplexity: params.minComplexity ? parseFloat(params.minComplexity) : undefined,
    maxComplexity: params.maxComplexity ? parseFloat(params.maxComplexity) : undefined,
    categoryIds: params.categoryIds?.split(',').filter(Boolean) || undefined,
    mechanicIds: params.mechanicIds?.split(',').filter(Boolean) || undefined,
    sortBy: params.sortBy || undefined,
    sortDescending: params.sortDesc === 'true',
  });

  return (
    <div className="container mx-auto px-4 py-8 max-w-7xl">
      {/* Header */}
      <div className="flex flex-col md:flex-row justify-between items-start md:items-center mb-8 gap-4">
        <div>
          <h1 className="font-heading text-3xl md:text-4xl font-bold mb-2">Catalogo Giochi</h1>
          <p className="text-muted-foreground">
            Esplora {total} giochi da tavolo. Cerca, filtra e scopri le regole.
          </p>
        </div>
        {/* Add Game button - visible only for Editor/Admin */}
        <AddGameButton />
      </div>

      {/* Toolbar: Search + View Toggle */}
      <div className="flex flex-col sm:flex-row gap-4 items-start sm:items-center justify-between mb-6">
        <div className="w-full sm:w-96">
          <SearchBar currentSearch={search} />
        </div>
        <ViewToggle currentView={view} />
      </div>

      {/* Games Grid/List */}
      <GameGrid games={games} variant={view} />

      {/* Pagination */}
      {totalPages > 1 && (
        <Pagination currentPage={currentPage} totalPages={totalPages} totalItems={total} />
      )}
    </div>
  );
}

/**
 * Game Catalog Page (Issue #1838: PAGE-003)
 *
 * Server Component for browsing games with hybrid grid/list view.
 * Uses URL searchParams for all state (view, search, page).
 *
 * Route: /games
 * Features:
 * - Server-side rendering (SEO-friendly)
 * - URL state persistence (shareable links)
 * - Browser back/forward support
 * - Responsive grid (2→3→4 columns)
 * - Search with debounce (300ms)
 * - Pagination (20 games/page)
 *
 * @see docs/wireframes page 3 "Catalogo Giochi (Hybrid View)"
 * @see Issue #1838 PAGE-003
 */

import { Metadata } from 'next';
import Link from 'next/link';

import { PublicLayoutWrapper } from '@/components/layouts';
import { Button } from '@/components/ui/button';

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
  view?: 'grid' | 'list';
  search?: string;
  page?: string;
}

interface Game {
  id: string;
  title: string;
  publisher: string | null;
  yearPublished: number | null;
  minPlayers: number | null;
  maxPlayers: number | null;
  minPlayTimeMinutes: number | null;
  maxPlayTimeMinutes: number | null;
  bggId: number | null;
  createdAt: string;
  imageUrl?: string | null;
  faqCount?: number | null;
  averageRating?: number | null;
}

interface PaginatedResponse {
  games: Game[];
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

async function fetchGames(
  search?: string,
  page: number = 1,
  pageSize: number = 20
): Promise<PaginatedResponse> {
  try {
    const params = new URLSearchParams();
    if (search) params.set('search', search);
    params.set('page', page.toString());
    params.set('pageSize', pageSize.toString());

    const url = `${API_BASE}/api/v1/games?${params.toString()}`;
    const response = await fetch(url, {
      next: { revalidate: 60 }, // Cache for 60 seconds
    });

    if (!response.ok) {
      throw new Error(`Failed to fetch games: ${response.status}`);
    }

    return response.json();
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

  // Fetch games server-side
  const { games, total, totalPages } = await fetchGames(search, currentPage, 20);

  return (
    <PublicLayoutWrapper containerWidth="xl">
      <div>
        {/* Header */}
        <div className="flex flex-col md:flex-row justify-between items-start md:items-center mb-8 gap-4">
          <div>
            <h1 className="font-heading text-3xl md:text-4xl font-bold mb-2">Catalogo Giochi</h1>
            <p className="text-muted-foreground">
              Esplora {total} giochi da tavolo. Cerca, filtra e scopri le regole.
            </p>
          </div>
          <Link href="/games/add">
            <Button>Aggiungi Gioco</Button>
          </Link>
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
    </PublicLayoutWrapper>
  );
}

/**
 * Board Game AI Game Catalog Page Stories (Issue #1017: BGAI-078)
 *
 * Chromatic visual regression testing for Board Game AI Games Page.
 * Uses the GameCatalogClient component with mocked data.
 *
 * Stories:
 * - DefaultGrid: Grid view with mock games
 * - ListView: List view variant
 * - MobileFlow: 375px mobile viewport
 * - TabletFlow: 768px tablet viewport
 * - DesktopFlow: 1440px desktop viewport
 * - EmptyState: No games found
 * - LoadingState: Skeleton state
 * - WithSearch: Search active
 * - MultiPage: Multiple pages pagination
 * - DarkTheme: Dark mode
 */

import type { Meta, StoryObj } from '@storybook/react';
import type { Game } from '@/lib/api';

// ============================================================================
// Mock Component (Simulates Page Structure for Storybook)
// ============================================================================

/**
 * Wrapper that simulates the full Board Game AI Games Page layout
 * Since the real page.tsx is a Server Component, we create a Story-compatible version
 */
function BoardGameAIGamesPageStory({
  games,
  view = 'grid',
  search = '',
  currentPage = 1,
  totalPages = 1,
  total = 0,
  loading = false,
  error = null,
}: {
  games: Game[];
  view?: 'grid' | 'list';
  search?: string;
  currentPage?: number;
  totalPages?: number;
  total?: number;
  loading?: boolean;
  error?: string | null;
}) {
  return (
    <div className="min-h-dvh bg-background">
      {/* Board Game AI Header */}
      <header className="sticky top-0 z-50 bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/60 border-b">
        <div className="container mx-auto px-4 py-4 flex items-center justify-between">
          <div className="flex items-center gap-4">
            <button className="inline-flex items-center gap-2 text-sm text-muted-foreground hover:text-foreground">
              ← <span className="hidden sm:inline">Board Game AI</span>
            </button>
            <div className="h-6 w-px bg-border hidden sm:block" />
            <div className="flex items-center gap-2 hover:opacity-80 transition-opacity">
              <span className="text-2xl">🎲</span>
              <span className="text-xl font-bold bg-gradient-to-r from-primary to-secondary bg-clip-text text-transparent hidden sm:inline">
                MeepleAI
              </span>
            </div>
          </div>

          <nav className="flex items-center gap-4">
            <button className="text-sm text-muted-foreground hover:text-foreground">
              Fai una domanda
            </button>
            <button className="text-sm bg-primary text-primary-foreground px-3 py-1.5 rounded-md">
              Accedi
            </button>
          </nav>
        </div>
      </header>

      {/* Main Content */}
      <main className="container mx-auto px-4 py-8">
        {/* Breadcrumb */}
        <nav aria-label="Breadcrumb" className="mb-6">
          <ol className="flex items-center gap-2 text-sm text-muted-foreground">
            <li>
              <span className="hover:text-foreground transition-colors cursor-pointer">Home</span>
            </li>
            <li>/</li>
            <li>
              <span className="hover:text-foreground transition-colors cursor-pointer">
                Board Game AI
              </span>
            </li>
            <li>/</li>
            <li className="text-foreground font-medium">Catalogo Giochi</li>
          </ol>
        </nav>

        {/* Page Header */}
        <div className="mb-8">
          <h1 className="font-heading text-3xl md:text-4xl font-bold mb-2">Catalogo Giochi</h1>
          <p className="text-muted-foreground text-lg">
            Esplora i giochi da tavolo disponibili. Seleziona un gioco per chiedere le regole
            all&apos;AI.
          </p>
        </div>

        {/* Toolbar */}
        <div className="space-y-6">
          <div className="flex flex-col sm:flex-row gap-4 items-start sm:items-center justify-between">
            {/* Search Bar */}
            <div className="relative w-full sm:w-96">
              <svg
                className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground"
                fill="none"
                stroke="currentColor"
                viewBox="0 0 24 24"
              >
                <circle cx="11" cy="11" r="8" />
                <path d="m21 21-4.34-4.34" />
              </svg>
              <input
                type="search"
                placeholder="Cerca giochi per nome..."
                defaultValue={search}
                className="w-full pl-10 pr-10 h-10 border rounded-md bg-background"
                aria-label="Cerca giochi"
              />
              {search && (
                <button
                  className="absolute right-1 top-1/2 -translate-y-1/2 h-7 w-7 p-0 flex items-center justify-center"
                  aria-label="Cancella ricerca"
                >
                  ×
                </button>
              )}
            </div>

            {/* View Toggle */}
            <div
              className="border rounded-md flex"
              role="group"
              aria-label="Modalità visualizzazione"
            >
              <button
                className={`px-3 py-2 flex items-center gap-2 ${view === 'grid' ? 'bg-accent text-accent-foreground' : ''}`}
                aria-label="Vista griglia"
              >
                <svg className="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <rect x="3" y="3" width="7" height="7" />
                  <rect x="14" y="3" width="7" height="7" />
                  <rect x="14" y="14" width="7" height="7" />
                  <rect x="3" y="14" width="7" height="7" />
                </svg>
                <span className="hidden sm:inline">Griglia</span>
              </button>
              <button
                className={`px-3 py-2 flex items-center gap-2 ${view === 'list' ? 'bg-accent text-accent-foreground' : ''}`}
                aria-label="Vista lista"
              >
                <svg className="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path d="M8 6h13M8 12h13M8 18h13M3 6h.01M3 12h.01M3 18h.01" />
                </svg>
                <span className="hidden sm:inline">Lista</span>
              </button>
            </div>
          </div>

          {/* Results Count */}
          {!loading && !error && (
            <p className="text-sm text-muted-foreground">
              {total === 0
                ? 'Nessun gioco trovato'
                : total === 1
                  ? '1 gioco trovato'
                  : `${total} giochi trovati`}
              {search && ` per "${search}"`}
            </p>
          )}

          {/* Error State */}
          {error && (
            <div className="flex flex-col items-center justify-center py-16 text-center">
              <div className="text-6xl mb-4">⚠️</div>
              <h3 className="text-lg font-semibold mb-2 text-destructive">{error}</h3>
              <button className="border px-4 py-2 rounded-md">Riprova</button>
            </div>
          )}

          {/* Loading State */}
          {loading && (
            <div
              className={
                view === 'grid'
                  ? 'grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-6'
                  : 'flex flex-col gap-4'
              }
            >
              {Array.from({ length: 20 }).map((_, i) => (
                <div
                  key={i}
                  className={`bg-muted animate-pulse rounded-lg ${view === 'grid' ? 'h-[300px]' : 'h-[120px]'}`}
                />
              ))}
            </div>
          )}

          {/* Empty State */}
          {!loading && !error && games.length === 0 && (
            <div className="flex flex-col items-center justify-center py-16 text-center">
              <div className="text-6xl mb-4">🎲</div>
              <h3 className="text-lg font-semibold mb-2">Nessun gioco trovato</h3>
              <p className="text-muted-foreground mb-4">Prova a modificare la ricerca</p>
              {search && <button className="border px-4 py-2 rounded-md">Cancella ricerca</button>}
            </div>
          )}

          {/* Games Grid/List */}
          {!loading && !error && games.length > 0 && (
            <div
              className={
                view === 'grid'
                  ? 'grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-6'
                  : 'flex flex-col gap-4'
              }
            >
              {games.map(game => (
                <div
                  key={game.id}
                  className={`relative group bg-card border rounded-lg overflow-hidden hover:shadow-lg transition-shadow cursor-pointer ${view === 'list' ? 'flex flex-row' : ''}`}
                >
                  {/* Image */}
                  <div
                    className={
                      view === 'grid' ? 'aspect-[3/4] bg-muted' : 'w-32 h-32 bg-muted flex-shrink-0'
                    }
                  >
                    {game.imageUrl && (
                      <img
                        src={game.imageUrl}
                        alt={game.title}
                        className="w-full h-full object-cover"
                      />
                    )}
                  </div>

                  {/* Content */}
                  <div className="p-4">
                    <h3 className="font-semibold text-lg mb-1">{game.title}</h3>
                    <p className="text-sm text-muted-foreground mb-2">
                      {game.publisher} • {game.yearPublished}
                    </p>
                    <div className="flex items-center gap-4 text-xs text-muted-foreground">
                      <span>
                        {game.minPlayers}-{game.maxPlayers} giocatori
                      </span>
                      <span>
                        {game.minPlayTimeMinutes}-{game.maxPlayTimeMinutes} min
                      </span>
                    </div>
                    {game.faqCount && game.faqCount > 0 && (
                      <div className="mt-2 text-xs text-primary">
                        {game.faqCount} FAQ disponibili
                      </div>
                    )}
                  </div>

                  {/* Hover overlay */}
                  <div className="absolute inset-0 bg-background/80 opacity-0 group-hover:opacity-100 transition-opacity flex items-center justify-center">
                    <button className="bg-primary text-primary-foreground px-4 py-2 rounded-md flex items-center gap-2">
                      <svg
                        className="h-4 w-4"
                        fill="none"
                        stroke="currentColor"
                        viewBox="0 0 24 24"
                      >
                        <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z" />
                      </svg>
                      Chiedi all&apos;AI
                    </button>
                  </div>
                </div>
              ))}
            </div>
          )}

          {/* Pagination */}
          {!loading && !error && totalPages > 1 && (
            <div className="flex flex-col items-center gap-4 mt-8">
              <p className="text-sm text-muted-foreground">
                Pagina {currentPage} di {totalPages} ({total} giochi)
              </p>
              <div className="flex items-center gap-2">
                <button
                  className="border px-3 py-1.5 rounded-md text-sm disabled:opacity-50"
                  disabled={currentPage === 1}
                >
                  ← Precedente
                </button>
                <button
                  className="border px-3 py-1.5 rounded-md text-sm disabled:opacity-50"
                  disabled={currentPage === totalPages}
                >
                  Successiva →
                </button>
              </div>
            </div>
          )}
        </div>
      </main>

      {/* Footer */}
      <footer className="border-t py-8 mt-16">
        <div className="container mx-auto px-4 text-center text-sm text-muted-foreground">
          <p>© 2025 MeepleAI. Il tuo assistente AI per giochi da tavolo.</p>
          <div className="flex justify-center gap-4 mt-4">
            <span className="hover:text-foreground transition-colors cursor-pointer">
              Board Game AI
            </span>
            <span className="hover:text-foreground transition-colors cursor-pointer">
              Carica PDF
            </span>
            <span className="hover:text-foreground transition-colors cursor-pointer">
              Impostazioni
            </span>
          </div>
        </div>
      </footer>
    </div>
  );
}

// ============================================================================
// Mock Data
// ============================================================================

const mockGames: Game[] = Array.from({ length: 20 }, (_, i) => ({
  id: `game-${i + 1}`,
  title:
    [
      'Catan',
      'Ticket to Ride',
      'Wingspan',
      'Azul',
      'Carcassonne',
      'Pandemic',
      '7 Wonders',
      'Splendor',
      'King of Tokyo',
      'Codenames',
      'Dixit',
      'Dominion',
      'Terraforming Mars',
      'Scythe',
      'Gloomhaven',
      'Brass: Birmingham',
      'Spirit Island',
      'Root',
      'Everdell',
      'Cascadia',
    ][i] || `Game ${i + 1}`,
  imageUrl: `https://picsum.photos/seed/bgai${i}/160/220`,
  publisher: ['CMON', 'Stonemaier', 'Days of Wonder', 'Next Move Games'][i % 4],
  minPlayers: [2, 2, 1, 2][i % 4],
  maxPlayers: [4, 5, 5, 4][i % 4],
  minPlayTimeMinutes: [30, 45, 60, 90][i % 4],
  maxPlayTimeMinutes: [60, 90, 90, 120][i % 4],
  yearPublished: 2015 + (i % 10),
  averageRating: 7.0 + (i % 3),
  bggId: 1000 + i,
  faqCount: i % 3 === 0 ? i + 5 : 0,
  createdAt: new Date(2024, 0, i + 1).toISOString(),
}));

// ============================================================================
// Story Meta
// ============================================================================

const meta: Meta<typeof BoardGameAIGamesPageStory> = {
  title: 'Pages/BoardGameAI/GameCatalog',
  component: BoardGameAIGamesPageStory,
  parameters: {
    layout: 'fullscreen',
    nextjs: {
      appDirectory: true,
      navigation: {
        pathname: '/board-game-ai/games',
        query: {},
      },
    },
  },
  tags: ['autodocs'],
};

export default meta;
type Story = StoryObj<typeof BoardGameAIGamesPageStory>;

// ============================================================================
// Stories
// ============================================================================

/**
 * Default Grid View
 * Standard catalog with games in grid layout
 */
export const DefaultGrid: Story = {
  args: {
    games: mockGames,
    view: 'grid',
    currentPage: 1,
    totalPages: 3,
    total: 45,
  },
};

/**
 * List View
 * Alternative layout showing horizontal game cards
 */
export const ListView: Story = {
  args: {
    games: mockGames,
    view: 'list',
    currentPage: 1,
    totalPages: 3,
    total: 45,
  },
};

/**
 * Mobile Flow (375px)
 * 2-column grid on mobile devices with Board Game AI header
 */
export const MobileFlow: Story = {
  args: {
    games: mockGames.slice(0, 8),
    view: 'grid',
    currentPage: 1,
    totalPages: 3,
    total: 45,
  },
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
    chromatic: {
      viewports: [375],
    },
  },
};

/**
 * Tablet Flow (768px)
 * 3-column grid on tablet devices
 */
export const TabletFlow: Story = {
  args: {
    games: mockGames.slice(0, 12),
    view: 'grid',
    currentPage: 1,
    totalPages: 4,
    total: 75,
  },
  parameters: {
    viewport: {
      defaultViewport: 'tablet',
    },
    chromatic: {
      viewports: [768],
    },
  },
};

/**
 * Desktop Flow (1440px)
 * 4-column grid on desktop
 */
export const DesktopFlow: Story = {
  args: {
    games: mockGames,
    view: 'grid',
    currentPage: 2,
    totalPages: 5,
    total: 100,
  },
  parameters: {
    viewport: {
      defaultViewport: 'desktop',
    },
    chromatic: {
      viewports: [1440],
    },
  },
};

/**
 * Responsive Matrix (375/768/1280/1440)
 * Single Chromatic snapshot across key breakpoints.
 */
export const ResponsiveMatrix: Story = {
  args: {
    games: mockGames.slice(0, 12),
    view: 'grid',
    currentPage: 1,
    totalPages: 3,
    total: 60,
  },
  parameters: {
    chromatic: {
      viewports: [375, 768, 1280, 1440],
      delay: 300,
    },
  },
};

/**
 * Empty State
 * No games found after search
 */
export const EmptyState: Story = {
  args: {
    games: [],
    view: 'grid',
    search: 'nonexistent',
    currentPage: 1,
    totalPages: 0,
    total: 0,
  },
};

/**
 * Loading State
 * Skeleton loaders while fetching
 */
export const LoadingState: Story = {
  args: {
    games: [],
    view: 'grid',
    currentPage: 1,
    totalPages: 0,
    total: 0,
    loading: true,
  },
};

/**
 * With Search Active
 * Search input with value and filtered results
 */
export const WithSearch: Story = {
  args: {
    games: mockGames.slice(0, 3),
    view: 'grid',
    search: 'Catan',
    currentPage: 1,
    totalPages: 1,
    total: 3,
  },
};

/**
 * Error State
 * API error display
 */
export const ErrorState: Story = {
  args: {
    games: [],
    view: 'grid',
    currentPage: 1,
    totalPages: 0,
    total: 0,
    error: 'Impossibile caricare i giochi. Riprova più tardi.',
  },
};

/**
 * Multi Page Pagination
 * Shows pagination controls with multiple pages
 */
export const MultiPagePagination: Story = {
  args: {
    games: mockGames,
    view: 'grid',
    currentPage: 2,
    totalPages: 5,
    total: 100,
  },
};

/**
 * Dark Theme
 * Dark mode variant
 */
export const DarkTheme: Story = {
  args: {
    games: mockGames.slice(0, 12),
    view: 'grid',
    currentPage: 1,
    totalPages: 3,
    total: 60,
  },
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    Story => (
      <div className="dark">
        <Story />
      </div>
    ),
  ],
};

/**
 * Board Game AI - Game Catalog Page (Issue #1017: BGAI-078)
 *
 * Game catalog integrated within the Board Game AI product area.
 * Reuses existing components from /games while maintaining
 * Board Game AI styling and navigation context.
 *
 * Route: /board-game-ai/games
 *
 * Features:
 * - Server-side rendering (SEO-friendly)
 * - URL state persistence (shareable links)
 * - Responsive grid (2→3→4 columns)
 * - Search with debounce (300ms)
 * - Pagination (20 games/page)
 * - Board Game AI header integration
 * - Playful Boardroom design system
 *
 * @see docs/04-frontend/wireframes-playful-boardroom.md
 * @see Issue #1017 BGAI-078
 */

import { Suspense } from 'react';

import { ArrowLeft } from 'lucide-react';
import { Metadata } from 'next';
import Link from 'next/link';

import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';

import { GameCatalogClient } from './GameCatalogClient';

// ============================================================================
// Metadata
// ============================================================================

export const metadata: Metadata = {
  title: 'Catalogo Giochi - Board Game AI | MeepleAI',
  description:
    'Esplora il nostro catalogo di giochi da tavolo. Cerca, filtra e ottieni risposte AI alle regole dei tuoi giochi preferiti.',
  keywords: ['giochi da tavolo', 'board games', 'catalogo', 'regole', 'AI', 'MeepleAI'],
  openGraph: {
    title: 'Catalogo Giochi - Board Game AI',
    description: 'Esplora il nostro catalogo di giochi da tavolo e ottieni risposte AI alle regole',
    type: 'website',
  },
};

// ============================================================================
// Loading Component
// ============================================================================

function GameCatalogSkeleton() {
  return (
    <div className="space-y-6">
      {/* Header skeleton */}
      <div className="space-y-2">
        <Skeleton className="h-10 w-64" />
        <Skeleton className="h-5 w-96" />
      </div>

      {/* Toolbar skeleton */}
      <div className="flex flex-col sm:flex-row gap-4 items-start sm:items-center justify-between">
        <Skeleton className="h-10 w-full sm:w-96" />
        <Skeleton className="h-10 w-32" />
      </div>

      {/* Grid skeleton */}
      <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-6">
        {Array.from({ length: 12 }).map((_, i) => (
          <Skeleton key={i} className="h-[300px] rounded-lg" />
        ))}
      </div>
    </div>
  );
}

// ============================================================================
// Page Component
// ============================================================================

interface SearchParams {
  view?: 'grid' | 'list';
  search?: string;
  page?: string;
}

export default async function BoardGameAIGamesPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;

  return (
    <div className="min-h-dvh bg-background">
      {/* Board Game AI Header */}
      <header className="sticky top-0 z-50 bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/60 border-b">
        <div className="container mx-auto px-4 py-4 flex items-center justify-between">
          <div className="flex items-center gap-4">
            <Button variant="ghost" size="sm" asChild className="gap-2">
              <Link href="/board-game-ai">
                <ArrowLeft className="h-4 w-4" />
                <span className="hidden sm:inline">Board Game AI</span>
              </Link>
            </Button>
            <div className="h-6 w-px bg-border hidden sm:block" />
            <Link href="/" className="flex items-center gap-2 hover:opacity-80 transition-opacity">
              <span className="text-2xl">🎲</span>
              <span className="text-xl font-bold bg-gradient-to-r from-primary to-secondary bg-clip-text text-transparent hidden sm:inline">
                MeepleAI
              </span>
            </Link>
          </div>

          <nav className="flex items-center gap-4">
            <Button variant="ghost" size="sm" asChild>
              <Link href="/board-game-ai/ask">Fai una domanda</Link>
            </Button>
            <Button variant="default" size="sm" asChild>
              <Link href="/login">Accedi</Link>
            </Button>
          </nav>
        </div>
      </header>

      {/* Main Content */}
      <main className="container mx-auto px-4 py-8">
        {/* Breadcrumb */}
        <nav aria-label="Breadcrumb" className="mb-6">
          <ol className="flex items-center gap-2 text-sm text-muted-foreground">
            <li>
              <Link href="/" className="hover:text-foreground transition-colors">
                Home
              </Link>
            </li>
            <li>/</li>
            <li>
              <Link href="/board-game-ai" className="hover:text-foreground transition-colors">
                Board Game AI
              </Link>
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

        {/* Client Component with Suspense */}
        <Suspense fallback={<GameCatalogSkeleton />}>
          <GameCatalogClient
            initialView={params.view || 'grid'}
            initialSearch={params.search || ''}
            initialPage={parseInt(params.page || '1', 10)}
          />
        </Suspense>
      </main>

      {/* Footer */}
      <footer className="border-t py-8 mt-16">
        <div className="container mx-auto px-4 text-center text-sm text-muted-foreground">
          <p>© 2025 MeepleAI. Il tuo assistente AI per giochi da tavolo.</p>
          <div className="flex justify-center gap-4 mt-4">
            <Link href="/board-game-ai" className="hover:text-foreground transition-colors">
              Board Game AI
            </Link>
            <Link href="/upload" className="hover:text-foreground transition-colors">
              Carica PDF
            </Link>
            <Link href="/settings" className="hover:text-foreground transition-colors">
              Impostazioni
            </Link>
          </div>
        </div>
      </footer>
    </div>
  );
}

/**
 * Discover Game Detail Page
 * Issue #5039 — Consolidate User Routes
 *
 * Shows public game detail for a community catalog entry.
 * Receives traffic redirected from /games/:id.
 *
 * TODO (Issue #5041): Migrate full content from /games/[id] sub-pages.
 */

import { ArrowLeft } from 'lucide-react';
import Link from 'next/link';

interface DiscoverGamePageProps {
  params: Promise<{ gameId: string }>;
  searchParams: Promise<{ tab?: string }>;
}

export default async function DiscoverGamePage({ params }: DiscoverGamePageProps) {
  const { gameId } = await params;

  return (
    <div className="min-h-screen bg-background">
      <div className="mx-auto max-w-6xl px-4 py-8 sm:px-6">
        <Link
          href="/discover"
          className="inline-flex items-center gap-2 text-sm font-medium text-muted-foreground hover:text-foreground"
        >
          <ArrowLeft className="h-4 w-4" />
          Torna a Scopri
        </Link>

        <div className="mt-6">
          <h1 className="text-2xl font-bold tracking-tight text-foreground">
            Dettaglio Gioco
          </h1>
          <p className="mt-2 text-muted-foreground">Gioco ID: {gameId}</p>
          {/* TODO Issue #5041: Render full game detail from community catalog */}
        </div>
      </div>
    </div>
  );
}

import { ArrowLeft } from 'lucide-react';
import Link from 'next/link';

import { BggSearchPanel } from '@/components/games/BggSearchPanel';

export const dynamic = 'force-dynamic';

export const metadata = {
  title: 'BGG Search - Add Games | MeepleAI',
  description: 'Search BoardGameGeek and add games to your library for Game Night.',
};

export default function BggSearchPage() {
  return (
    <div className="container mx-auto max-w-6xl px-4 py-8">
      {/* Breadcrumb */}
      <nav className="mb-6 flex items-center gap-2 text-sm text-muted-foreground font-nunito">
        <Link href="/games" className="hover:text-foreground transition-colors">
          Games
        </Link>
        <span>/</span>
        <span className="text-foreground">BGG Search</span>
      </nav>

      {/* Header */}
      <div className="mb-8">
        <div className="flex items-center gap-3">
          <Link
            href="/games"
            className="rounded-md p-1.5 hover:bg-muted transition-colors"
            aria-label="Back to games"
          >
            <ArrowLeft className="h-5 w-5" />
          </Link>
          <div>
            <h1 className="font-quicksand text-2xl font-bold tracking-tight">
              Search BoardGameGeek
            </h1>
            <p className="mt-1 text-sm text-muted-foreground font-nunito">
              Find games on BGG and add them to your library for tonight&apos;s game night.
            </p>
          </div>
        </div>
      </div>

      {/* Search Panel */}
      <div className="rounded-xl bg-white/70 p-6 backdrop-blur-md dark:bg-zinc-900/70">
        <BggSearchPanel />
      </div>
    </div>
  );
}

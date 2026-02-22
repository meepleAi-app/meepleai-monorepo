/**
 * Discover Page — Community Game Catalog
 * Issue #5039 — Consolidate User Routes
 *
 * Consolidates community-facing game browsing into a single hub:
 * - Tab: Catalog (/discover → Sfoglia il catalogo)
 * - Tab: Proposals (/discover?tab=proposals → Le mie proposte)
 *
 * TODO (Issue #5041): Migrate full content from /games and /library/proposals.
 */

import { DiscoverNavConfig } from './NavConfig';

interface DiscoverPageProps {
  searchParams: Promise<{ tab?: string }>;
}

export default async function DiscoverPage({ searchParams }: DiscoverPageProps) {
  const params = await searchParams;
  const tab = params.tab ?? 'catalog';

  // Proposals tab — stub until Issue #5041 migrates full content here
  if (tab === 'proposals') {
    return (
      <div className="min-h-screen bg-background">
        <DiscoverNavConfig />
        <div className="mx-auto max-w-7xl px-4 py-8 sm:px-6 lg:px-8">
          <h1 className="text-2xl font-bold tracking-tight text-foreground">Le mie Proposte</h1>
          <p className="mt-2 text-muted-foreground">
            Le proposte sono in fase di migrazione. Disponibile con Issue #5041.
          </p>
          {/* TODO Issue #5041: Migrate MyProposalsClient content here */}
        </div>
      </div>
    );
  }

  // Default: catalog view — stub until Issue #5041
  return (
    <div className="min-h-screen bg-background">
      <DiscoverNavConfig />
      <div className="mx-auto max-w-7xl px-4 py-8 sm:px-6 lg:px-8">
        <h1 className="text-2xl font-bold tracking-tight text-foreground">Scopri</h1>
        <p className="mt-2 text-muted-foreground">
          Sfoglia il catalogo comunitario dei giochi da tavolo.
        </p>
        {/* TODO Issue #5041: Render full game catalog with tabs */}
      </div>
    </div>
  );
}

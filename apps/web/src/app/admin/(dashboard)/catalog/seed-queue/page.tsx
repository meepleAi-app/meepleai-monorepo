'use client';

import { BulkPasteForm } from './components/BulkPasteForm';
import { SeedLogStream } from './components/SeedLogStream';
import { SeedQueueList } from './components/SeedQueueList';
import { SeedQueueStatusHero } from './components/SeedQueueStatusHero';
import { SingleAddForm } from './components/SingleAddForm';
import { WikidataSearchForm } from './components/WikidataSearchForm';

/**
 * Issue #1903 M8.2 — admin catalog seed queue page.
 *
 * Two-column layout (input sx · queue dx) per design spec §5.1. Live SSE log at
 * the bottom. All sub-components own their data via React Query so the page
 * itself is a thin composition.
 *
 * Auth: route lives under the `admin/(dashboard)` route group which is gated by
 * a server-side admin check; the BE re-enforces auth at the
 * `RequireAdminSessionFilter` level on every endpoint.
 *
 * Feature flag: when `admin.catalog-seed.enabled=false`, every BE call returns
 * 503 and surfaces in the per-component error UI.
 */
export default function CatalogSeedQueuePage() {
  return (
    <div className="flex flex-col gap-4">
      <header className="flex items-center gap-4 border-b border-border bg-background/80 px-6 py-3 backdrop-blur-md">
        <div>
          <h1 className="font-quicksand text-[20px] font-extrabold leading-tight tracking-tight text-foreground">
            Catalog seed queue
          </h1>
          <p className="mt-0.5 font-mono text-[11px] text-muted-foreground">
            Admin · Catalog · Seed pipeline (Wikidata + BGG)
          </p>
        </div>
      </header>

      <div className="flex flex-col gap-4 px-6">
        <SeedQueueStatusHero />

        <div className="grid grid-cols-1 gap-4 lg:grid-cols-[minmax(0,360px)_1fr]">
          {/* Input column */}
          <div className="flex flex-col gap-4">
            <BulkPasteForm />
            <SingleAddForm />
            <WikidataSearchForm />
          </div>

          {/* Queue column */}
          <div className="flex flex-col gap-4">
            <SeedQueueList />
          </div>
        </div>

        <SeedLogStream />
      </div>
    </div>
  );
}

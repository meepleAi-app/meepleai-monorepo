/**
 * CollectionDashboardClient - Issue #3476, #3632
 * EPIC #3475: User Private Library & Collections Management
 *
 * Client component for collection dashboard with enhanced features:
 * - Hero stats with key metrics
 * - Advanced filtering and search
 * - Grid/List view toggle
 * - Quick actions (add game, create collection)
 * - Responsive "Warm Tabletop" aesthetic
 */

'use client';

import { CollectionDashboard } from '@/components/collection';

export function CollectionDashboardClient() {
  return (
    <div className="container mx-auto px-4 py-6 max-w-screen-2xl">
      {/* Header */}
      <div className="mb-6">
        <h1 className="text-3xl font-bold tracking-tight font-heading">
          La Mia Collezione
        </h1>
        <p className="text-muted-foreground mt-1">
          Gestisci i tuoi giochi, traccia partite e organizza la collezione
        </p>
      </div>

      {/* Collection Dashboard with all features */}
      <CollectionDashboard />
    </div>
  );
}

export default CollectionDashboardClient;

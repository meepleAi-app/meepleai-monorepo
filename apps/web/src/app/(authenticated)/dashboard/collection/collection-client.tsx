/**
 * CollectionDashboardClient - Issue #3476
 *
 * Client component for collection dashboard with state management
 */

'use client';

import { useState } from 'react';

import { Plus } from 'lucide-react';

import { CollectionGrid } from '@/components/collection/CollectionGrid';
import { CollectionStats } from '@/components/collection/CollectionStats';
import { ActivityFeed } from '@/components/dashboard/ActivityFeed';
import { Button } from '@/components/ui/primitives/button';
import { useCollectionStats } from '@/hooks/useCollectionStats';
import type { SortOption, ActiveFilters } from '@/types/collection';

export function CollectionDashboardClient() {
  const [sortBy, setSortBy] = useState<SortOption>('date-added-desc');
  const [filters, setFilters] = useState<ActiveFilters>({});

  const { stats, games, isLoading } = useCollectionStats({
    sortBy,
    filters,
  });

  const handleGameClick = (_gameId: string) => {
    // TODO: Navigate to game details or mark as played
  };

  const handleAddGame = () => {
    // TODO: Open game wizard or navigate to add game page
  };

  return (
    <div className="container mx-auto px-4 py-6 space-y-8 max-w-screen-2xl">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">La Mia Collezione</h1>
          <p className="text-muted-foreground mt-1">
            Gestisci i tuoi giochi, traccia partite e organizza la collezione
          </p>
        </div>
        <Button
          onClick={handleAddGame}
          className="hidden sm:flex"
          data-testid="add-game-button"
        >
          <Plus className="h-4 w-4 mr-2" />
          Aggiungi Gioco
        </Button>
      </div>

      {/* Hero Stats */}
      <CollectionStats stats={stats ?? undefined} isLoading={isLoading} />

      {/* Main Content Grid */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
        {/* Collection Grid - Takes 2 columns on large screens */}
        <div className="lg:col-span-2">
          <CollectionGrid
            games={games}
            sortBy={sortBy}
            filters={filters}
            onSortChange={setSortBy}
            onFilterChange={setFilters}
            onGameClick={handleGameClick}
            isLoading={isLoading}
          />
        </div>

        {/* Sidebar - Activity Feed */}
        <div className="lg:col-span-1">
          <ActivityFeed
            events={stats?.recentActivity || []}
            isLoading={isLoading}
          />
        </div>
      </div>

      {/* Mobile Add Button */}
      <div className="sm:hidden fixed bottom-6 right-6 z-50">
        <Button
          onClick={handleAddGame}
          size="lg"
          className="rounded-full shadow-lg h-14 w-14 p-0"
          data-testid="add-game-button-mobile"
        >
          <Plus className="h-6 w-6" />
        </Button>
      </div>
    </div>
  );
}

export default CollectionDashboardClient;

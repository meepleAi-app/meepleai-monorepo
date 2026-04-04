'use client';

import { useState } from 'react';

import { GameCatalogGrid } from '@/components/admin/shared-games/game-catalog-grid';
import { GameFilters } from '@/components/admin/shared-games/game-filters';
import { RecentlyProcessedWidget } from '@/components/admin/shared-games/RecentlyProcessedWidget';

export function AllGamesClient() {
  const [search, setSearch] = useState('');
  const [category, setCategory] = useState('all');
  const [status, setStatus] = useState('all');
  const [players, setPlayers] = useState('all');

  return (
    <>
      {/* Recently Processed PDFs */}
      <RecentlyProcessedWidget />

      {/* Filters */}
      <GameFilters
        onSearchChange={setSearch}
        onCategoryChange={setCategory}
        onStatusChange={setStatus}
        onPlayersChange={setPlayers}
      />

      {/* Game Grid */}
      <GameCatalogGrid
        searchQuery={search}
        categoryFilter={category}
        statusFilter={status}
        playersFilter={players}
      />
    </>
  );
}

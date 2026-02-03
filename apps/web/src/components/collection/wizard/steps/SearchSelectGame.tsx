'use client';

/**
 * Step 1: Search/Select Game
 * Issue #3477: Search SharedGameCatalog or create custom game
 */

import { useState } from 'react';

import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { useAddGameWizard } from '@/hooks/useAddGameWizard';
import type { Game } from '@/types/domain';

// Mock game data (TODO: Replace with real API call when backend ready)
const MOCK_GAMES: Game[] = [
  { id: '1', title: 'Catan', createdAt: '2024-01-01' },
  { id: '2', title: 'Ticket to Ride', createdAt: '2024-01-02' },
  { id: '3', title: 'Carcassonne', createdAt: '2024-01-03' },
  { id: '4', title: 'Pandemic', createdAt: '2024-01-04' },
  { id: '5', title: 'Azul', createdAt: '2024-01-05' },
];

export function SearchSelectGame() {
  const { selectSharedGame, selectCustomGame, goNext } = useAddGameWizard();
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedGameId, setSelectedGameId] = useState<string | null>(null);

  // Filter games based on search query
  const filteredGames = searchQuery.trim()
    ? MOCK_GAMES.filter(game => game.title.toLowerCase().includes(searchQuery.toLowerCase()))
    : MOCK_GAMES;

  const handleSelectGame = (game: Game) => {
    setSelectedGameId(game.id);
    selectSharedGame(game);
  };

  const handleCreateCustom = () => {
    selectCustomGame();
    goNext(); // Immediately go to Step 2 (Game Details)
  };

  const handleNext = () => {
    goNext(); // Skip to Step 3 (Upload PDF)
  };

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-semibold text-slate-900 dark:text-white mb-2">
          Search or Create Game
        </h2>
        <p className="text-slate-600 dark:text-slate-400">
          Search the shared game catalog or create a custom game entry
        </p>
      </div>

      {/* Search Input */}
      <div className="space-y-2">
        <Input
          type="text"
          placeholder="Search by game title..."
          value={searchQuery}
          onChange={e => setSearchQuery(e.target.value)}
          className="w-full"
        />
        <p className="text-sm text-slate-500">
          {filteredGames.length} game{filteredGames.length !== 1 ? 's' : ''} found
        </p>
      </div>

      {/* Game Results Grid */}
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 max-h-96 overflow-y-auto p-1">
        {filteredGames.map(game => (
          <button
            key={game.id}
            onClick={() => handleSelectGame(game)}
            className={`p-4 border-2 rounded-lg text-left transition-colors hover:border-blue-400 ${
              selectedGameId === game.id
                ? 'border-blue-600 bg-blue-50 dark:bg-blue-900/20'
                : 'border-slate-200 dark:border-slate-700 hover:bg-slate-50 dark:hover:bg-slate-800'
            }`}
          >
            <h3 className="font-semibold text-slate-900 dark:text-white">{game.title}</h3>
            <p className="text-sm text-slate-500 mt-1">Shared Catalog</p>
            {selectedGameId === game.id && (
              <span className="inline-block mt-2 text-xs bg-blue-600 text-white px-2 py-1 rounded">
                Selected ✓
              </span>
            )}
          </button>
        ))}
      </div>

      {/* Divider */}
      <div className="flex items-center gap-4">
        <div className="flex-1 h-px bg-slate-200 dark:bg-slate-700" />
        <span className="text-sm text-slate-500">OR</span>
        <div className="flex-1 h-px bg-slate-200 dark:bg-slate-700" />
      </div>

      {/* Create Custom Game */}
      <div className="p-4 border-2 border-dashed border-slate-300 dark:border-slate-600 rounded-lg text-center">
        <p className="text-slate-600 dark:text-slate-400 mb-3">
          Can't find your game? Create a custom entry.
        </p>
        <Button variant="outline" onClick={handleCreateCustom}>
          + Create Custom Game
        </Button>
      </div>

      {/* Next Button (only if shared game selected) */}
      {selectedGameId && (
        <div className="flex justify-end">
          <Button onClick={handleNext}>Next →</Button>
        </div>
      )}
    </div>
  );
}

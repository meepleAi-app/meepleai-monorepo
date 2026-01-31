/**
 * Game Selector - Select game from library
 * Issue #3239 (FRONT-003)
 *
 * Features:
 * - Fetch games from user library with PDF
 * - shadcn Select component
 * - Update Zustand store on selection
 */

'use client';

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { useAgentStore } from '@/stores/agentStore';
import { BookOpen } from 'lucide-react';

interface Game {
  id: string;
  title: string;
  hasPdf: boolean;
}

export function GameSelector() {
  const { selectedGameId, setSelectedGame } = useAgentStore();

  // Mock data - replace with React Query in production
  const games: Game[] = [
    { id: '1', title: '7 Wonders', hasPdf: true },
    { id: '2', title: 'Splendor', hasPdf: true },
    { id: '3', title: 'Catan', hasPdf: true },
  ];

  return (
    <div className="space-y-3">
      <label className="block text-sm font-medium text-slate-200">
        Select Game
        <span className="ml-1 text-red-400">*</span>
      </label>

      <Select value={selectedGameId || ''} onValueChange={setSelectedGame}>
        <SelectTrigger className="w-full bg-slate-900 border-slate-700">
          <SelectValue placeholder="Choose a game..." />
        </SelectTrigger>
        <SelectContent>
          {games.map(game => (
            <SelectItem key={game.id} value={game.id}>
              <div className="flex items-center gap-2">
                <BookOpen className="h-4 w-4 text-cyan-400" />
                <span>{game.title}</span>
                {game.hasPdf && (
                  <span className="text-xs text-green-400">📚 Rulebook</span>
                )}
              </div>
            </SelectItem>
          ))}
        </SelectContent>
      </Select>

      {!selectedGameId && (
        <p className="text-xs text-red-400">Please select a game to continue</p>
      )}
    </div>
  );
}

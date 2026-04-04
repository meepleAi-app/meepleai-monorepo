'use client';

import React, { useState } from 'react';

import { ArrowUpDown, Plus, X } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';

interface Player {
  id: string;
  name: string;
  score: number;
}

type SortDirection = 'desc' | 'asc';

/**
 * Scoreboard — multi-player scoreboard for board game sessions.
 */
export function Scoreboard() {
  const [players, setPlayers] = useState<Player[]>([]);
  const [newName, setNewName] = useState('');
  const [sortDir, setSortDir] = useState<SortDirection>('desc');

  const addPlayer = () => {
    const name = newName.trim();
    if (!name) return;
    setPlayers(prev => [...prev, { id: crypto.randomUUID(), name, score: 0 }]);
    setNewName('');
  };

  const adjustScore = (id: string, delta: number) => {
    setPlayers(prev => prev.map(p => (p.id === id ? { ...p, score: p.score + delta } : p)));
  };

  const removePlayer = (id: string) => {
    setPlayers(prev => prev.filter(p => p.id !== id));
  };

  const toggleSort = () => {
    setSortDir(prev => (prev === 'desc' ? 'asc' : 'desc'));
  };

  const sorted = [...players].sort((a, b) =>
    sortDir === 'desc' ? b.score - a.score : a.score - b.score
  );

  return (
    <div className="space-y-3" data-testid="scoreboard">
      {/* Add player */}
      <div className="flex gap-2">
        <Input
          value={newName}
          onChange={e => setNewName(e.target.value)}
          onKeyDown={e => e.key === 'Enter' && addPlayer()}
          placeholder="Nome giocatore…"
          className="h-8 text-sm"
          aria-label="New player name"
        />
        <Button
          variant="outline"
          size="sm"
          onClick={addPlayer}
          disabled={!newName.trim()}
          aria-label="Add player"
          className="gap-1"
        >
          <Plus className="h-3.5 w-3.5" />
          Aggiungi
        </Button>
      </div>

      {/* Table */}
      {players.length === 0 ? (
        <p className="py-4 text-center text-sm text-slate-400">Nessun giocatore. Aggiungine uno!</p>
      ) : (
        <div className="rounded-lg border border-slate-200 overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-slate-50">
              <tr>
                <th className="px-3 py-2 text-left font-medium text-slate-600">#</th>
                <th className="px-3 py-2 text-left font-medium text-slate-600">Giocatore</th>
                <th className="px-3 py-2 text-right font-medium text-slate-600">
                  <button
                    onClick={toggleSort}
                    className="inline-flex items-center gap-1 hover:text-amber-600 transition-colors"
                    aria-label={`Sort by score ${sortDir === 'desc' ? 'ascending' : 'descending'}`}
                  >
                    Punti
                    <ArrowUpDown className="h-3 w-3" />
                  </button>
                </th>
                <th className="px-3 py-2 text-right font-medium text-slate-600">Azioni</th>
                <th className="w-8" />
              </tr>
            </thead>
            <tbody>
              {sorted.map((player, idx) => (
                <tr
                  key={player.id}
                  className="border-t border-slate-100 hover:bg-slate-50 transition-colors"
                  data-testid={`player-row-${player.id}`}
                >
                  <td className="px-3 py-2 text-slate-400 tabular-nums">{idx + 1}</td>
                  <td className="px-3 py-2 font-medium text-slate-800">{player.name}</td>
                  <td className="px-3 py-2 text-right font-bold tabular-nums text-slate-800">
                    {player.score}
                  </td>
                  <td className="px-3 py-2">
                    <div className="flex items-center justify-end gap-1">
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => adjustScore(player.id, -1)}
                        aria-label={`Decrease score for ${player.name}`}
                        className="h-6 w-6 p-0 text-xs"
                      >
                        −
                      </Button>
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => adjustScore(player.id, 1)}
                        aria-label={`Increase score for ${player.name}`}
                        className="h-6 w-6 p-0 text-xs"
                      >
                        +
                      </Button>
                    </div>
                  </td>
                  <td className="px-1 py-2">
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => removePlayer(player.id)}
                      aria-label={`Remove ${player.name}`}
                      className="h-6 w-6 p-0 text-slate-400 hover:text-red-500"
                    >
                      <X className="h-3 w-3" />
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

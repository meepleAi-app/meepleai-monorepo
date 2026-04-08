'use client';

/**
 * ScoreboardTab — Multi-dimension scoreboard with inline edit, stepper,
 * row reorder, ranking, and round breakdown.
 */

import React, { useMemo, useState } from 'react';

import { cn } from '@/lib/utils';

import { useToolkitDrawer } from '../ToolkitDrawerProvider';
import { RankingBar, type RankingEntry } from './RankingBar';
import { RoundBreakdown } from './RoundBreakdown';
import { ScoreCategoryHeader } from './ScoreCategoryHeader';
import { ScoreCell } from './ScoreCell';

// ============================================================================
// Add category inline form
// ============================================================================

function AddCategoryButton({ onAdd }: { onAdd: (name: string) => void }) {
  const [open, setOpen] = useState(false);
  const [name, setName] = useState('');

  const submit = () => {
    const trimmed = name.trim();
    if (trimmed) {
      onAdd(trimmed);
      setName('');
      setOpen(false);
    }
  };

  if (!open) {
    return (
      <button
        type="button"
        onClick={() => setOpen(true)}
        className="rounded-lg border border-dashed border-gray-300 px-2 py-1 text-[10px] font-semibold text-gray-400 hover:border-[hsl(142,70%,45%)] hover:text-[hsl(142,70%,45%)]"
        data-testid="add-category-btn"
      >
        + Aggiungi
      </button>
    );
  }

  return (
    <div className="flex items-center gap-1">
      <input
        type="text"
        value={name}
        onChange={e => setName(e.target.value)}
        autoFocus
        onKeyDown={e => {
          if (e.key === 'Enter') submit();
          if (e.key === 'Escape') setOpen(false);
        }}
        placeholder="Nome"
        className="w-20 rounded border border-[hsl(142,70%,45%)] px-1 text-[10px] outline-none"
        data-testid="add-category-input"
      />
      <button
        type="button"
        onClick={submit}
        className="text-[10px] text-[hsl(142,70%,45%)]"
        data-testid="add-category-confirm"
      >
        ✓
      </button>
    </div>
  );
}

// ============================================================================
// ScoreboardTab
// ============================================================================

export function ScoreboardTab() {
  const { store, logEvent } = useToolkitDrawer();
  const players = store(s => s.players);
  const scores = store(s => s.scores);
  const scoreCategories = store(s => s.scoreCategories);
  const currentTurnIndex = store(s => s.currentTurnIndex);
  const currentRound = store(s => s.currentRound);

  const [dragIndex, setDragIndex] = useState<number | null>(null);

  const handleScoreChange = (
    playerId: string,
    playerName: string,
    category: string,
    newValue: number
  ) => {
    const prev = scores[playerId]?.[category] ?? 0;
    const delta = newValue - prev;
    if (delta === 0) return;

    store.getState().setScore(playerId, category, newValue);
    logEvent(
      'score_change',
      { category, delta, newTotal: newValue },
      { playerId, playerName, round: currentRound }
    );
  };

  const handleAddCategory = (name: string) => {
    store.getState().addScoreCategory(name);
  };

  const handleRemoveCategory = (category: string) => {
    store.getState().removeScoreCategory(category);
  };

  const handleReset = () => {
    if (confirm('Azzerare tutti i punteggi? I valori attuali saranno persi.')) {
      store.getState().resetScores();
      logEvent('score_reset', {});
    }
  };

  const handleAdvanceRound = () => {
    store.getState().advanceRound();
    logEvent('round_advance', { round: currentRound + 1 });
  };

  const handleSetTurn = (index: number, playerName: string) => {
    const prevPlayer = players[currentTurnIndex];
    const nextPlayer = players[index];
    if (!nextPlayer || prevPlayer?.id === nextPlayer.id) return;

    store.getState().setTurn(index);
    logEvent(
      'turn_change',
      { nextPlayerName: playerName },
      { playerId: nextPlayer.id, playerName: nextPlayer.name, round: currentRound }
    );
  };

  // Row reorder via drag
  const handleDragStart = (index: number) => setDragIndex(index);
  const handleDragOver = (e: React.DragEvent) => e.preventDefault();
  const handleDrop = (targetIndex: number) => {
    if (dragIndex === null || dragIndex === targetIndex) {
      setDragIndex(null);
      return;
    }
    const reordered = [...players];
    const [moved] = reordered.splice(dragIndex, 1);
    reordered.splice(targetIndex, 0, moved);
    store.getState().reorderPlayers(reordered.map(p => p.id));
    setDragIndex(null);
  };

  // Compute totals and ranking
  const rankingEntries: RankingEntry[] = useMemo(
    () =>
      players.map(p => ({
        id: p.id,
        name: p.name,
        color: p.color,
        total: Object.values(scores[p.id] ?? {}).reduce((a, b) => a + b, 0),
      })),
    [players, scores]
  );

  if (players.length === 0) {
    return (
      <div className="py-8 text-center" data-testid="scoreboard-tab">
        <p className="text-sm italic text-gray-400">
          Aggiungi giocatori dalla barra in basso per iniziare
        </p>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-3" data-testid="scoreboard-tab">
      {/* Categories row */}
      <div className="flex items-center justify-between">
        <h3 className="text-xs font-semibold uppercase tracking-wider text-gray-400">Categorie</h3>
        <AddCategoryButton onAdd={handleAddCategory} />
      </div>

      {/* Score table */}
      <div className="overflow-x-auto">
        <table className="w-full border-collapse">
          <thead>
            <tr>
              <th className="border-b border-gray-200 px-1 pb-1 text-left text-[10px] font-bold uppercase tracking-wider text-gray-600">
                Giocatore
              </th>
              {scoreCategories.map(cat => (
                <ScoreCategoryHeader
                  key={cat}
                  category={cat}
                  onRemove={() => handleRemoveCategory(cat)}
                />
              ))}
              <th className="border-b border-gray-200 px-1 pb-1 text-center text-[10px] font-bold uppercase tracking-wider text-[hsl(142,70%,45%)]">
                Totale
              </th>
            </tr>
          </thead>
          <tbody>
            {players.map((player, idx) => {
              const isTurn = idx === currentTurnIndex;
              const total = Object.values(scores[player.id] ?? {}).reduce((a, b) => a + b, 0);
              return (
                <tr
                  key={player.id}
                  draggable
                  onDragStart={() => handleDragStart(idx)}
                  onDragOver={handleDragOver}
                  onDrop={() => handleDrop(idx)}
                  className={cn('border-b border-gray-100', dragIndex === idx && 'opacity-50')}
                  data-testid={`score-row-${player.id}`}
                >
                  <td className="py-1.5 pr-1">
                    <div className="flex items-center gap-1.5">
                      <span className="cursor-grab text-[10px] text-gray-300" title="Trascina">
                        ⠿
                      </span>
                      <button
                        type="button"
                        onClick={() => handleSetTurn(idx, player.name)}
                        className={cn(
                          'flex items-center gap-1.5 rounded px-1 py-0.5 text-xs font-medium',
                          isTurn && 'bg-[hsl(142,70%,45%)]/10'
                        )}
                        data-testid={`set-turn-${player.id}`}
                      >
                        {isTurn && <span className="text-[hsl(142,70%,45%)]">●</span>}
                        <span
                          className="h-3 w-3 rounded-full"
                          style={{ backgroundColor: player.color }}
                        />
                        <span className="max-w-[72px] truncate text-gray-800">{player.name}</span>
                      </button>
                    </div>
                  </td>
                  {scoreCategories.map(cat => (
                    <td key={cat} className="py-1.5 text-center">
                      <ScoreCell
                        value={scores[player.id]?.[cat] ?? 0}
                        onChange={val => handleScoreChange(player.id, player.name, cat, val)}
                        testId={`score-${player.id}-${cat}`}
                      />
                    </td>
                  ))}
                  <td className="py-1.5 text-center text-sm font-bold text-[hsl(142,70%,45%)]">
                    {total}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      {/* Ranking */}
      <RankingBar entries={rankingEntries} />

      {/* Round breakdown */}
      <RoundBreakdown currentRound={currentRound} onAdvanceRound={handleAdvanceRound} />

      {/* Actions */}
      <div className="flex justify-end gap-2">
        <button
          type="button"
          onClick={handleReset}
          className="rounded-lg border border-gray-300 px-3 py-1.5 text-xs font-medium text-gray-600 hover:border-red-400 hover:text-red-500"
          data-testid="reset-scores-btn"
        >
          🔄 Reset
        </button>
      </div>
    </div>
  );
}

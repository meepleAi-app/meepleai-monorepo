'use client';

/**
 * DiceRollerTab — Main dice tab composing preset rows, pool builder,
 * and result display. Logs every roll to the session diary with player
 * attribution. Provides CTA buttons to re-roll or pass to the next
 * player, and an inline form for manually recording a roll result.
 */

import React, { useCallback, useState } from 'react';

import { cn } from '@/lib/utils';

import { useDiceRoller } from '../hooks/useDiceRoller';
import { useToolkitDrawer } from '../ToolkitDrawerProvider';
import { UNIVERSAL_DICE_PRESETS } from '../types';
import { DicePoolBuilder } from './DicePoolBuilder';
import { DicePresetRow } from './DicePresetRow';
import { DiceResultDisplay } from './DiceResultDisplay';

import type { DicePreset, DiceResult, LocalPlayer } from '../types';

// ============================================================================
// Quick Log Form — inline manual roll entry
// ============================================================================

interface DiceQuickLogFormProps {
  players: LocalPlayer[];
  defaultPlayerId?: string;
  onSave: (playerId: string | null, formula: string, total: number) => void;
  onCancel: () => void;
}

function DiceQuickLogForm({ players, defaultPlayerId, onSave, onCancel }: DiceQuickLogFormProps) {
  const [selectedPlayerId, setSelectedPlayerId] = useState(defaultPlayerId ?? players[0]?.id ?? '');
  const [formula, setFormula] = useState('');
  const [totalStr, setTotalStr] = useState('');

  const canSubmit = formula.trim() !== '' && totalStr !== '';

  const handleSubmit = () => {
    const total = parseInt(totalStr, 10);
    if (isNaN(total) || !formula.trim()) return;
    onSave(selectedPlayerId || null, formula.trim(), total);
  };

  return (
    <div
      className="flex flex-col gap-2.5 rounded-xl border border-gray-200 bg-gray-50 p-3"
      data-testid="dice-quick-log-form"
    >
      <h4 className="text-xs font-semibold text-gray-600">Registra tiro manuale</h4>

      {players.length > 0 && (
        <select
          value={selectedPlayerId}
          onChange={e => setSelectedPlayerId(e.target.value)}
          className="rounded-lg border border-gray-300 px-2 py-1.5 text-xs outline-none focus:border-[hsl(142,70%,45%)]"
          data-testid="quick-log-player"
        >
          <option value="">— nessun giocatore —</option>
          {players.map(p => (
            <option key={p.id} value={p.id}>
              {p.name}
            </option>
          ))}
        </select>
      )}

      <input
        type="text"
        placeholder="Formula (es. 3d6+6)"
        value={formula}
        onChange={e => setFormula(e.target.value)}
        onKeyDown={e => {
          if (e.key === 'Enter' && canSubmit) handleSubmit();
        }}
        className="rounded-lg border border-gray-300 px-2 py-1.5 text-xs outline-none focus:border-[hsl(142,70%,45%)]"
        data-testid="quick-log-formula"
      />

      <input
        type="number"
        placeholder="Totale ottenuto"
        value={totalStr}
        onChange={e => setTotalStr(e.target.value)}
        onKeyDown={e => {
          if (e.key === 'Enter' && canSubmit) handleSubmit();
        }}
        className="rounded-lg border border-gray-300 px-2 py-1.5 text-xs outline-none focus:border-[hsl(142,70%,45%)]"
        data-testid="quick-log-total"
      />

      <div className="flex gap-2">
        <button
          type="button"
          onClick={onCancel}
          className="flex-1 rounded-lg border border-gray-300 py-1.5 text-xs font-medium text-gray-500 hover:bg-gray-100"
          data-testid="quick-log-cancel"
        >
          Annulla
        </button>
        <button
          type="button"
          onClick={handleSubmit}
          disabled={!canSubmit}
          className={cn(
            'flex-1 rounded-lg py-1.5 text-xs font-medium transition-colors',
            canSubmit
              ? 'bg-[hsl(142,70%,45%)] text-white hover:bg-[hsl(142,70%,40%)]'
              : 'cursor-not-allowed bg-gray-200 text-gray-400'
          )}
          data-testid="quick-log-save"
        >
          Salva
        </button>
      </div>
    </div>
  );
}

// ============================================================================
// DiceRollerTab
// ============================================================================

export function DiceRollerTab() {
  const { store, logEvent } = useToolkitDrawer();
  const customPresets = store(s => s.customDicePresets);
  const players = store(s => s.players);
  const currentTurnIndex = store(s => s.currentTurnIndex);
  const currentRound = store(s => s.currentRound);
  const { roll, lastResult, isRolling } = useDiceRoller();

  // Current player and next player (for CTA row attribution)
  const currentPlayer = players[currentTurnIndex] ?? null;
  const nextPlayerIndex = players.length > 1 ? (currentTurnIndex + 1) % players.length : -1;
  const nextPlayer = nextPlayerIndex >= 0 ? (players[nextPlayerIndex] ?? null) : null;

  // Collapsible sections
  const [showAiPresets, setShowAiPresets] = useState(false);
  const [showCustomPresets, setShowCustomPresets] = useState(true);
  const [showQuickLog, setShowQuickLog] = useState(false);

  // Save-preset inline form
  const [saveName, setSaveName] = useState('');
  const [lastFormula, setLastFormula] = useState('');

  // AI presets placeholder — will be populated from toolkit config in the future
  const aiPresets: DicePreset[] = [];

  // ── Roll with player attribution ─────────────────────────────────────────

  const handleRoll = useCallback(
    (formula: string) => {
      const result: DiceResult = roll(formula);
      setLastFormula(formula);

      logEvent(
        'dice_roll',
        {
          formula: result.formula,
          rolls: result.rolls,
          modifier: result.modifier,
          total: result.total,
        },
        currentPlayer
          ? { playerId: currentPlayer.id, playerName: currentPlayer.name, round: currentRound }
          : undefined
      );
    },
    [roll, logEvent, currentPlayer, currentRound]
  );

  // ── Re-roll same formula for same player ─────────────────────────────────

  const handleReroll = useCallback(() => {
    if (lastFormula && lastFormula !== '0') handleRoll(lastFormula);
  }, [handleRoll, lastFormula]);

  // ── Advance turn, then roll same formula for the new current player ──────

  const handleRollNext = useCallback(() => {
    if (!lastFormula || lastFormula === '0') return;
    store.getState().advanceTurn();
    // Read fresh state synchronously after mutation
    const state = store.getState();
    const newPlayer = state.players[state.currentTurnIndex] ?? null;
    const result = roll(lastFormula);
    logEvent(
      'dice_roll',
      {
        formula: result.formula,
        rolls: result.rolls,
        modifier: result.modifier,
        total: result.total,
      },
      newPlayer
        ? { playerId: newPlayer.id, playerName: newPlayer.name, round: state.currentRound }
        : undefined
    );
  }, [lastFormula, roll, logEvent, store]);

  // ── Manual quick log ─────────────────────────────────────────────────────

  const handleQuickLogSave = useCallback(
    (playerId: string | null, formula: string, total: number) => {
      const player = players.find(p => p.id === playerId) ?? null;
      logEvent(
        'dice_roll',
        { formula, rolls: [], modifier: 0, total },
        player ? { playerId: player.id, playerName: player.name, round: currentRound } : undefined
      );
      setShowQuickLog(false);
    },
    [players, logEvent, currentRound]
  );

  // ── Save preset ───────────────────────────────────────────────────────────

  const handleSavePreset = () => {
    const name = saveName.trim();
    if (!name || !lastFormula || lastFormula === '0') return;

    store.getState().addCustomPreset({
      name,
      formula: lastFormula,
      source: 'custom',
    });
    setSaveName('');
  };

  const hasResult = !!lastResult && !!lastFormula && lastFormula !== '0';

  return (
    <div className="flex flex-col gap-4" data-testid="dice-roller-tab">
      {/* Universal presets */}
      <section>
        <h3 className="mb-1.5 text-xs font-semibold uppercase tracking-wider text-gray-400">
          Preset
        </h3>
        <DicePresetRow presets={UNIVERSAL_DICE_PRESETS} onRoll={handleRoll} variant="universal" />
      </section>

      {/* AI presets (collapsible) */}
      {aiPresets.length > 0 && (
        <section>
          <button
            type="button"
            onClick={() => setShowAiPresets(v => !v)}
            className="mb-1.5 flex items-center gap-1 text-xs font-semibold uppercase tracking-wider text-purple-400"
          >
            <span className={cn('transition-transform', showAiPresets && 'rotate-90')}>
              &#9654;
            </span>
            Suggeriti AI
          </button>
          {showAiPresets && <DicePresetRow presets={aiPresets} onRoll={handleRoll} variant="ai" />}
        </section>
      )}

      {/* Custom presets (collapsible) */}
      <section>
        <button
          type="button"
          onClick={() => setShowCustomPresets(v => !v)}
          className="mb-1.5 flex items-center gap-1 text-xs font-semibold uppercase tracking-wider text-[hsl(142,70%,40%)]"
        >
          <span className={cn('transition-transform', showCustomPresets && 'rotate-90')}>
            &#9654;
          </span>
          I miei preset
        </button>
        {showCustomPresets && (
          <>
            <DicePresetRow presets={customPresets} onRoll={handleRoll} variant="custom" />

            {/* Save preset form */}
            {lastFormula && lastFormula !== '0' && (
              <div className="mt-2 flex items-center gap-2">
                <input
                  type="text"
                  placeholder="Nome preset"
                  value={saveName}
                  onChange={e => setSaveName(e.target.value)}
                  onKeyDown={e => {
                    if (e.key === 'Enter') handleSavePreset();
                  }}
                  className="flex-1 rounded-lg border border-gray-300 px-2 py-1 text-xs outline-none focus:border-[hsl(142,70%,45%)]"
                  data-testid="save-preset-input"
                />
                <button
                  type="button"
                  onClick={handleSavePreset}
                  disabled={!saveName.trim()}
                  className={cn(
                    'rounded-lg px-2 py-1 text-xs font-medium transition-colors',
                    saveName.trim()
                      ? 'bg-[hsl(142,70%,45%)] text-white hover:bg-[hsl(142,70%,40%)]'
                      : 'cursor-not-allowed bg-gray-200 text-gray-400'
                  )}
                  data-testid="save-preset-btn"
                >
                  Salva
                </button>
              </div>
            )}
          </>
        )}
      </section>

      {/* Pool builder */}
      <section>
        <h3 className="mb-1.5 text-xs font-semibold uppercase tracking-wider text-gray-400">
          Pool dadi
        </h3>
        <DicePoolBuilder onRoll={handleRoll} />
      </section>

      {/* Result display */}
      <DiceResultDisplay result={lastResult} isRolling={isRolling} />

      {/* CTA row — re-roll or pass to next player */}
      {hasResult && !showQuickLog && (
        <div className="flex items-center justify-center gap-2" data-testid="dice-cta-row">
          <button
            type="button"
            onClick={handleReroll}
            className="flex items-center gap-1.5 rounded-xl border border-gray-200 bg-white px-3 py-2 text-xs font-semibold text-gray-700 shadow-sm transition-colors hover:border-[hsl(142,70%,45%)] hover:text-[hsl(142,70%,45%)]"
            data-testid="dice-reroll-btn"
          >
            ↺ Ritira
          </button>
          {nextPlayer && (
            <button
              type="button"
              onClick={handleRollNext}
              className="flex items-center gap-1.5 rounded-xl bg-[hsl(142,70%,45%)] px-3 py-2 text-xs font-semibold text-white shadow-sm transition-colors hover:bg-[hsl(142,70%,40%)]"
              data-testid="dice-roll-next-btn"
            >
              → {nextPlayer.name}
            </button>
          )}
        </div>
      )}

      {/* Quick log — inline manual entry form or trigger link */}
      {showQuickLog ? (
        <DiceQuickLogForm
          players={players}
          defaultPlayerId={currentPlayer?.id}
          onSave={handleQuickLogSave}
          onCancel={() => setShowQuickLog(false)}
        />
      ) : (
        <button
          type="button"
          onClick={() => setShowQuickLog(true)}
          className="text-center text-xs text-gray-400 underline-offset-2 hover:text-gray-600 hover:underline"
          data-testid="dice-quick-log-btn"
        >
          📝 Registra tiro manuale
        </button>
      )}
    </div>
  );
}

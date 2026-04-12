'use client';

/**
 * EventDiaryTab — Chronological log of game events with filters.
 *
 * Manual entry modes:
 *  1. Quick note: plain text input (manual_entry, current player)
 *  2. Structured form: player + event type + type-specific value fields
 */

import React, { useMemo, useState } from 'react';

import { cn } from '@/lib/utils';

import { useToolkitDrawer } from '../ToolkitDrawerProvider';
import { DiaryEventRow } from './DiaryEventRow';
import { DiaryFilters } from './DiaryFilters';

import type { DiaryEvent, DiaryEventType, LocalPlayer } from '../types';

// ============================================================================
// Supported manual event types + their labels
// ============================================================================

interface ManualEventOption {
  type: DiaryEventType;
  label: string;
  icon: string;
}

const MANUAL_EVENT_OPTIONS: ManualEventOption[] = [
  { type: 'manual_entry', label: 'Nota', icon: '✍️' },
  { type: 'dice_roll', label: 'Dado', icon: '🎲' },
  { type: 'score_change', label: 'Punti', icon: '🏆' },
  { type: 'turn_change', label: 'Turno', icon: '🔄' },
  { type: 'round_advance', label: 'Round', icon: '🔔' },
];

// ============================================================================
// Structured form — player + type + dynamic value fields
// ============================================================================

interface DiaryEventFormProps {
  players: LocalPlayer[];
  scoreCategories: string[];
  currentRound: number;
  onSubmit: (
    type: DiaryEventType,
    payload: Record<string, unknown>,
    extra?: { playerId?: string; playerName?: string; round?: number }
  ) => void;
  onCancel: () => void;
}

function DiaryEventForm({
  players,
  scoreCategories,
  currentRound,
  onSubmit,
  onCancel,
}: DiaryEventFormProps) {
  const [selectedType, setSelectedType] = useState<DiaryEventType>('manual_entry');
  const [selectedPlayerId, setSelectedPlayerId] = useState(players[0]?.id ?? '');

  // manual_entry
  const [noteText, setNoteText] = useState('');

  // dice_roll
  const [diceFormula, setDiceFormula] = useState('');
  const [diceTotal, setDiceTotal] = useState('');

  // score_change
  const [scoreCategory, setScoreCategory] = useState(scoreCategories[0] ?? '');
  const [scoreDelta, setScoreDelta] = useState('');

  // turn_change
  const [nextPlayerId, setNextPlayerId] = useState(players[1]?.id ?? players[0]?.id ?? '');

  // round_advance — uses currentRound+1 (no user input needed)

  const player = players.find(p => p.id === selectedPlayerId) ?? null;

  const canSubmit = useMemo(() => {
    switch (selectedType) {
      case 'manual_entry':
        return noteText.trim() !== '';
      case 'dice_roll':
        return diceFormula.trim() !== '' && diceTotal !== '' && !isNaN(parseInt(diceTotal, 10));
      case 'score_change':
        return scoreCategory.trim() !== '' && scoreDelta !== '' && !isNaN(parseInt(scoreDelta, 10));
      case 'turn_change':
        return !!nextPlayerId;
      case 'round_advance':
        return true;
      default:
        return false;
    }
  }, [selectedType, noteText, diceFormula, diceTotal, scoreCategory, scoreDelta, nextPlayerId]);

  const handleSubmit = () => {
    if (!canSubmit) return;

    const extra = player
      ? { playerId: player.id, playerName: player.name, round: currentRound }
      : { round: currentRound };

    switch (selectedType) {
      case 'manual_entry':
        onSubmit('manual_entry', { text: noteText.trim() }, extra);
        break;
      case 'dice_roll': {
        const total = parseInt(diceTotal, 10);
        onSubmit(
          'dice_roll',
          { formula: diceFormula.trim(), rolls: [], modifier: 0, total },
          extra
        );
        break;
      }
      case 'score_change': {
        const delta = parseInt(scoreDelta, 10);
        // newTotal not available in manual entry; DiaryEventRow shows '?' gracefully
        onSubmit('score_change', { category: scoreCategory.trim(), delta }, extra);
        break;
      }
      case 'turn_change': {
        const nextPlayer = players.find(p => p.id === nextPlayerId);
        onSubmit(
          'turn_change',
          { nextPlayerName: nextPlayer?.name ?? '' },
          nextPlayer
            ? { playerId: nextPlayer.id, playerName: nextPlayer.name, round: currentRound }
            : extra
        );
        break;
      }
      case 'round_advance':
        onSubmit('round_advance', { round: currentRound + 1 }, { round: currentRound + 1 });
        break;
    }
  };

  return (
    <div
      className="flex flex-col gap-2.5 rounded-xl border border-gray-200 bg-gray-50 p-3"
      data-testid="diary-event-form"
    >
      {/* Type selector */}
      <div className="flex gap-1.5 overflow-x-auto pb-0.5" role="group" aria-label="Tipo evento">
        {MANUAL_EVENT_OPTIONS.map(opt => (
          <button
            key={opt.type}
            type="button"
            onClick={() => setSelectedType(opt.type)}
            className={cn(
              'flex shrink-0 items-center gap-1 rounded-full px-2.5 py-1 text-[10px] font-semibold transition-colors',
              selectedType === opt.type
                ? 'bg-[hsl(142,70%,45%)] text-white'
                : 'border border-gray-300 text-gray-600 hover:border-[hsl(142,70%,45%)]'
            )}
            data-testid={`diary-form-type-${opt.type}`}
          >
            <span>{opt.icon}</span>
            <span>{opt.label}</span>
          </button>
        ))}
      </div>

      {/* Player selector (hidden for turn_change and round_advance) */}
      {selectedType !== 'turn_change' && selectedType !== 'round_advance' && players.length > 0 && (
        <select
          value={selectedPlayerId}
          onChange={e => setSelectedPlayerId(e.target.value)}
          className="rounded-lg border border-gray-300 px-2 py-1.5 text-xs outline-none focus:border-[hsl(142,70%,45%)]"
          data-testid="diary-form-player"
        >
          <option value="">— nessun giocatore —</option>
          {players.map(p => (
            <option key={p.id} value={p.id}>
              {p.name}
            </option>
          ))}
        </select>
      )}

      {/* Dynamic value fields */}
      {selectedType === 'manual_entry' && (
        <input
          type="text"
          placeholder="Testo nota…"
          value={noteText}
          onChange={e => setNoteText(e.target.value)}
          onKeyDown={e => {
            if (e.key === 'Enter' && canSubmit) handleSubmit();
          }}
          autoFocus
          className="rounded-lg border border-gray-300 px-2 py-1.5 text-xs outline-none focus:border-[hsl(142,70%,45%)]"
          data-testid="diary-form-note-text"
        />
      )}

      {selectedType === 'dice_roll' && (
        <div className="flex gap-2">
          <input
            type="text"
            placeholder="Formula (es. 3d6+6)"
            value={diceFormula}
            onChange={e => setDiceFormula(e.target.value)}
            className="flex-1 rounded-lg border border-gray-300 px-2 py-1.5 text-xs outline-none focus:border-[hsl(142,70%,45%)]"
            data-testid="diary-form-dice-formula"
          />
          <input
            type="number"
            placeholder="Totale"
            value={diceTotal}
            onChange={e => setDiceTotal(e.target.value)}
            className="w-20 rounded-lg border border-gray-300 px-2 py-1.5 text-xs outline-none focus:border-[hsl(142,70%,45%)]"
            data-testid="diary-form-dice-total"
          />
        </div>
      )}

      {selectedType === 'score_change' && (
        <div className="flex gap-2">
          {scoreCategories.length > 0 ? (
            <select
              value={scoreCategory}
              onChange={e => setScoreCategory(e.target.value)}
              className="flex-1 rounded-lg border border-gray-300 px-2 py-1.5 text-xs outline-none focus:border-[hsl(142,70%,45%)]"
              data-testid="diary-form-score-category"
            >
              {scoreCategories.map(cat => (
                <option key={cat} value={cat}>
                  {cat}
                </option>
              ))}
            </select>
          ) : (
            <input
              type="text"
              placeholder="Categoria"
              value={scoreCategory}
              onChange={e => setScoreCategory(e.target.value)}
              className="flex-1 rounded-lg border border-gray-300 px-2 py-1.5 text-xs outline-none focus:border-[hsl(142,70%,45%)]"
              data-testid="diary-form-score-category"
            />
          )}
          <input
            type="number"
            placeholder="±punti"
            value={scoreDelta}
            onChange={e => setScoreDelta(e.target.value)}
            className="w-20 rounded-lg border border-gray-300 px-2 py-1.5 text-xs outline-none focus:border-[hsl(142,70%,45%)]"
            data-testid="diary-form-score-delta"
          />
        </div>
      )}

      {selectedType === 'turn_change' && players.length > 1 && (
        <select
          value={nextPlayerId}
          onChange={e => setNextPlayerId(e.target.value)}
          className="rounded-lg border border-gray-300 px-2 py-1.5 text-xs outline-none focus:border-[hsl(142,70%,45%)]"
          data-testid="diary-form-turn-next"
        >
          {players.map(p => (
            <option key={p.id} value={p.id}>
              {p.name}
            </option>
          ))}
        </select>
      )}

      {selectedType === 'round_advance' && (
        <p className="text-xs text-gray-500">Registra inizio Round {currentRound + 1}</p>
      )}

      {/* Actions */}
      <div className="flex gap-2">
        <button
          type="button"
          onClick={onCancel}
          className="flex-1 rounded-lg border border-gray-300 py-1.5 text-xs font-medium text-gray-500 hover:bg-gray-100"
          data-testid="diary-form-cancel"
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
          data-testid="diary-form-submit"
        >
          Salva
        </button>
      </div>
    </div>
  );
}

// ============================================================================
// Round group helper
// ============================================================================

function groupByRound(events: DiaryEvent[]): Array<{ round: number | null; events: DiaryEvent[] }> {
  const groups: Array<{ round: number | null; events: DiaryEvent[] }> = [];
  let currentRound: number | null | undefined = undefined;
  let currentGroup: DiaryEvent[] = [];

  for (const evt of events) {
    const round = evt.round ?? null;
    if (round !== currentRound) {
      if (currentGroup.length > 0) {
        groups.push({ round: currentRound ?? null, events: currentGroup });
      }
      currentRound = round;
      currentGroup = [];
    }
    currentGroup.push(evt);
  }
  if (currentGroup.length > 0) {
    groups.push({ round: currentRound ?? null, events: currentGroup });
  }
  return groups;
}

// ============================================================================
// EventDiaryTab
// ============================================================================

export function EventDiaryTab() {
  const { store, logEvent } = useToolkitDrawer();
  const diary = store(s => s.diary);
  const players = store(s => s.players);
  const scoreCategories = store(s => s.scoreCategories);
  const currentTurnIndex = store(s => s.currentTurnIndex);
  const currentRound = store(s => s.currentRound);
  const currentPlayer = players[currentTurnIndex];

  const [activeFilters, setActiveFilters] = useState<DiaryEventType[]>([]);
  const [quickText, setQuickText] = useState('');
  const [showStructuredForm, setShowStructuredForm] = useState(false);

  const filteredEvents = useMemo(() => {
    const sorted = [...diary].sort((a, b) => b.timestamp - a.timestamp);
    if (activeFilters.length === 0) return sorted;
    return sorted.filter(e => activeFilters.includes(e.type));
  }, [diary, activeFilters]);

  const groups = useMemo(() => groupByRound(filteredEvents), [filteredEvents]);

  const handleToggleFilter = (type: DiaryEventType) => {
    setActiveFilters(prev =>
      prev.includes(type) ? prev.filter(t => t !== type) : [...prev, type]
    );
  };

  const handleReset = () => setActiveFilters([]);

  // Quick plain-text note (existing behavior)
  const handleSubmitQuick = () => {
    const trimmed = quickText.trim();
    if (!trimmed) return;
    logEvent(
      'manual_entry',
      { text: trimmed },
      {
        playerId: currentPlayer?.id,
        playerName: currentPlayer?.name,
        round: currentRound,
      }
    );
    setQuickText('');
  };

  // Structured form submit
  const handleStructuredSubmit = (
    type: DiaryEventType,
    payload: Record<string, unknown>,
    extra?: { playerId?: string; playerName?: string; round?: number }
  ) => {
    logEvent(type, payload, extra);
    setShowStructuredForm(false);
  };

  return (
    <div className="flex h-full flex-col gap-3" data-testid="event-diary-tab">
      {/* Filters */}
      <DiaryFilters
        activeFilters={activeFilters}
        onToggle={handleToggleFilter}
        onReset={handleReset}
      />

      {/* Event list */}
      <div className="flex-1 overflow-y-auto">
        {groups.length === 0 ? (
          <p className="py-4 text-center text-xs italic text-gray-400">Nessun evento nel diario</p>
        ) : (
          groups.map((group, idx) => (
            <div key={idx} className="mb-2">
              {group.round !== null && (
                <div className="mb-1 mt-2 text-center text-[10px] font-bold uppercase tracking-wider text-gray-400">
                  ── Round {group.round} ──
                </div>
              )}
              {group.events.map(evt => (
                <DiaryEventRow key={evt.id} event={evt} />
              ))}
            </div>
          ))
        )}
      </div>

      {/* Bottom entry area */}
      {showStructuredForm ? (
        <DiaryEventForm
          players={players}
          scoreCategories={scoreCategories}
          currentRound={currentRound}
          onSubmit={handleStructuredSubmit}
          onCancel={() => setShowStructuredForm(false)}
        />
      ) : (
        <div className="flex flex-col gap-1.5 border-t border-gray-200 pt-2">
          {/* Quick text note */}
          <div className="flex items-center gap-2">
            <input
              type="text"
              value={quickText}
              onChange={e => setQuickText(e.target.value)}
              onKeyDown={e => {
                if (e.key === 'Enter') handleSubmitQuick();
              }}
              placeholder="✍️ Nota rapida…"
              className="flex-1 rounded-lg border border-gray-300 px-3 py-1.5 text-xs outline-none focus:border-[hsl(142,70%,45%)]"
              data-testid="diary-manual-input"
            />
            <button
              type="button"
              onClick={handleSubmitQuick}
              disabled={!quickText.trim()}
              className="rounded-lg bg-[hsl(142,70%,45%)] px-3 py-1.5 text-xs font-medium text-white disabled:cursor-not-allowed disabled:bg-gray-200 disabled:text-gray-400"
              data-testid="diary-manual-submit"
            >
              +
            </button>
          </div>
          {/* Toggle structured form */}
          <button
            type="button"
            onClick={() => setShowStructuredForm(true)}
            className="text-center text-[10px] text-gray-400 underline-offset-2 hover:text-gray-600 hover:underline"
            data-testid="diary-structured-form-btn"
          >
            + Registra evento strutturato
          </button>
        </div>
      )}
    </div>
  );
}

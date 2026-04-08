'use client';

/**
 * EventDiaryTab — Chronological log of game events with filters and manual entries.
 */

import React, { useMemo, useState } from 'react';

import { useToolkitDrawer } from '../ToolkitDrawerProvider';
import { DiaryEventRow } from './DiaryEventRow';
import { DiaryFilters } from './DiaryFilters';

import type { DiaryEvent, DiaryEventType } from '../types';

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

export function EventDiaryTab() {
  const { store, logEvent } = useToolkitDrawer();
  const diary = store(s => s.diary);
  const players = store(s => s.players);
  const currentTurnIndex = store(s => s.currentTurnIndex);
  const currentPlayer = players[currentTurnIndex];

  const [activeFilters, setActiveFilters] = useState<DiaryEventType[]>([]);
  const [manualText, setManualText] = useState('');

  const filteredEvents = useMemo(() => {
    // Sort by timestamp desc (most recent first)
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

  const handleSubmitManual = () => {
    const trimmed = manualText.trim();
    if (!trimmed) return;
    logEvent(
      'manual_entry',
      { text: trimmed },
      {
        playerId: currentPlayer?.id,
        playerName: currentPlayer?.name,
        round: store.getState().currentRound,
      }
    );
    setManualText('');
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

      {/* Manual entry input */}
      <div className="flex items-center gap-2 border-t border-gray-200 pt-2">
        <input
          type="text"
          value={manualText}
          onChange={e => setManualText(e.target.value)}
          onKeyDown={e => {
            if (e.key === 'Enter') handleSubmitManual();
          }}
          placeholder="✍️ Aggiungi nota al diario"
          className="flex-1 rounded-lg border border-gray-300 px-3 py-1.5 text-xs outline-none focus:border-[hsl(142,70%,45%)]"
          data-testid="diary-manual-input"
        />
        <button
          type="button"
          onClick={handleSubmitManual}
          disabled={!manualText.trim()}
          className="rounded-lg bg-[hsl(142,70%,45%)] px-3 py-1.5 text-xs font-medium text-white disabled:cursor-not-allowed disabled:bg-gray-200 disabled:text-gray-400"
          data-testid="diary-manual-submit"
        >
          +
        </button>
      </div>
    </div>
  );
}

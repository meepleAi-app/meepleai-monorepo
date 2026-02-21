'use client';

/**
 * CounterTool — per-player resource / token counter with +/- controls
 * Epic #4968: Game Session Toolkit v2
 */

import React from 'react';

import { Minus, Plus } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';

import type { CounterState, Participant } from './types';

// ── Public types ──────────────────────────────────────────────────────────────

export interface CounterToolConfig {
  name: string;
  minValue: number;
  maxValue: number;
  defaultValue: number;
  isPerPlayer: boolean;
  /** Optional icon to display next to the counter name */
  icon: React.ReactNode | null;
  /** Accent color for the counter values (hex or CSS color) */
  color: string;
}

// ── Component ─────────────────────────────────────────────────────────────────

interface CounterToolProps {
  config: CounterToolConfig;
  counterState: CounterState;
  participants: Participant[];
  currentUserId: string;
  onApplyChange: (playerId: string, change: number) => Promise<void>;
}

export function CounterTool({
  config,
  counterState,
  participants,
  onApplyChange,
}: CounterToolProps) {
  return (
    <div className="max-w-md mx-auto space-y-4">
      {/* Counter name */}
      <div className="flex items-center gap-2">
        {config.icon && <span aria-hidden="true">{config.icon}</span>}
        <h3 className="text-lg font-bold text-slate-800 dark:text-slate-200">{config.name}</h3>
        <span className="text-xs text-slate-400 dark:text-slate-500">
          ({counterState.minValue}–{counterState.maxValue})
        </span>
      </div>

      {/* Per-player rows */}
      <ul aria-label={`${config.name} counters per player`} className="space-y-3">
        {participants.map((p) => {
          const value = counterState.playerValues[p.id] ?? counterState.defaultValue;
          const atMin = value <= counterState.minValue;
          const atMax = value >= counterState.maxValue;

          return (
            <li
              key={p.id}
              className="flex items-center gap-4 rounded-xl bg-white dark:bg-slate-800/60 ring-1 ring-slate-200 dark:ring-slate-700 px-4 py-3"
            >
              {/* Avatar dot */}
              <span
                className="w-3 h-3 rounded-full shrink-0"
                style={{ backgroundColor: p.avatarColor }}
                aria-hidden="true"
              />

              {/* Player name + value */}
              <div className="flex-1 min-w-0">
                <p className="text-sm font-medium text-slate-700 dark:text-slate-300 truncate">
                  {p.displayName}
                </p>
                <p
                  className="text-3xl font-black tabular-nums leading-none"
                  style={{ color: config.color }}
                  aria-label={`${value} ${config.name}`}
                >
                  {value}
                </p>
              </div>

              {/* Controls */}
              <div className="flex items-center gap-2 shrink-0">
                <Button
                  variant="outline"
                  size="icon"
                  onClick={() => void onApplyChange(p.id, -1)}
                  disabled={atMin}
                  aria-label={`Decrease ${config.name} for ${p.displayName}`}
                  className="h-9 w-9"
                >
                  <Minus className="w-4 h-4" aria-hidden="true" />
                </Button>
                <Button
                  variant="outline"
                  size="icon"
                  onClick={() => void onApplyChange(p.id, 1)}
                  disabled={atMax}
                  aria-label={`Increase ${config.name} for ${p.displayName}`}
                  className="h-9 w-9"
                >
                  <Plus className="w-4 h-4" aria-hidden="true" />
                </Button>
              </div>
            </li>
          );
        })}
      </ul>
    </div>
  );
}

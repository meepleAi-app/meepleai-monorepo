'use client';

/**
 * DicePoolBuilder — Interactive builder for composing a dice formula
 * from individual die types with +/- steppers and a modifier.
 */

import React, { useCallback, useMemo, useState } from 'react';

import { cn } from '@/lib/utils';

// ============================================================================
// Types & Constants
// ============================================================================

interface DieType {
  sides: number;
  label: string;
}

const DIE_TYPES: DieType[] = [
  { sides: 4, label: 'd4' },
  { sides: 6, label: 'd6' },
  { sides: 8, label: 'd8' },
  { sides: 10, label: 'd10' },
  { sides: 12, label: 'd12' },
  { sides: 20, label: 'd20' },
];

export interface DicePoolBuilderProps {
  onRoll: (formula: string) => void;
}

// ============================================================================
// Component
// ============================================================================

export function DicePoolBuilder({ onRoll }: DicePoolBuilderProps) {
  const [pool, setPool] = useState<Record<number, number>>({});
  const [modifier, setModifier] = useState(0);

  const updateDie = useCallback((sides: number, delta: number) => {
    setPool(prev => {
      const current = prev[sides] ?? 0;
      const next = Math.max(0, Math.min(20, current + delta));
      if (next === 0) {
        const { [sides]: _, ...rest } = prev;
        return rest;
      }
      return { ...prev, [sides]: next };
    });
  }, []);

  /** Build the formula string from the current pool. */
  const formula = useMemo(() => {
    const parts: string[] = [];
    for (const die of DIE_TYPES) {
      const count = pool[die.sides] ?? 0;
      if (count > 0) {
        parts.push(`${count}${die.label}`);
      }
    }
    if (modifier > 0) parts.push(`+${modifier}`);
    if (modifier < 0) parts.push(`${modifier}`);
    return parts.join('+').replace(/\+\+/g, '+').replace(/\+-/g, '-') || '0';
  }, [pool, modifier]);

  const hasDice = Object.values(pool).some(c => c > 0);

  const handleRoll = () => {
    if (hasDice) onRoll(formula);
  };

  return (
    <div className="flex flex-col gap-3" data-testid="dice-pool-builder">
      {/* Die type grid */}
      <div className="grid grid-cols-3 gap-2">
        {DIE_TYPES.map(die => {
          const count = pool[die.sides] ?? 0;
          return (
            <div
              key={die.sides}
              className="flex items-center justify-between rounded-lg border border-gray-200 px-2 py-1.5"
              data-testid={`die-stepper-${die.label}`}
            >
              <span className="text-xs font-semibold text-gray-600">{die.label}</span>
              <div className="flex items-center gap-1">
                <button
                  type="button"
                  onClick={() => updateDie(die.sides, -1)}
                  disabled={count === 0}
                  className={cn(
                    'flex h-5 w-5 items-center justify-center rounded text-xs font-bold transition-colors',
                    count === 0
                      ? 'cursor-not-allowed text-gray-300'
                      : 'text-gray-600 hover:bg-gray-100'
                  )}
                  aria-label={`Rimuovi ${die.label}`}
                >
                  -
                </button>
                <span className="w-4 text-center text-xs font-medium">{count}</span>
                <button
                  type="button"
                  onClick={() => updateDie(die.sides, 1)}
                  className="flex h-5 w-5 items-center justify-center rounded text-xs font-bold text-gray-600 transition-colors hover:bg-gray-100"
                  aria-label={`Aggiungi ${die.label}`}
                >
                  +
                </button>
              </div>
            </div>
          );
        })}
      </div>

      {/* Modifier */}
      <div className="flex items-center gap-2">
        <span className="text-xs font-medium text-gray-500">Modificatore:</span>
        <div className="flex items-center gap-1">
          <button
            type="button"
            onClick={() => setModifier(m => m - 1)}
            className="flex h-5 w-5 items-center justify-center rounded text-xs font-bold text-gray-600 hover:bg-gray-100"
            aria-label="Riduci modificatore"
          >
            -
          </button>
          <span className="w-8 text-center text-sm font-medium">
            {modifier >= 0 ? `+${modifier}` : modifier}
          </span>
          <button
            type="button"
            onClick={() => setModifier(m => m + 1)}
            className="flex h-5 w-5 items-center justify-center rounded text-xs font-bold text-gray-600 hover:bg-gray-100"
            aria-label="Aumenta modificatore"
          >
            +
          </button>
        </div>
      </div>

      {/* Current formula display */}
      {hasDice && (
        <div
          className="text-center text-sm font-medium text-gray-700"
          data-testid="dice-formula-display"
        >
          {formula}
        </div>
      )}

      {/* Roll button */}
      <button
        type="button"
        onClick={handleRoll}
        disabled={!hasDice}
        className={cn(
          'rounded-xl py-2.5 text-sm font-bold transition-colors',
          hasDice
            ? 'bg-[hsl(142,70%,45%)] text-white hover:bg-[hsl(142,70%,40%)] active:scale-[0.98]'
            : 'cursor-not-allowed bg-gray-200 text-gray-400'
        )}
        data-testid="dice-roll-btn"
      >
        LANCIA
      </button>
    </div>
  );
}

/** Returns the current formula string from pool state (exported for save-preset). */
export function buildFormula(pool: Record<number, number>, modifier: number): string {
  const parts: string[] = [];
  for (const die of DIE_TYPES) {
    const count = pool[die.sides] ?? 0;
    if (count > 0) {
      parts.push(`${count}d${die.sides}`);
    }
  }
  if (modifier > 0) parts.push(`+${modifier}`);
  if (modifier < 0) parts.push(`${modifier}`);
  return parts.join('+').replace(/\+\+/g, '+').replace(/\+-/g, '-') || '0';
}

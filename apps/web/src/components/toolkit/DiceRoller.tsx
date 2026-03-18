'use client';

import React, { useState } from 'react';

import { Button } from '@/components/ui/primitives/button';

type DiceType = 'd4' | 'd6' | 'd8' | 'd10' | 'd12' | 'd20';

const DICE_TYPES: DiceType[] = ['d4', 'd6', 'd8', 'd10', 'd12', 'd20'];

const DICE_FACES: Record<DiceType, number> = {
  d4: 4,
  d6: 6,
  d8: 8,
  d10: 10,
  d12: 12,
  d20: 20,
};

interface RollResult {
  dice: number[];
  total: number;
  diceType: DiceType;
  count: number;
}

/**
 * DiceRoller — configurable dice roller for board game sessions.
 */
export function DiceRoller() {
  const [selectedDice, setSelectedDice] = useState<DiceType>('d6');
  const [count, setCount] = useState(1);
  const [result, setResult] = useState<RollResult | null>(null);

  const roll = () => {
    const faces = DICE_FACES[selectedDice];
    const dice = Array.from({ length: count }, () => Math.floor(Math.random() * faces) + 1);
    const total = dice.reduce((a, b) => a + b, 0);
    setResult({ dice, total, diceType: selectedDice, count });
  };

  return (
    <div className="space-y-4" data-testid="dice-roller">
      {/* Dice type selector */}
      <div>
        <p className="mb-2 text-xs font-medium text-slate-500">Tipo di dado</p>
        <div className="flex flex-wrap gap-2">
          {DICE_TYPES.map(d => (
            <button
              key={d}
              onClick={() => setSelectedDice(d)}
              className={`rounded-md border px-3 py-1.5 text-sm font-medium transition-colors ${
                selectedDice === d
                  ? 'border-amber-500 bg-amber-500 text-white'
                  : 'border-slate-200 bg-white text-slate-700 hover:border-amber-300 hover:bg-amber-50'
              }`}
              aria-label={`Select ${d}`}
              aria-pressed={selectedDice === d}
            >
              {d}
            </button>
          ))}
        </div>
      </div>

      {/* Count selector */}
      <div>
        <p className="mb-2 text-xs font-medium text-slate-500">Numero di dadi: {count}</p>
        <div className="flex items-center gap-2">
          <Button
            variant="outline"
            size="sm"
            onClick={() => setCount(c => Math.max(1, c - 1))}
            aria-label="Decrease dice count"
            disabled={count <= 1}
          >
            −
          </Button>
          <span className="w-8 text-center text-sm font-semibold tabular-nums">{count}</span>
          <Button
            variant="outline"
            size="sm"
            onClick={() => setCount(c => Math.min(10, c + 1))}
            aria-label="Increase dice count"
            disabled={count >= 10}
          >
            +
          </Button>
        </div>
      </div>

      {/* Roll button */}
      <Button onClick={roll} className="w-full" aria-label="Roll dice">
        🎲 Lancia {count}
        {selectedDice}
      </Button>

      {/* Results */}
      {result && (
        <div
          className="rounded-lg border border-amber-200 bg-amber-50 p-3 space-y-2"
          data-testid="dice-result"
        >
          <div className="flex flex-wrap gap-2">
            {result.dice.map((val, i) => (
              <span
                key={i}
                className="flex h-9 w-9 items-center justify-center rounded-md border-2 border-amber-400 bg-white text-sm font-bold text-amber-700"
                aria-label={`Die ${i + 1}: ${val}`}
              >
                {val}
              </span>
            ))}
          </div>
          {result.count > 1 && (
            <p className="text-sm font-semibold text-amber-800">
              Totale: <span className="text-lg">{result.total}</span>
            </p>
          )}
        </div>
      )}
    </div>
  );
}

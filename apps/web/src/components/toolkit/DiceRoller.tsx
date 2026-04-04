'use client';

import React, { useState } from 'react';

import { Button } from '@/components/ui/primitives/button';
import type { DiceConfig } from '@/lib/types/standalone-toolkit';

export interface DiceRollResult {
  faces: string[]; // single results (strings for custom, numbers as strings for standard)
  total: string; // numeric sum for standard, join(' | ') for custom
}

interface DiceRollerProps {
  config: DiceConfig;
  actorLabel?: string;
  onRoll?: (result: DiceRollResult) => void;
}

function rollDice(config: DiceConfig): DiceRollResult {
  if (config.customFaces) {
    const faces = Array.from(
      { length: config.count },
      () => config.customFaces![Math.floor(Math.random() * config.customFaces!.length)]
    );
    return { faces, total: faces.join(' | ') };
  }
  const faces = Array.from({ length: config.count }, () =>
    String(Math.floor(Math.random() * config.sides!) + 1)
  );
  const total = String(faces.reduce((a, b) => a + Number(b), 0));
  return { faces, total };
}

export function DiceRoller({ config, actorLabel, onRoll }: DiceRollerProps) {
  const [result, setResult] = useState<DiceRollResult | null>(null);

  const handleRoll = () => {
    const r = rollDice(config);
    setResult(r);
    onRoll?.(r);
  };

  const isMulti = config.count > 1;
  const isCustom = Boolean(config.customFaces);

  return (
    <div
      className="space-y-3 rounded-lg border border-slate-200 bg-white p-4"
      data-testid="dice-roller"
    >
      <div className="flex items-center justify-between">
        <span className="text-sm font-semibold text-slate-700">{config.name}</span>
        {actorLabel && <span className="text-xs text-slate-400">{actorLabel}</span>}
      </div>

      {config.customFaces && <p className="text-xs text-slate-500">{config.description}</p>}

      <Button onClick={handleRoll} className="w-full" aria-label={`Tira ${config.name}`}>
        🎲 Tira {config.name}
      </Button>

      {result && (
        <div className="space-y-1 text-center">
          {isMulti && (
            <div className="flex justify-center gap-2">
              {result.faces.map((f, i) => (
                <span
                  key={i}
                  className="flex h-10 w-10 items-center justify-center rounded-md border-2 border-amber-400 bg-amber-50 text-lg font-bold"
                  data-testid="dice-result"
                >
                  {f}
                </span>
              ))}
            </div>
          )}
          {!isMulti && (
            <span
              className="flex mx-auto h-14 w-14 items-center justify-center rounded-md border-2 border-amber-400 bg-amber-50 text-2xl font-bold"
              data-testid="dice-result"
            >
              {result.faces[0]}
            </span>
          )}
          {isMulti && !isCustom && (
            <p className="text-sm font-semibold text-slate-600">
              Totale: <span data-testid="dice-total">{result.total}</span>
            </p>
          )}
        </div>
      )}
    </div>
  );
}

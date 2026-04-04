'use client';

import { useState } from 'react';

import { Dice5 } from 'lucide-react';

import { cn } from '@/lib/utils';
import { useSessionStore } from '@/stores/session';

type DiceConfig = { label: string; count: number; sides: number };

const DICE_TYPES: DiceConfig[] = [
  { label: '2d6', count: 2, sides: 6 },
  { label: '1d6', count: 1, sides: 6 },
  { label: '1d20', count: 1, sides: 20 },
  { label: '1d12', count: 1, sides: 12 },
  { label: '1d8', count: 1, sides: 8 },
];

function rollDice(config: DiceConfig): { values: number[]; total: number } {
  const values = Array.from(
    { length: config.count },
    () => Math.floor(Math.random() * config.sides) + 1
  );
  return { values, total: values.reduce((a, b) => a + b, 0) };
}

interface SimpleDiceRollerProps {
  playerId: string;
  playerName: string;
}

export function SimpleDiceRoller({ playerId, playerName }: SimpleDiceRollerProps) {
  const [selectedType, setSelectedType] = useState(0);
  const [lastResult, setLastResult] = useState<{ values: number[]; total: number } | null>(null);
  const addEvent = useSessionStore(s => s.addEvent);

  const handleRoll = () => {
    const config = DICE_TYPES[selectedType];
    const result = rollDice(config);
    setLastResult(result);

    addEvent({
      id: crypto.randomUUID(),
      type: 'dice_roll',
      playerId,
      data: { playerName, values: result.values, total: result.total, diceType: config.label },
      timestamp: new Date().toISOString(),
    });
  };

  return (
    <div className="flex flex-col gap-3 p-3" data-testid="dice-roller">
      <div className="flex items-center gap-2">
        <select
          value={selectedType}
          onChange={e => setSelectedType(Number(e.target.value))}
          aria-label="Tipo dadi"
          className="flex-1 rounded-lg border border-border bg-background px-3 py-1.5 text-sm"
        >
          {DICE_TYPES.map((dt, i) => (
            <option key={dt.label} value={i}>
              {dt.label}
            </option>
          ))}
        </select>
        <button
          onClick={handleRoll}
          aria-label="Lancia dadi"
          className={cn(
            'inline-flex items-center gap-2 px-4 py-1.5 rounded-lg',
            'bg-primary text-primary-foreground font-medium text-sm',
            'hover:bg-primary/90 transition-colors'
          )}
        >
          <Dice5 className="h-4 w-4" />
          Lancia
        </button>
      </div>

      {lastResult && (
        <div
          data-testid="dice-result"
          className="flex items-center gap-3 p-3 rounded-lg bg-muted/50"
        >
          <span className="text-3xl font-bold font-quicksand tabular-nums">{lastResult.total}</span>
          <span className="text-sm text-muted-foreground">({lastResult.values.join(' + ')})</span>
        </div>
      )}
    </div>
  );
}

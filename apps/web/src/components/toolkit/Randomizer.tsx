'use client';

import { useState, KeyboardEvent } from 'react';

import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { useStandaloneToolkitStore } from '@/lib/stores/standalone-toolkit-store';

const MAX_ITEMS = 50;

interface RandomizerProps {
  onAction?: (extracted: string) => void;
}

export function Randomizer({ onAction }: RandomizerProps) {
  const [inputValue, setInputValue] = useState('');
  const { randomizer, setRandomizerItems, extractRandom, resetRandomizer } =
    useStandaloneToolkitStore();

  const addItem = () => {
    const trimmed = inputValue.trim();
    if (!trimmed || randomizer.originalItems.length >= MAX_ITEMS) return;
    const next = [...randomizer.originalItems, trimmed];
    setRandomizerItems(next);
    setInputValue('');
  };

  const handleKeyDown = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter') addItem();
  };

  const handleExtract = () => {
    const result = extractRandom();
    if (result) onAction?.(result);
  };

  const isEmpty = randomizer.remainingItems.length === 0;

  return (
    <div className="space-y-3 rounded-lg border border-slate-200 bg-white p-4">
      <div className="flex items-center justify-between">
        <span className="text-sm font-semibold text-slate-700">Randomizzatore</span>
        <span className="text-xs text-slate-400">
          <span data-testid="pool-count">{randomizer.remainingItems.length}</span>/
          {randomizer.originalItems.length}
        </span>
      </div>

      <div className="flex gap-2">
        <Input
          value={inputValue}
          onChange={e => setInputValue(e.target.value)}
          onKeyDown={handleKeyDown}
          placeholder="Aggiungi voce..."
          className="flex-1"
          disabled={randomizer.originalItems.length >= MAX_ITEMS}
        />
        <Button
          variant="outline"
          size="sm"
          onClick={addItem}
          disabled={randomizer.originalItems.length >= MAX_ITEMS}
        >
          +
        </Button>
      </div>

      {randomizer.lastExtracted && (
        <div
          className="rounded-md border-2 border-purple-300 bg-purple-50 p-3 text-center text-base font-semibold text-purple-800"
          data-testid="randomizer-result"
        >
          {randomizer.lastExtracted}
        </div>
      )}

      {isEmpty && randomizer.originalItems.length > 0 && (
        <p className="text-center text-xs text-slate-400">
          Pool esaurito — premi Reset per ricominciare
        </p>
      )}

      <div className="flex gap-2">
        <Button
          onClick={handleExtract}
          disabled={isEmpty || randomizer.originalItems.length === 0}
          className="flex-1"
          aria-label="Estrai elemento"
        >
          🎯 Estrai
        </Button>
        <Button
          variant="outline"
          onClick={resetRandomizer}
          disabled={randomizer.originalItems.length === 0}
          aria-label="Reset pool"
        >
          ↩ Reset
        </Button>
      </div>
    </div>
  );
}

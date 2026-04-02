'use client';

import { useEffect } from 'react';

import { Button } from '@/components/ui/primitives/button';
import { useStandaloneToolkitStore } from '@/lib/stores/standalone-toolkit-store';

interface CounterToolProps {
  id: string;
  name: string;
  initialValue: number;
  min?: number;
  max?: number;
  onAction?: (action: string, value: number) => void;
}

export function CounterTool({ id, name, initialValue, min, max, onAction }: CounterToolProps) {
  const { counters, initCounters, incrementCounter, decrementCounter, resetCounter } =
    useStandaloneToolkitStore();

  const counter = counters.find(c => c.id === id);

  useEffect(() => {
    if (!counter) {
      initCounters([{ id, name, initialValue, min, max }]);
    }
  }, [id]); // eslint-disable-line react-hooks/exhaustive-deps

  if (!counter) return null;

  const atMin = min !== undefined && counter.value <= min;
  const atMax = max !== undefined && counter.value >= max;

  return (
    <div className="space-y-2 rounded-lg border border-slate-200 bg-white p-4">
      <div className="text-center text-sm font-semibold text-slate-700">{counter.name}</div>
      <div className="flex items-center justify-center gap-4">
        <Button
          variant="outline"
          size="sm"
          className="h-12 w-12 text-xl"
          onClick={() => {
            decrementCounter(id);
            onAction?.('decrement', counter.value - 1);
          }}
          disabled={atMin}
          aria-label={`- ${name}`}
        >
          −
        </Button>
        <span
          className="w-16 text-center text-3xl font-bold tabular-nums text-slate-800"
          data-testid="counter-value"
        >
          {counter.value}
        </span>
        <Button
          variant="outline"
          size="sm"
          className="h-12 w-12 text-xl"
          onClick={() => {
            incrementCounter(id);
            onAction?.('increment', counter.value + 1);
          }}
          disabled={atMax}
          aria-label={`+ ${name}`}
        >
          +
        </Button>
      </div>
      {(min !== undefined || max !== undefined) && (
        <p className="text-center text-xs text-slate-400">
          {min ?? '−∞'} – {max ?? '+∞'}
        </p>
      )}
      <Button
        variant="ghost"
        size="sm"
        className="w-full text-xs text-slate-400"
        onClick={() => {
          resetCounter(id);
          onAction?.('reset', initialValue);
        }}
        aria-label={`Reset ${name}`}
      >
        Reset
      </Button>
    </div>
  );
}

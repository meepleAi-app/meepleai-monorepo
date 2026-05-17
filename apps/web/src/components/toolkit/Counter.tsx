'use client';

import React, { useState } from 'react';

import { Plus, RotateCcw, Trash2 } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';

interface CounterEntry {
  id: string;
  label: string;
  value: number;
}

const DEFAULT_COUNTERS: CounterEntry[] = [
  { id: '1', label: 'Punti vita', value: 20 },
  { id: '2', label: 'Risorse', value: 0 },
];

/**
 * Counter — multiple named counters for board game sessions.
 */
export function Counter() {
  const [counters, setCounters] = useState<CounterEntry[]>(DEFAULT_COUNTERS);
  const [newLabel, setNewLabel] = useState('');

  const increment = (id: string) => {
    setCounters(prev => prev.map(c => (c.id === id ? { ...c, value: c.value + 1 } : c)));
  };

  const decrement = (id: string) => {
    setCounters(prev => prev.map(c => (c.id === id ? { ...c, value: c.value - 1 } : c)));
  };

  const reset = (id: string) => {
    setCounters(prev => prev.map(c => (c.id === id ? { ...c, value: 0 } : c)));
  };

  const remove = (id: string) => {
    setCounters(prev => prev.filter(c => c.id !== id));
  };

  const addCounter = () => {
    const label = newLabel.trim();
    if (!label) return;
    setCounters(prev => [...prev, { id: crypto.randomUUID(), label, value: 0 }]);
    setNewLabel('');
  };

  return (
    <div className="space-y-3" data-testid="counter">
      {/* Counter list */}
      {counters.length === 0 && (
        <p className="py-4 text-center text-sm text-muted-foreground">Nessun contatore. Aggiungine uno!</p>
      )}

      {counters.map(counter => (
        <div
          key={counter.id}
          className="flex items-center gap-2 rounded-lg border border-border bg-card p-3"
          data-testid={`counter-${counter.id}`}
        >
          <span className="min-w-0 flex-1 truncate text-sm font-medium text-foreground">
            {counter.label}
          </span>

          <div className="flex items-center gap-1">
            <Button
              variant="outline"
              size="sm"
              onClick={() => decrement(counter.id)}
              aria-label={`Decrement ${counter.label}`}
              className="h-8 w-8 p-0"
            >
              −
            </Button>
            <span
              className="w-10 text-center text-lg font-bold tabular-nums text-foreground"
              aria-label={`${counter.label}: ${counter.value}`}
            >
              {counter.value}
            </span>
            <Button
              variant="outline"
              size="sm"
              onClick={() => increment(counter.id)}
              aria-label={`Increment ${counter.label}`}
              className="h-8 w-8 p-0"
            >
              +
            </Button>
          </div>

          <Button
            variant="ghost"
            size="sm"
            onClick={() => reset(counter.id)}
            aria-label={`Reset ${counter.label}`}
            className="h-8 w-8 p-0 text-muted-foreground hover:text-muted-foreground"
          >
            <RotateCcw className="h-3.5 w-3.5" />
          </Button>

          <Button
            variant="ghost"
            size="sm"
            onClick={() => remove(counter.id)}
            aria-label={`Remove ${counter.label}`}
            className="h-8 w-8 p-0 text-muted-foreground hover:text-red-500"
          >
            <Trash2 className="h-3.5 w-3.5" />
          </Button>
        </div>
      ))}

      {/* Add counter */}
      <div className="flex gap-2 border-t border-border pt-3">
        <Input
          value={newLabel}
          onChange={e => setNewLabel(e.target.value)}
          onKeyDown={e => e.key === 'Enter' && addCounter()}
          placeholder="Nome contatore…"
          className="h-8 text-sm"
          aria-label="New counter name"
        />
        <Button
          variant="outline"
          size="sm"
          onClick={addCounter}
          disabled={!newLabel.trim()}
          aria-label="Add counter"
          className="gap-1"
        >
          <Plus className="h-3.5 w-3.5" />
          Aggiungi
        </Button>
      </div>
    </div>
  );
}

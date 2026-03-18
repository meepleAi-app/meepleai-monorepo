'use client';

import React, { useState } from 'react';

import { Plus, Shuffle, Trash2, X } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';

/**
 * Randomizer — random selection from a configurable list of items.
 */
export function Randomizer() {
  const [items, setItems] = useState<string[]>([]);
  const [newItem, setNewItem] = useState('');
  const [picked, setPicked] = useState<string | null>(null);

  const addItem = () => {
    const value = newItem.trim();
    if (!value) return;
    setItems(prev => [...prev, value]);
    setNewItem('');
    setPicked(null);
  };

  const removeItem = (index: number) => {
    setItems(prev => prev.filter((_, i) => i !== index));
    setPicked(null);
  };

  const pickRandom = () => {
    if (items.length === 0) return;
    const idx = Math.floor(Math.random() * items.length);
    setPicked(items[idx]);
  };

  const clearAll = () => {
    setItems([]);
    setPicked(null);
  };

  return (
    <div className="space-y-3" data-testid="randomizer">
      {/* Add item */}
      <div className="flex gap-2">
        <Input
          value={newItem}
          onChange={e => setNewItem(e.target.value)}
          onKeyDown={e => e.key === 'Enter' && addItem()}
          placeholder="Aggiungi elemento…"
          className="h-8 text-sm"
          aria-label="New item"
        />
        <Button
          variant="outline"
          size="sm"
          onClick={addItem}
          disabled={!newItem.trim()}
          aria-label="Add item"
          className="gap-1"
        >
          <Plus className="h-3.5 w-3.5" />
          Aggiungi
        </Button>
      </div>

      {/* Item list */}
      {items.length === 0 ? (
        <p className="py-3 text-center text-sm text-slate-400">
          Nessun elemento. Aggiungine qualcuno!
        </p>
      ) : (
        <div className="space-y-1 max-h-40 overflow-y-auto rounded-lg border border-slate-200 p-2">
          {items.map((item, idx) => (
            <div
              key={idx}
              className={`flex items-center justify-between rounded-md px-3 py-1.5 text-sm transition-colors ${
                item === picked
                  ? 'border border-amber-300 bg-amber-50 font-medium text-amber-800'
                  : 'bg-slate-50 text-slate-700'
              }`}
              data-testid={`item-${idx}`}
            >
              <span className="truncate">{item}</span>
              <button
                onClick={() => removeItem(idx)}
                aria-label={`Remove ${item}`}
                className="ml-2 flex-shrink-0 text-slate-400 hover:text-red-500 transition-colors"
              >
                <X className="h-3.5 w-3.5" />
              </button>
            </div>
          ))}
        </div>
      )}

      {/* Result */}
      {picked && (
        <div
          className="rounded-lg border-2 border-amber-400 bg-amber-50 p-4 text-center"
          data-testid="randomizer-result"
          aria-live="polite"
        >
          <p className="text-xs font-medium text-amber-600 uppercase tracking-wide mb-1">
            Selezionato
          </p>
          <p className="text-xl font-bold text-amber-800">{picked}</p>
        </div>
      )}

      {/* Actions */}
      <div className="flex gap-2">
        <Button
          onClick={pickRandom}
          disabled={items.length === 0}
          className="flex-1 gap-1"
          aria-label="Pick random item"
        >
          <Shuffle className="h-4 w-4" />
          Scegli a caso
        </Button>
        <Button
          variant="outline"
          onClick={clearAll}
          disabled={items.length === 0}
          aria-label="Clear all items"
          className="gap-1"
        >
          <Trash2 className="h-3.5 w-3.5" />
          Svuota
        </Button>
      </div>
    </div>
  );
}

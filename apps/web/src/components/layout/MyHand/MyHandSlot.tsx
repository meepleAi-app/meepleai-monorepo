'use client';

import React from 'react';

import { AlertTriangle, Plus, X } from 'lucide-react';

import { cn } from '@/lib/utils';
import type { MyHandSlot as MyHandSlotData, MyHandSlotType } from '@/stores/my-hand/types';

const SLOT_LABELS: Record<MyHandSlotType, string> = {
  toolkit: 'Toolkit',
  game: 'Gioco',
  session: 'Partita',
  ai: 'AI',
};

const SLOT_ICONS: Record<MyHandSlotType, string> = {
  toolkit: '🔧',
  game: '🎮',
  session: '🎯',
  ai: '🤖',
};

interface MyHandSlotProps {
  slotType: MyHandSlotType;
  slot: MyHandSlotData;
  onAssign: (slotType: MyHandSlotType) => void;
  onClear: (slotType: MyHandSlotType) => void;
  compact?: boolean;
}

export function MyHandSlot({
  slotType,
  slot,
  onAssign,
  onClear,
  compact = false,
}: MyHandSlotProps): React.JSX.Element {
  const label = SLOT_LABELS[slotType];
  const icon = SLOT_ICONS[slotType];

  if (!slot.entityId) {
    return (
      <div
        className={cn(
          'flex flex-col items-center gap-1 rounded-lg border border-dashed border-border p-3 text-muted-foreground',
          compact && 'p-2'
        )}
      >
        <span className="text-lg">{icon}</span>
        {!compact && <span className="text-xs font-medium">{label}</span>}
        <button
          aria-label={`Seleziona ${label}`}
          onClick={() => onAssign(slotType)}
          className="mt-1 flex items-center gap-1 rounded-md bg-secondary px-2 py-1 text-xs hover:bg-secondary/80"
        >
          <Plus className="h-3 w-3" />
          {!compact && 'Seleziona'}
        </button>
      </div>
    );
  }

  if (!slot.isEntityValid) {
    return (
      <div
        className={cn(
          'flex flex-col items-center gap-1 rounded-lg border border-yellow-500/40 bg-yellow-50/10 p-3',
          compact && 'p-2'
        )}
      >
        <AlertTriangle className="h-4 w-4 text-yellow-500" />
        {!compact && (
          <span className="text-center text-xs text-muted-foreground">Non più disponibile</span>
        )}
        <button
          aria-label={`Seleziona ${label}`}
          onClick={() => onAssign(slotType)}
          className="mt-1 rounded-md bg-secondary px-2 py-1 text-xs hover:bg-secondary/80"
        >
          {compact ? <Plus className="h-3 w-3" /> : 'Seleziona nuovo'}
        </button>
      </div>
    );
  }

  return (
    <div
      className={cn(
        'group relative flex flex-col gap-1 rounded-lg border border-border bg-card p-3',
        compact && 'p-2'
      )}
    >
      <button
        aria-label={`Rimuovi ${label}`}
        onClick={() => onClear(slotType)}
        className="absolute right-1 top-1 rounded p-0.5 hover:bg-muted opacity-0 group-hover:opacity-100 focus:opacity-100"
      >
        <X className="h-3 w-3" />
      </button>
      <div className="flex items-center gap-1.5">
        <span className="text-base">{icon}</span>
        {!compact && <span className="truncate text-xs font-medium">{slot.entityLabel}</span>}
      </div>
      {compact && slot.entityLabel && (
        <span className="truncate text-[10px] text-muted-foreground">{slot.entityLabel}</span>
      )}
    </div>
  );
}

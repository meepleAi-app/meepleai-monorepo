'use client';

/**
 * DicePresetRow — Horizontal scrollable row of dice preset buttons.
 */

import { cn } from '@/lib/utils';

import type { DicePreset } from '../types';

export interface DicePresetRowProps {
  presets: DicePreset[];
  onRoll: (formula: string) => void;
  variant: 'universal' | 'ai' | 'custom';
}

const VARIANT_STYLES = {
  universal: 'border-gray-300 text-gray-700 hover:bg-gray-100',
  ai: 'border-purple-300 text-purple-700 hover:bg-purple-50',
  custom: 'border-[hsl(142,70%,45%)] text-[hsl(142,70%,35%)] hover:bg-green-50',
} as const;

export function DicePresetRow({ presets, onRoll, variant }: DicePresetRowProps) {
  if (presets.length === 0) return null;

  return (
    <div className="flex gap-2 overflow-x-auto pb-1" data-testid={`dice-preset-row-${variant}`}>
      {presets.map(preset => (
        <button
          key={preset.name}
          type="button"
          onClick={() => onRoll(preset.formula)}
          className={cn(
            'shrink-0 rounded-full border px-3 py-1 text-xs font-medium transition-colors',
            VARIANT_STYLES[variant]
          )}
          data-testid={`dice-preset-${preset.name}`}
        >
          {preset.icon ? `${preset.icon} ` : ''}
          {preset.name}
        </button>
      ))}
    </div>
  );
}

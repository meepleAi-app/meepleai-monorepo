'use client';

/**
 * DiaryFilters — Pill toggles for filtering diary events.
 * Multi-select: active filters are OR-combined.
 */

import React from 'react';

import { cn } from '@/lib/utils';

import type { DiaryEventType } from '../types';

export interface DiaryFilterOption {
  type: DiaryEventType;
  label: string;
  icon: string;
}

export const DIARY_FILTER_OPTIONS: DiaryFilterOption[] = [
  { type: 'dice_roll', label: 'Dadi', icon: '🎲' },
  { type: 'score_change', label: 'Punti', icon: '🏆' },
  { type: 'note_added', label: 'Note', icon: '📝' },
  { type: 'turn_change', label: 'Turni', icon: '🔄' },
  { type: 'manual_entry', label: 'Manual', icon: '✍️' },
];

export interface DiaryFiltersProps {
  activeFilters: DiaryEventType[];
  onToggle: (type: DiaryEventType) => void;
  onReset: () => void;
}

export function DiaryFilters({ activeFilters, onToggle, onReset }: DiaryFiltersProps) {
  const allActive = activeFilters.length === 0;

  return (
    <div className="flex flex-wrap gap-1.5" data-testid="diary-filters">
      <button
        type="button"
        onClick={onReset}
        className={cn(
          'rounded-full px-2.5 py-1 text-[10px] font-semibold transition-colors',
          allActive
            ? 'bg-[hsl(142,70%,45%)] text-white'
            : 'border border-gray-300 text-gray-600 hover:border-[hsl(142,70%,45%)]'
        )}
        data-testid="diary-filter-all"
      >
        Tutti
      </button>
      {DIARY_FILTER_OPTIONS.map(opt => {
        const isActive = activeFilters.includes(opt.type);
        return (
          <button
            key={opt.type}
            type="button"
            onClick={() => onToggle(opt.type)}
            className={cn(
              'flex items-center gap-1 rounded-full px-2.5 py-1 text-[10px] font-semibold transition-colors',
              isActive
                ? 'bg-[hsl(142,70%,45%)] text-white'
                : 'border border-gray-300 text-gray-600 hover:border-[hsl(142,70%,45%)]'
            )}
            data-testid={`diary-filter-${opt.type}`}
          >
            <span>{opt.icon}</span>
            <span>{opt.label}</span>
          </button>
        );
      })}
    </div>
  );
}

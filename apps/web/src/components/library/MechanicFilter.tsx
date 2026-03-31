'use client';

import { useState } from 'react';

import { MechanicIcon } from '@/components/icons/mechanics';
import { cn } from '@/lib/utils';

const MECHANIC_LABELS: Record<string, string> = {
  'engine-building': 'Engine Building',
  'area-control': 'Area Control',
  'deck-building': 'Deck Building',
  'worker-placement': 'Worker Placement',
  cooperative: 'Cooperativo',
  competitive: 'Competitivo',
  'dice-rolling': 'Dice Rolling',
  'puzzle-abstract': 'Puzzle/Abstract',
  'narrative-rpg': 'Narrativo/RPG',
  'tile-placement': 'Tile Placement',
  trading: 'Trading',
  'set-collection': 'Set Collection',
  'dungeon-crawler': 'Dungeon Crawler',
  'route-building': 'Route Building',
  'social-deduction': 'Social Deduction',
};

const MAX_VISIBLE = 5;

export interface MechanicFilterProps {
  mechanics: string[];
  selected: string[];
  onSelect: (mechanic: string) => void;
}

export function MechanicFilter({ mechanics, selected, onSelect }: MechanicFilterProps) {
  const [expanded, setExpanded] = useState(false);
  const hasMore = mechanics.length > MAX_VISIBLE;
  const visibleMechanics = expanded ? mechanics : mechanics.slice(0, MAX_VISIBLE);
  const hiddenCount = mechanics.length - MAX_VISIBLE;

  return (
    <div className="flex flex-wrap gap-2">
      {visibleMechanics.map(mechanic => {
        const isSelected = selected.includes(mechanic);
        return (
          <button
            key={mechanic}
            type="button"
            aria-pressed={isSelected}
            onClick={() => onSelect(mechanic)}
            className={cn(
              'flex items-center gap-1.5 rounded-full px-3 py-1.5 text-xs font-medium transition-colors',
              isSelected
                ? 'bg-[#f0a030] text-[#0d1117]'
                : 'bg-[#21262d] text-[#8b949e] hover:bg-[#30363d] hover:text-[#e6edf3]'
            )}
          >
            <MechanicIcon mechanic={mechanic} size={14} />
            {MECHANIC_LABELS[mechanic] ?? mechanic}
          </button>
        );
      })}

      {hasMore && (
        <button
          type="button"
          data-testid="mechanic-toggle"
          aria-expanded={expanded}
          aria-label={
            expanded ? 'Mostra meno meccaniche' : `Mostra ${hiddenCount} meccaniche aggiuntive`
          }
          onClick={() => setExpanded(e => !e)}
          className="flex items-center gap-1.5 rounded-full px-3 py-1.5 text-xs font-medium bg-[#21262d] text-[#8b949e] hover:bg-[#30363d] hover:text-[#e6edf3] transition-colors border border-dashed border-[#30363d]"
        >
          {expanded ? '↑ Meno' : `+${hiddenCount} altri`}
        </button>
      )}
    </div>
  );
}

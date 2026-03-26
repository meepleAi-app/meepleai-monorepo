'use client';

import { cn } from '@/lib/utils';

interface FilterChip {
  id: string;
  label: string;
}

interface FilterChipsRowProps {
  chips: FilterChip[];
  activeId: string;
  onSelect: (id: string) => void;
  className?: string;
}

export function FilterChipsRow({ chips, activeId, onSelect, className }: FilterChipsRowProps) {
  return (
    <div
      className={cn('flex gap-1.5 overflow-x-auto scrollbar-none pb-1', className)}
      role="tablist"
    >
      {chips.map(chip => (
        <button
          key={chip.id}
          type="button"
          role="tab"
          aria-selected={chip.id === activeId}
          className={cn(
            'shrink-0 px-3 py-1 rounded-[var(--radius)]',
            'text-xs font-semibold border transition-colors',
            chip.id === activeId
              ? 'bg-primary text-white border-primary'
              : 'bg-card text-muted-foreground border-border hover:bg-muted/50'
          )}
          onClick={() => onSelect(chip.id)}
        >
          {chip.label}
        </button>
      ))}
    </div>
  );
}

'use client';

import { LayoutGrid, List } from 'lucide-react';

import { cn } from '@/lib/utils';

interface ViewToggleProps {
  view: 'grid' | 'list';
  onViewChange: (view: 'grid' | 'list') => void;
  className?: string;
}

export function ViewToggle({ view, onViewChange, className }: ViewToggleProps) {
  return (
    <div className={cn('flex gap-1 p-0.5 bg-muted rounded-lg', className)}>
      <button
        type="button"
        className={cn(
          'p-1.5 rounded-md transition-colors',
          view === 'grid'
            ? 'bg-card shadow-sm text-foreground'
            : 'text-muted-foreground hover:text-foreground'
        )}
        onClick={() => onViewChange('grid')}
        aria-label="Vista griglia"
        aria-pressed={view === 'grid'}
      >
        <LayoutGrid className="w-4 h-4" />
      </button>
      <button
        type="button"
        className={cn(
          'p-1.5 rounded-md transition-colors',
          view === 'list'
            ? 'bg-card shadow-sm text-foreground'
            : 'text-muted-foreground hover:text-foreground'
        )}
        onClick={() => onViewChange('list')}
        aria-label="Vista lista"
        aria-pressed={view === 'list'}
      >
        <List className="w-4 h-4" />
      </button>
    </div>
  );
}

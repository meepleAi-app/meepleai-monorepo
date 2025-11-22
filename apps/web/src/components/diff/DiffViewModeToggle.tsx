import React from 'react';
import { List, Columns2 } from 'lucide-react';
import { ToggleGroup, ToggleGroupItem } from '@/components/ui/toggle-group';

export interface DiffViewModeToggleProps {
  currentMode: 'list' | 'side-by-side';
  onModeChange: (mode: 'list' | 'side-by-side') => void;
}

/**
 * Toggle between list and side-by-side diff views
 * Migrated to shadcn UI components (ToggleGroup)
 */
export function DiffViewModeToggle({ currentMode, onModeChange }: DiffViewModeToggleProps) {
  return (
    <ToggleGroup
      type="single"
      value={currentMode}
      onValueChange={(value) => {
        if (value) onModeChange(value as 'list' | 'side-by-side');
      }}
      className="diff-view-mode-toggle"
      aria-label="Diff view mode"
    >
      <ToggleGroupItem
        value="list"
        aria-label="List view"
        className="gap-1.5"
      >
        <List className="h-4 w-4" />
        List
      </ToggleGroupItem>
      <ToggleGroupItem
        value="side-by-side"
        aria-label="Side-by-side view"
        className="gap-1.5"
      >
        <Columns2 className="h-4 w-4" />
        Side-by-Side
      </ToggleGroupItem>
    </ToggleGroup>
  );
}

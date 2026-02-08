/**
 * BulkSelectCheckbox - Multi-Selection Feature for MeepleCard
 * Issue #3829
 */

'use client';

import { Checkbox } from '@/components/ui/primitives/checkbox';
import { cn } from '@/lib/utils';

export interface BulkSelectCheckboxProps {
  selectable: boolean;
  selected: boolean;
  onSelect: (id: string, selected: boolean) => void;
  id: string;
  entityColor: string;
  size?: 'sm' | 'md';
  className?: string;
}

export function BulkSelectCheckbox({
  selectable,
  selected,
  onSelect,
  id,
  entityColor,
  size = 'md',
  className,
}: BulkSelectCheckboxProps) {
  if (!selectable) return null;

  const handleChange = (checked: boolean | 'indeterminate') => {
    if (checked === 'indeterminate') return;
    onSelect(id, checked);
  };

  const handleClick = (e: React.MouseEvent) => {
    e.stopPropagation();
  };

  return (
    <div
      className={cn(
        'absolute top-2 left-2 z-20',
        'rounded-md p-1',
        'bg-background/90 backdrop-blur-sm',
        'border border-border/50',
        className
      )}
      onClick={handleClick}
      data-testid="bulk-select-container"
    >
      <Checkbox
        checked={selected}
        onCheckedChange={handleChange}
        aria-label={`Select ${id}`}
        className={cn(
          size === 'sm' ? 'h-4 w-4' : 'h-5 w-5',
          selected && 'border-transparent'
        )}
        style={
          selected
            ? { backgroundColor: `hsl(${entityColor})` }
            : undefined
        }
        data-testid="bulk-select-checkbox"
      />
    </div>
  );
}

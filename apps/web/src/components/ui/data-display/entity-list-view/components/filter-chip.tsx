/**
 * FilterChip - Active filter display chip with remove button
 */

'use client';

import React from 'react';
import { X } from 'lucide-react';
import { cn } from '@/lib/utils';

export interface FilterChipProps {
  label: string;
  value: string;
  onRemove: () => void;
  className?: string;
}

export const FilterChip = React.memo(function FilterChip({
  label,
  value,
  onRemove,
  className,
}: FilterChipProps) {
  return (
    <div
      className={cn(
        'inline-flex items-center gap-1.5',
        'px-2.5 py-1 rounded-md',
        'bg-primary/10 text-primary border border-primary/20',
        'text-xs font-medium',
        className
      )}
    >
      <span>
        {label}: {value}
      </span>
      <button
        type="button"
        onClick={onRemove}
        className="hover:bg-primary/20 rounded-sm p-0.5 transition-colors"
        aria-label={`Remove ${label} filter`}
      >
        <X className="w-3 h-3" />
      </button>
    </div>
  );
});

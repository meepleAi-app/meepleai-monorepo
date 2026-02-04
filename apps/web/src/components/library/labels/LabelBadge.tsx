/**
 * LabelBadge Component (Issue #3514)
 *
 * Displays a single label as a badge with optional remove button.
 * Supports predefined (secondary) and custom (outline) variants.
 */

'use client';

import { X } from 'lucide-react';

import { cn } from '@/lib/utils';
import type { LabelDto } from '@/lib/api/schemas/library.schemas';

export interface LabelBadgeProps {
  label: LabelDto;
  onRemove?: (labelId: string) => void;
  disabled?: boolean;
  className?: string;
}

/**
 * Get contrasting text color based on background color brightness
 */
function getContrastColor(hexColor: string): string {
  // Convert hex to RGB
  const hex = hexColor.replace('#', '');
  const r = parseInt(hex.substring(0, 2), 16);
  const g = parseInt(hex.substring(2, 4), 16);
  const b = parseInt(hex.substring(4, 6), 16);

  // Calculate relative luminance
  const luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255;

  return luminance > 0.5 ? '#1a1a1a' : '#ffffff';
}

export function LabelBadge({ label, onRemove, disabled = false, className }: LabelBadgeProps) {
  const bgColor = `${label.color}20`; // 20% opacity for background
  const borderColor = `${label.color}40`; // 40% opacity for border
  const textColor = label.color;

  return (
    <span
      className={cn(
        'inline-flex items-center gap-1 rounded-full border px-2.5 py-0.5 font-nunito text-xs font-medium transition-all',
        label.isPredefined
          ? 'bg-opacity-20' // Predefined: more subtle
          : 'bg-opacity-10', // Custom: even more subtle
        disabled && 'opacity-50',
        className
      )}
      style={{
        backgroundColor: bgColor,
        borderColor: borderColor,
        color: textColor,
      }}
    >
      {label.name}
      {onRemove && !disabled && (
        <button
          type="button"
          onClick={(e) => {
            e.stopPropagation();
            onRemove(label.id);
          }}
          className="ml-0.5 rounded-full p-0.5 transition-colors hover:bg-black/10"
          aria-label={`Rimuovi etichetta ${label.name}`}
        >
          <X className="h-3 w-3" />
        </button>
      )}
    </span>
  );
}

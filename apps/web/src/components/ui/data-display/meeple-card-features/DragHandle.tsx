/**
 * DragHandle - Drag & Drop Feature for MeepleCard
 * Issue #3828
 *
 * NOTE: Requires @dnd-kit/core, @dnd-kit/sortable, @dnd-kit/utilities
 * Install: pnpm add @dnd-kit/core @dnd-kit/sortable @dnd-kit/utilities
 */

'use client';

import { GripVertical } from 'lucide-react';
import { cn } from '@/lib/utils';

export interface DragData {
  id: string;
  type: 'game' | 'collection';
  index: number;
}

export interface DragHandleProps {
  draggable: boolean;
  dragData: DragData;
  onDragStart?: (data: DragData) => void;
  onDragEnd?: (data: { from: number; to: number }) => void;
  size?: 'sm' | 'md';
  className?: string;
}

/**
 * DragHandle component
 *
 * PLACEHOLDER IMPLEMENTATION - Full dnd-kit integration in Issue #3830
 * This provides the UI only. Parent component must wrap with DndContext.
 */
export function DragHandle({
  draggable,
  dragData,
  onDragStart,
  onDragEnd: _onDragEnd,
  size = 'md',
  className,
}: DragHandleProps) {
  if (!draggable) return null;

  const handleDragStart = (e: React.DragEvent) => {
    e.stopPropagation();
    onDragStart?.(dragData);
  };

  return (
    <button
      draggable
      onDragStart={handleDragStart}
      className={cn(
        'cursor-grab active:cursor-grabbing',
        'p-1 rounded hover:bg-muted/50',
        'touch-none',
        className
      )}
      aria-label={`Drag handle for ${dragData.type}`}
      data-testid="drag-handle"
    >
      <GripVertical className={cn(size === 'sm' ? 'w-4 h-4' : 'w-5 h-5', 'text-muted-foreground')} />
    </button>
  );
}

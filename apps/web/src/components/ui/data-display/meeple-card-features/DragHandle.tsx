/**
 * DragHandle - Drag & Drop Feature for MeepleCard
 * Issue #3828
 * Issue #4620 - Mobile: touch-compatible with visible grip handle
 *
 * NOTE: Requires @dnd-kit/core, @dnd-kit/sortable, @dnd-kit/utilities
 * Install: pnpm add @dnd-kit/core @dnd-kit/sortable @dnd-kit/utilities
 */

'use client';

import * as React from 'react';

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
 * Desktop: native HTML drag (mouse).
 * Mobile: touch-hold activates drag via touch events.
 * PLACEHOLDER IMPLEMENTATION - Full dnd-kit integration in Issue #3830
 */
export function DragHandle({
  draggable,
  dragData,
  onDragStart,
  onDragEnd: _onDragEnd,
  size = 'md',
  className,
}: DragHandleProps) {
  const longPressTimer = React.useRef<ReturnType<typeof setTimeout> | null>(null);
  const [isTouchDragging, setIsTouchDragging] = React.useState(false);

  if (!draggable) return null;

  const handleDragStart = (e: React.DragEvent) => {
    e.stopPropagation();
    onDragStart?.(dragData);
  };

  // Touch events for mobile drag
  const handleTouchStart = (e: React.TouchEvent) => {
    e.stopPropagation();
    // Long-press (300ms) to activate drag mode
    longPressTimer.current = setTimeout(() => {
      setIsTouchDragging(true);
      onDragStart?.(dragData);
    }, 300);
  };

  const handleTouchEnd = () => {
    if (longPressTimer.current) {
      clearTimeout(longPressTimer.current);
      longPressTimer.current = null;
    }
    setIsTouchDragging(false);
  };

  const handleTouchCancel = () => {
    if (longPressTimer.current) {
      clearTimeout(longPressTimer.current);
      longPressTimer.current = null;
    }
    setIsTouchDragging(false);
  };

  return (
    <button
      draggable
      onDragStart={handleDragStart}
      onTouchStart={handleTouchStart}
      onTouchEnd={handleTouchEnd}
      onTouchCancel={handleTouchCancel}
      className={cn(
        'cursor-grab active:cursor-grabbing',
        // Mobile: 44px touch target, always visible
        'w-11 h-11 md:w-auto md:h-auto',
        'flex items-center justify-center',
        'p-1 rounded hover:bg-muted/50',
        // Mobile: no touch-none (enable touch), Desktop: touch-none for native drag
        'md:touch-none',
        // Visual feedback when touch-dragging
        isTouchDragging && 'bg-muted/70 scale-110',
        'transition-transform duration-150',
        className
      )}
      aria-label={`Drag handle for ${dragData.type}`}
      data-testid="drag-handle"
    >
      <GripVertical className={cn(size === 'sm' ? 'w-4 h-4' : 'w-5 h-5', 'text-muted-foreground')} />
    </button>
  );
}

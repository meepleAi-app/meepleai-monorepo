'use client';

interface DragHandleProps {
  onDragStart?: () => void;
  onDragEnd?: () => void;
}

export function DragHandle({ onDragStart, onDragEnd }: DragHandleProps) {
  return (
    <div
      className="absolute left-1 top-1/2 z-20 flex -translate-y-1/2 cursor-grab items-center justify-center opacity-0 transition-opacity group-hover:opacity-60 active:cursor-grabbing"
      draggable
      onDragStart={onDragStart}
      onDragEnd={onDragEnd}
    >
      <span className="text-[10px] text-[var(--mc-text-muted)]">⠿</span>
    </div>
  );
}

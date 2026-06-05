/**
 * RankingEditor — drag-and-drop reorder of players to assign ranks 1..N.
 *
 * Strategy: server-side `RankingScoringStrategy`. Position 1 = winner.
 * Positions are derived from list order, so distinct/no-gaps invariants are
 * preserved by construction.
 *
 * Uses `@dnd-kit/sortable` (already a dependency) for both pointer and
 * keyboard accessibility. Visual drag interactions are not exercised in the
 * unit suite (jsdom does not implement pointer capture); see the matching E2E
 * harness for end-to-end keyboard / pointer drag coverage.
 *
 * Asse D follow-up P1 (#1899) T3.
 *
 * @module components/sessions/score-strategies/RankingEditor
 */

'use client';

import { useEffect, useState } from 'react';

import {
  DndContext,
  KeyboardSensor,
  PointerSensor,
  type DragEndEvent,
  useSensor,
  useSensors,
} from '@dnd-kit/core';
import {
  arrayMove,
  SortableContext,
  sortableKeyboardCoordinates,
  useSortable,
  verticalListSortingStrategy,
} from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';

import type { PlayerOption, RankingScoreData } from './types';

export interface RankingEditorProps {
  players: readonly PlayerOption[];
  initialData?: RankingScoreData;
  onChange: (data: RankingScoreData) => void;
  disabled?: boolean;
}

function initialOrder(players: readonly PlayerOption[], initialData?: RankingScoreData): string[] {
  if (initialData?.positions && initialData.positions.length > 0) {
    return [...initialData.positions].sort((a, b) => a.position - b.position).map(p => p.playerId);
  }
  return players.map(p => p.id);
}

interface RankableItemProps {
  id: string;
  displayName: string;
  position: number;
  disabled?: boolean;
}

function RankableItem({ id, displayName, position, disabled }: RankableItemProps) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({
    id,
    disabled,
  });

  return (
    <li
      ref={setNodeRef}
      style={{
        transform: CSS.Transform.toString(transform),
        transition,
        opacity: isDragging ? 0.5 : 1,
      }}
      className="flex items-center gap-3 rounded-md border border-border bg-card p-3"
      data-testid={`ranking-item-${id}`}
    >
      <span
        className="w-8 font-mono text-lg font-bold tabular-nums"
        data-testid={`ranking-position-${id}`}
      >
        {position}
      </span>
      <span className="flex-1 font-medium">{displayName}</span>
      <button
        type="button"
        {...attributes}
        {...listeners}
        disabled={disabled}
        aria-label={`Trascina ${displayName}`}
        data-testid={`ranking-handle-${id}`}
        className="cursor-grab disabled:cursor-not-allowed"
      >
        ⋮⋮
      </button>
    </li>
  );
}

export function RankingEditor({ players, initialData, onChange, disabled }: RankingEditorProps) {
  const [ids, setIds] = useState<string[]>(() => initialOrder(players, initialData));

  useEffect(() => {
    onChange({
      positions: ids.map((id, idx) => ({ playerId: id, position: idx + 1 })),
    });
  }, [ids, onChange]);

  const sensors = useSensors(
    useSensor(PointerSensor),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates })
  );

  const handleDragEnd = (event: DragEndEvent) => {
    const { active, over } = event;
    if (!over || active.id === over.id) return;
    setIds(prev => {
      const oldIndex = prev.indexOf(active.id as string);
      const newIndex = prev.indexOf(over.id as string);
      if (oldIndex === -1 || newIndex === -1) return prev;
      return arrayMove(prev, oldIndex, newIndex);
    });
  };

  const playerMap = new Map(players.map(p => [p.id, p]));

  return (
    <DndContext sensors={sensors} onDragEnd={handleDragEnd}>
      <SortableContext items={ids} strategy={verticalListSortingStrategy}>
        <ul className="space-y-2" data-testid="ranking-editor">
          {ids.map((id, idx) => {
            const player = playerMap.get(id);
            if (!player) return null;
            return (
              <RankableItem
                key={id}
                id={id}
                displayName={player.displayName}
                position={idx + 1}
                disabled={disabled}
              />
            );
          })}
        </ul>
      </SortableContext>
    </DndContext>
  );
}

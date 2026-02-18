'use client';

import { GripVerticalIcon, PencilIcon, Trash2Icon } from 'lucide-react';

import { Button } from '@/components/ui/button';

export interface CategoryRowProps {
  name: string;
  emoji: string;
  gameCount: number;
  color: string;
  onEdit?: () => void;
  onDelete?: () => void;
}

export function CategoryRow({
  name,
  emoji,
  gameCount,
  color,
  onEdit,
  onDelete,
}: CategoryRowProps) {
  return (
    <tr className="hover:bg-amber-50/30 dark:hover:bg-zinc-900/50 transition-colors">
      {/* Drag Handle */}
      <td className="px-6 py-4">
        <GripVerticalIcon className="w-5 h-5 text-gray-400 dark:text-zinc-600 cursor-move opacity-40 hover:opacity-100 transition-opacity" />
      </td>

      {/* Category Name */}
      <td className="px-6 py-4">
        <div className="flex items-center gap-3">
          <div className="text-2xl">{emoji}</div>
          <span className="text-sm font-medium text-gray-900 dark:text-zinc-100">{name}</span>
        </div>
      </td>

      {/* Game Count */}
      <td className="px-6 py-4">
        <span className="text-sm text-gray-700 dark:text-zinc-300">{gameCount} games</span>
      </td>

      {/* Color */}
      <td className="px-6 py-4">
        <div className="flex items-center gap-2">
          <div
            className="w-6 h-6 rounded-full border-2 border-gray-300 dark:border-zinc-600"
            style={{ backgroundColor: color }}
          />
          <span className="text-xs font-mono text-gray-600 dark:text-zinc-400">{color}</span>
        </div>
      </td>

      {/* Actions */}
      <td className="px-6 py-4">
        <div className="flex items-center gap-2">
          <Button
            variant="ghost"
            size="sm"
            onClick={onEdit}
            className="h-8 w-8 p-0"
          >
            <PencilIcon className="h-4 w-4" />
            <span className="sr-only">Edit {name}</span>
          </Button>
          <Button
            variant="ghost"
            size="sm"
            onClick={onDelete}
            className="h-8 w-8 p-0 text-red-600 hover:text-red-700 hover:bg-red-50 dark:text-red-400 dark:hover:bg-red-900/20"
          >
            <Trash2Icon className="h-4 w-4" />
            <span className="sr-only">Delete {name}</span>
          </Button>
        </div>
      </td>
    </tr>
  );
}

'use client';

import { PlusIcon } from 'lucide-react';

import { Button } from '@/components/ui/button';

import { CategoryRow } from './category-row';

interface Category {
  id: string;
  name: string;
  emoji: string;
  gameCount: number;
  color: string;
}

const MOCK_CATEGORIES: Category[] = [
  { id: '1', name: 'Strategy', emoji: '♟️', gameCount: 42, color: '#3b82f6' },
  { id: '2', name: 'Party', emoji: '🎉', gameCount: 28, color: '#ec4899' },
  { id: '3', name: 'Cooperative', emoji: '🤝', gameCount: 19, color: '#10b981' },
  { id: '4', name: 'Deck Building', emoji: '🃏', gameCount: 15, color: '#8b5cf6' },
  { id: '5', name: 'Family', emoji: '👨\u200d👩\u200d👧\u200d👦', gameCount: 34, color: '#f59e0b' },
  { id: '6', name: 'Abstract', emoji: '🔷', gameCount: 12, color: '#06b6d4' },
  { id: '7', name: 'Thematic', emoji: '🗺️', gameCount: 23, color: '#ef4444' },
  { id: '8', name: 'Euro', emoji: '🏛️', gameCount: 31, color: '#6366f1' },
];

export function CategoriesTable() {
  const handleEdit = (id: string) => {
    // TODO: Implement edit dialog
    console.log('Edit category:', id);
  };

  const handleDelete = (id: string) => {
    // TODO: Implement delete confirmation
    console.log('Delete category:', id);
  };

  const handleAddCategory = () => {
    // TODO: Implement add category dialog
    console.log('Add category');
  };

  return (
    <div className="space-y-6">
      {/* Header with Add Button */}
      <div className="flex items-center justify-between">
        <div>
          <h2 className="font-quicksand text-xl font-bold text-slate-900 dark:text-zinc-100">
            Categories
          </h2>
          <p className="text-sm text-slate-600 dark:text-zinc-400 mt-1">
            Drag to reorder, click to edit
          </p>
        </div>
        <Button onClick={handleAddCategory} className="bg-amber-500 hover:bg-amber-600 text-white">
          <PlusIcon className="h-4 w-4 mr-2" />
          Add Category
        </Button>
      </div>

      {/* Table */}
      <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border border-amber-200/50 dark:border-zinc-700/50 rounded-lg overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead className="bg-amber-100/50 dark:bg-zinc-900/50 border-b border-amber-200/50 dark:border-zinc-700/50">
              <tr>
                <th className="px-6 py-3 text-left text-xs font-medium text-gray-700 dark:text-zinc-300 uppercase tracking-wider w-12">
                  {/* Drag handle column */}
                </th>
                <th className="px-6 py-3 text-left text-xs font-medium text-gray-700 dark:text-zinc-300 uppercase tracking-wider">
                  Category Name
                </th>
                <th className="px-6 py-3 text-left text-xs font-medium text-gray-700 dark:text-zinc-300 uppercase tracking-wider">
                  Game Count
                </th>
                <th className="px-6 py-3 text-left text-xs font-medium text-gray-700 dark:text-zinc-300 uppercase tracking-wider">
                  Color
                </th>
                <th className="px-6 py-3 text-left text-xs font-medium text-gray-700 dark:text-zinc-300 uppercase tracking-wider">
                  Actions
                </th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-200/50 dark:divide-zinc-700/50">
              {MOCK_CATEGORIES.map(category => (
                <CategoryRow
                  key={category.id}
                  name={category.name}
                  emoji={category.emoji}
                  gameCount={category.gameCount}
                  color={category.color}
                  onEdit={() => handleEdit(category.id)}
                  onDelete={() => handleDelete(category.id)}
                />
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* Note about drag-to-reorder */}
      <p className="text-xs text-slate-500 dark:text-zinc-500 italic">
        Note: Drag-to-reorder functionality will be added in a future update
      </p>
    </div>
  );
}

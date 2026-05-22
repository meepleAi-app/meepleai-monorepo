/* eslint-disable local/no-hardcoded-color-utility -- admin CRUD chrome: text-white / button color on style-prop colored bg or admin-decorative inline gradient. DS-13c admin scope (--admin-* decision deferred to DS-15). */
'use client';

import { useState } from 'react';

import { PlusIcon } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { logger } from '@/lib/logger';

import { CategoryFormDialog, type CategoryFormValue } from './category-form-dialog';
import { CategoryRow } from './category-row';
import { DeleteCategoryConfirm } from './delete-category-confirm';

interface Category {
  id: string;
  name: string;
  emoji: string;
  gameCount: number;
  color: string;
}

const INITIAL_CATEGORIES: Category[] = [
  { id: '1', name: 'Strategy', emoji: '♟️', gameCount: 42, color: '#3b82f6' },
  { id: '2', name: 'Party', emoji: '🎉', gameCount: 28, color: '#ec4899' },
  { id: '3', name: 'Cooperative', emoji: '🤝', gameCount: 19, color: '#10b981' },
  { id: '4', name: 'Deck Building', emoji: '🃏', gameCount: 15, color: '#8b5cf6' },
  { id: '5', name: 'Family', emoji: '👨‍👩‍👧‍👦', gameCount: 34, color: '#f59e0b' },
  { id: '6', name: 'Abstract', emoji: '🔷', gameCount: 12, color: '#06b6d4' },
  { id: '7', name: 'Thematic', emoji: '🗺️', gameCount: 23, color: '#ef4444' },
  { id: '8', name: 'Euro', emoji: '🏛️', gameCount: 31, color: '#6366f1' },
];

export function CategoriesTable() {
  // TODO(#1429 Phase 2): replace `INITIAL_CATEGORIES` + local state with a
  // TanStack Query hook backed by `GET /api/v1/admin/categories` once the
  // BE endpoints exist. The handlers below will swap to mutation hooks
  // (`useMutation` with optimistic updates) rather than direct setState.
  const [categories, setCategories] = useState<Category[]>(INITIAL_CATEGORIES);
  const [editing, setEditing] = useState<Category | null>(null);
  const [deleting, setDeleting] = useState<Category | null>(null);
  const [adding, setAdding] = useState(false);

  const handleEdit = (id: string) => {
    const category = categories.find(c => c.id === id);
    if (!category) {
      logger.warn(`Edit requested for unknown category id=${id}`);
      return;
    }
    setEditing(category);
  };

  const handleDelete = (id: string) => {
    const category = categories.find(c => c.id === id);
    if (!category) {
      logger.warn(`Delete requested for unknown category id=${id}`);
      return;
    }
    setDeleting(category);
  };

  const handleAddCategory = () => {
    setAdding(true);
  };

  const handleSaveAdd = (value: CategoryFormValue) => {
    // Local-state mutation only (Phase 1). Phase 2 will swap this for a
    // POST /api/v1/admin/categories mutation; the temporary id below is
    // a client-side placeholder until the server assigns one.
    const id = `local-${Date.now().toString(36)}`;
    setCategories(prev => [...prev, { ...value, gameCount: 0, id }]);
    setAdding(false);
  };

  const handleSaveEdit = (value: CategoryFormValue) => {
    if (editing === null) {
      return;
    }
    const targetId = editing.id;
    setCategories(prev => prev.map(c => (c.id === targetId ? { ...c, ...value } : c)));
    setEditing(null);
  };

  const handleConfirmDelete = () => {
    if (deleting === null) {
      return;
    }
    const targetId = deleting.id;
    setCategories(prev => prev.filter(c => c.id !== targetId));
    setDeleting(null);
  };

  return (
    <div className="space-y-6">
      {/* Header with Add Button */}
      <div className="flex items-center justify-between">
        <div>
          <h2 className="font-quicksand text-xl font-bold text-foreground dark:text-zinc-100">
            Categories
          </h2>
          <p className="text-sm text-muted-foreground dark:text-muted-foreground mt-1">
            Drag to reorder, click to edit
          </p>
        </div>
        <Button onClick={handleAddCategory} className="bg-amber-500 hover:bg-amber-600 text-white">
          <PlusIcon className="h-4 w-4 mr-2" />
          Add Category
        </Button>
      </div>

      {/* Table */}
      <div className="bg-card/70 dark:bg-zinc-800/70 backdrop-blur-md border border-amber-200/50 dark:border-zinc-700/50 rounded-lg overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead className="bg-amber-100/50 dark:bg-zinc-900/50 border-b border-amber-200/50 dark:border-zinc-700/50">
              <tr>
                <th className="px-6 py-3 text-left text-xs font-medium text-foreground dark:text-zinc-300 uppercase tracking-wider w-12">
                  {/* Drag handle column */}
                </th>
                <th className="px-6 py-3 text-left text-xs font-medium text-foreground dark:text-zinc-300 uppercase tracking-wider">
                  Category Name
                </th>
                <th className="px-6 py-3 text-left text-xs font-medium text-foreground dark:text-zinc-300 uppercase tracking-wider">
                  Game Count
                </th>
                <th className="px-6 py-3 text-left text-xs font-medium text-foreground dark:text-zinc-300 uppercase tracking-wider">
                  Color
                </th>
                <th className="px-6 py-3 text-left text-xs font-medium text-foreground dark:text-zinc-300 uppercase tracking-wider">
                  Actions
                </th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-200/50 dark:divide-zinc-700/50">
              {categories.map(category => (
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
      <p className="text-xs text-muted-foreground dark:text-muted-foreground italic">
        Note: Drag-to-reorder functionality will be added in a future update
      </p>

      <CategoryFormDialog open={adding} onClose={() => setAdding(false)} onSave={handleSaveAdd} />

      <CategoryFormDialog
        open={editing !== null}
        initial={
          editing ? { color: editing.color, emoji: editing.emoji, name: editing.name } : undefined
        }
        onClose={() => setEditing(null)}
        onSave={handleSaveEdit}
      />

      <DeleteCategoryConfirm
        open={deleting !== null}
        categoryName={deleting?.name ?? ''}
        gameCount={deleting?.gameCount ?? 0}
        onClose={() => setDeleting(null)}
        onConfirm={handleConfirmDelete}
      />
    </div>
  );
}

/* eslint-disable local/no-hardcoded-color-utility -- admin CRUD chrome: text-white / button color on style-prop colored bg or admin-decorative inline gradient. DS-13c admin scope (--admin-* decision deferred to DS-15). */
'use client';

import { useState } from 'react';

import { PlusIcon } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import {
  useAdminCategories,
  useCreateAdminCategory,
  useDeleteAdminCategory,
  useUpdateAdminCategory,
} from '@/hooks/queries/useAdminCategories';
import { AdminCategoriesApiError, type CategoryDto } from '@/lib/api/admin-categories';
import { logger } from '@/lib/logger';

import { CategoryFormDialog, type CategoryFormValue } from './category-form-dialog';
import { CategoryRow } from './category-row';
import { DeleteCategoryConfirm } from './delete-category-confirm';

const FALLBACK_EMOJI = '🎲';
const FALLBACK_COLOR = '#94a3b8';

/**
 * Derives a URL-friendly slug from the visible name (matches the server-side
 * pattern `^[a-z0-9]+(?:-[a-z0-9]+)*$` enforced by Create/UpdateGameCategoryCommandValidator).
 */
function slugify(name: string): string {
  return name
    .toLowerCase()
    .normalize('NFKD')
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '');
}

function describeError(err: unknown): string {
  if (err instanceof AdminCategoriesApiError) {
    return err.serverMessage;
  }
  if (err instanceof Error) {
    return err.message;
  }
  return 'Unknown error';
}

export function CategoriesTable() {
  const categoriesQuery = useAdminCategories();
  const createMutation = useCreateAdminCategory();
  const updateMutation = useUpdateAdminCategory();
  const deleteMutation = useDeleteAdminCategory();

  const [editing, setEditing] = useState<CategoryDto | null>(null);
  const [deleting, setDeleting] = useState<CategoryDto | null>(null);
  const [adding, setAdding] = useState(false);
  const [mutationError, setMutationError] = useState<string | null>(null);

  const categories = categoriesQuery.data ?? [];

  const handleEdit = (id: string) => {
    const category = categories.find(c => c.id === id);
    if (!category) {
      logger.warn(`Edit requested for unknown category id=${id}`);
      return;
    }
    setMutationError(null);
    setEditing(category);
  };

  const handleDelete = (id: string) => {
    const category = categories.find(c => c.id === id);
    if (!category) {
      logger.warn(`Delete requested for unknown category id=${id}`);
      return;
    }
    setMutationError(null);
    setDeleting(category);
  };

  const handleAddCategory = () => {
    setMutationError(null);
    setAdding(true);
  };

  const handleSaveAdd = async (value: CategoryFormValue) => {
    try {
      await createMutation.mutateAsync({
        name: value.name,
        slug: slugify(value.name),
        emoji: value.emoji,
        color: value.color,
      });
      setAdding(false);
    } catch (err) {
      setMutationError(describeError(err));
    }
  };

  const handleSaveEdit = async (value: CategoryFormValue) => {
    if (editing === null) {
      return;
    }
    try {
      await updateMutation.mutateAsync({
        id: editing.id,
        payload: {
          name: value.name,
          slug: editing.slug || slugify(value.name),
          emoji: value.emoji,
          color: value.color,
        },
      });
      setEditing(null);
    } catch (err) {
      setMutationError(describeError(err));
    }
  };

  const handleConfirmDelete = async () => {
    if (deleting === null) {
      return;
    }
    try {
      await deleteMutation.mutateAsync(deleting.id);
      setDeleting(null);
    } catch (err) {
      setMutationError(describeError(err));
    }
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

      {mutationError !== null && (
        <div
          role="alert"
          className="rounded-md border border-red-200 dark:border-red-800 bg-red-50/80 dark:bg-red-900/40 px-4 py-3 text-sm text-red-700 dark:text-red-200"
        >
          {mutationError}
        </div>
      )}

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
              {categoriesQuery.isLoading && (
                <tr>
                  <td
                    colSpan={5}
                    className="px-6 py-8 text-center text-sm text-muted-foreground dark:text-muted-foreground"
                  >
                    Loading categories…
                  </td>
                </tr>
              )}
              {categoriesQuery.isError && (
                <tr>
                  <td
                    colSpan={5}
                    className="px-6 py-8 text-center text-sm text-red-600 dark:text-red-400"
                    role="alert"
                  >
                    Failed to load categories: {describeError(categoriesQuery.error)}
                  </td>
                </tr>
              )}
              {!categoriesQuery.isLoading &&
                !categoriesQuery.isError &&
                categories.length === 0 && (
                  <tr>
                    <td
                      colSpan={5}
                      className="px-6 py-8 text-center text-sm text-muted-foreground dark:text-muted-foreground"
                    >
                      No categories yet. Click <em>Add Category</em> to create one.
                    </td>
                  </tr>
                )}
              {categories.map(category => (
                <CategoryRow
                  key={category.id}
                  name={category.name}
                  emoji={category.emoji ?? FALLBACK_EMOJI}
                  gameCount={category.gameCount ?? 0}
                  color={category.color ?? FALLBACK_COLOR}
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
          editing
            ? {
                color: editing.color ?? FALLBACK_COLOR,
                emoji: editing.emoji ?? FALLBACK_EMOJI,
                name: editing.name,
              }
            : undefined
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

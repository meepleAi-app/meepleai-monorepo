/* eslint-disable local/no-hardcoded-color-utility -- admin CRUD chrome: matches the existing categories-table palette (amber + zinc). DS-13c admin scope (--admin-* decision deferred to DS-15). */
'use client';

import { useEffect, useState } from 'react';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';

export interface CategoryFormValue {
  /** Hex color string, e.g. "#3b82f6". */
  readonly color: string;
  /** Single-character emoji (or short emoji sequence). */
  readonly emoji: string;
  /** Visible category name, 1..50 chars after trim. */
  readonly name: string;
}

export interface CategoryFormDialogProps {
  /** When true the dialog is open. Use a controlled-component pattern. */
  readonly open: boolean;
  /** Initial form values for edit mode; pass undefined for add mode. */
  readonly initial?: CategoryFormValue;
  /** Called when the user dismisses without submitting. */
  readonly onClose: () => void;
  /**
   * Called with the validated values on submit. Persistence is the caller's
   * responsibility (issue #1429) — callers wire `useAdminCategories` TanStack
   * mutations (create/update, shipped in PR #1440). This dialog stays
   * presentational so it can be reused outside the categories admin table.
   */
  readonly onSave: (value: CategoryFormValue) => void;
}

const DEFAULT_FORM: CategoryFormValue = {
  color: '#3b82f6',
  emoji: '🎲',
  name: '',
};

/**
 * Add / Edit category dialog (issue #1429).
 *
 * Single component handles both add and edit by checking whether `initial`
 * is provided. Client-side validation is intentionally minimal (presence +
 * length); authoritative schema validation is enforced server-side via
 * FluentValidation on the `/api/v1/admin/categories` endpoints (PR #1440).
 */
export function CategoryFormDialog({ initial, onClose, onSave, open }: CategoryFormDialogProps) {
  const isEdit = initial !== undefined;
  const [form, setForm] = useState<CategoryFormValue>(initial ?? DEFAULT_FORM);
  const [error, setError] = useState<string | null>(null);

  // Reset the form every time the dialog is reopened so a stale Edit value
  // doesn't bleed into a subsequent Add (or vice versa).
  useEffect(() => {
    if (open) {
      setForm(initial ?? DEFAULT_FORM);
      setError(null);
    }
  }, [initial, open]);

  const handleSubmit = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const trimmedName = form.name.trim();
    if (trimmedName.length === 0) {
      setError('Name is required.');
      return;
    }
    if (trimmedName.length > 50) {
      setError('Name must be 50 characters or fewer.');
      return;
    }
    if (form.emoji.trim().length === 0) {
      setError('Emoji is required.');
      return;
    }
    if (!/^#[0-9a-fA-F]{6}$/.test(form.color)) {
      setError('Color must be a 6-digit hex like #3b82f6.');
      return;
    }
    onSave({ color: form.color, emoji: form.emoji.trim(), name: trimmedName });
  };

  return (
    <Dialog open={open} onOpenChange={isOpen => !isOpen && onClose()}>
      <DialogContent>
        <form onSubmit={handleSubmit}>
          <DialogHeader>
            <DialogTitle>{isEdit ? 'Edit category' : 'Add category'}</DialogTitle>
            <DialogDescription>
              {isEdit
                ? 'Update the category metadata. Changes apply to all games tagged with this category.'
                : 'Define a new category for shared-game tagging.'}
            </DialogDescription>
          </DialogHeader>

          <div className="grid gap-4 py-4">
            <div className="grid gap-2">
              <Label htmlFor="category-name">Name</Label>
              <Input
                autoFocus
                id="category-name"
                maxLength={50}
                onChange={event => setForm(prev => ({ ...prev, name: event.target.value }))}
                placeholder="e.g. Strategy"
                value={form.name}
              />
            </div>

            <div className="grid gap-2">
              <Label htmlFor="category-emoji">Emoji</Label>
              <Input
                id="category-emoji"
                maxLength={4}
                onChange={event => setForm(prev => ({ ...prev, emoji: event.target.value }))}
                placeholder="e.g. 🎲"
                value={form.emoji}
              />
            </div>

            <div className="grid gap-2">
              <Label htmlFor="category-color">Color (hex)</Label>
              <div className="flex items-center gap-3">
                <Input
                  className="font-mono"
                  id="category-color"
                  maxLength={7}
                  onChange={event => setForm(prev => ({ ...prev, color: event.target.value }))}
                  placeholder="#3b82f6"
                  value={form.color}
                />
                <div
                  aria-hidden
                  className="h-9 w-9 rounded-full border-2 border-border dark:border-zinc-600 shrink-0"
                  style={{ backgroundColor: form.color }}
                />
              </div>
            </div>

            {error !== null && (
              <p className="text-sm text-red-600 dark:text-red-400" role="alert">
                {error}
              </p>
            )}
          </div>

          <DialogFooter>
            <Button onClick={onClose} type="button" variant="ghost">
              Cancel
            </Button>
            <Button className="bg-amber-500 hover:bg-amber-600 text-white" type="submit">
              {isEdit ? 'Save changes' : 'Add category'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

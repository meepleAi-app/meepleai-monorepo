/* eslint-disable local/no-hardcoded-color-utility -- admin CRUD chrome: matches the existing categories-table palette (red + zinc). DS-13c admin scope (--admin-* decision deferred to DS-15). */
'use client';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';

export interface DeleteCategoryConfirmProps {
  /** Category display name (shown in the warning message). */
  readonly categoryName: string;
  /** Games currently tagged with this category — drives the "warning" state. */
  readonly gameCount: number;
  /** When true the dialog is open. Controlled-component pattern. */
  readonly open: boolean;
  /** Dismiss without deleting. */
  readonly onClose: () => void;
  /** User confirmed deletion. Caller is responsible for the actual delete. */
  readonly onConfirm: () => void;
}

/**
 * Delete-category confirmation dialog (issue #1429, Phase 1 FE-only stub).
 *
 * Surfaces a warning when the category is currently tagging games so the
 * operator can't accidentally remove a popular tag. Phase 2 will pair this
 * with a BE-side cascading-delete rule (re-tag affected games to a fallback,
 * or block the delete server-side until they're untagged).
 */
export function DeleteCategoryConfirm({
  categoryName,
  gameCount,
  onClose,
  onConfirm,
  open,
}: DeleteCategoryConfirmProps) {
  const hasGames = gameCount > 0;
  return (
    <Dialog open={open} onOpenChange={isOpen => !isOpen && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Delete category</DialogTitle>
          <DialogDescription>This action cannot be undone.</DialogDescription>
        </DialogHeader>

        <div className="py-4">
          <p className="text-sm text-foreground dark:text-zinc-100">
            Are you sure you want to delete{' '}
            <span className="font-semibold">&ldquo;{categoryName}&rdquo;</span>?
          </p>

          {hasGames && (
            <p
              className="mt-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700 dark:border-red-900/40 dark:bg-red-900/20 dark:text-red-300"
              role="alert"
            >
              <strong>Warning:</strong> {gameCount} {gameCount === 1 ? 'game is' : 'games are'}{' '}
              currently tagged with this category. They will lose this tag.
            </p>
          )}
        </div>

        <DialogFooter>
          <Button onClick={onClose} type="button" variant="ghost">
            Cancel
          </Button>
          <Button
            className="bg-red-600 hover:bg-red-700 text-white"
            onClick={onConfirm}
            type="button"
          >
            Delete
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

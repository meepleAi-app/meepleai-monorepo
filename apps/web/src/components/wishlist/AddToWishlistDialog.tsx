'use client';

/**
 * AddToWishlistDialog — Unified ADD/EDIT dialog for the wishlist (Issue #3007, Task B4).
 *
 * `mode="add"` (default) creates a new wishlist entry via `useAddToWishlist`. The game
 * field is a searchable combobox over the user's library (`useLibrary`); the selected
 * game stays removable even when pre-filled via `prefillGameId`.
 *
 * `mode="edit"` updates an existing entry (`item`) via `useUpdateWishlistItem`. The game
 * is locked to a non-removable chip (a wishlist entry's game cannot change); priority,
 * target price and notes are pre-filled from `item`. Clearing a previously-set target
 * price/notes sends `clearTargetPrice`/`clearNotes` so the API nulls them out instead of
 * silently ignoring the empty value.
 *
 * Mockup: admin-mockups/design_files/sp4-library-wishlist-ui.jsx (`AddWishlistDialog`,
 * ~lines 244-406). BGG search is out of scope (user-side BGG access ban, issue #2123) —
 * the combobox only searches the user's own library.
 */

import React, { useMemo, useState } from 'react';

import { X } from 'lucide-react';

import { toast } from '@/components/layout/Toast';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { Textarea } from '@/components/ui/primitives/textarea';
import { useLibrary } from '@/hooks/queries/useLibrary';
import { useAddToWishlist, useUpdateWishlistItem } from '@/hooks/queries/useWishlist';
import { useTranslation } from '@/hooks/useTranslation';
import type { WishlistItemDto } from '@/lib/api/schemas/wishlist.schemas';
import { cn } from '@/lib/utils';

// ============================================================================
// Types & constants
// ============================================================================

type Priority = 'high' | 'medium' | 'low';

const PRIORITY_ORDER: readonly Priority[] = ['high', 'medium', 'low'];
const KNOWN_PRIORITIES: readonly Priority[] = ['high', 'medium', 'low'];
const NOTES_MAX = 200;
const NOTES_NEAR_LIMIT = NOTES_MAX - 30;

interface SelectedGame {
  id: string;
  title: string;
}

interface AddToWishlistDialogProps {
  /** 'add' (default) creates a new entry; 'edit' updates `item`. */
  mode?: 'add' | 'edit';
  /** The wishlist item being edited. Required when `mode === 'edit'`. */
  item?: WishlistItemDto;
  /** Controlled open state (when provided, dialog is externally controlled) */
  open?: boolean;
  /** Controlled open change handler */
  onOpenChange?: (open: boolean) => void;
  trigger?: React.ReactNode;
  /** Pre-selects a game in add mode. Still removable via the combobox. */
  prefillGameId?: string;
  onSuccess?: () => void;
}

// ============================================================================
// Helpers
// ============================================================================

/** Case-insensitively normalizes a raw `priority` string, defaulting to 'medium'. */
function normalizePriority(raw: string | undefined): Priority {
  const lower = (raw ?? '').toLowerCase();
  return (KNOWN_PRIORITIES as readonly string[]).includes(lower) ? (lower as Priority) : 'medium';
}

function priorityChipClasses(key: Priority, isSelected: boolean): string {
  if (!isSelected) {
    return 'border-border bg-card text-foreground hover:bg-muted';
  }
  if (key === 'high') return 'border-destructive bg-destructive/10 text-destructive';
  if (key === 'low') return 'border-muted-foreground/40 bg-muted text-muted-foreground';
  return 'border-primary bg-primary/10 text-primary';
}

// ============================================================================
// Component
// ============================================================================

export function AddToWishlistDialog({
  mode = 'add',
  item,
  open: controlledOpen,
  onOpenChange: controlledOnOpenChange,
  trigger,
  prefillGameId,
  onSuccess,
}: AddToWishlistDialogProps) {
  const { t } = useTranslation();
  const isEdit = mode === 'edit' && !!item;

  const [internalOpen, setInternalOpen] = useState(false);
  const isControlled = controlledOpen !== undefined;
  const open = isControlled ? controlledOpen : internalOpen;
  const setOpen = isControlled ? (controlledOnOpenChange ?? (() => {})) : setInternalOpen;

  const { data: libraryData } = useLibrary();
  const libraryGames = useMemo<SelectedGame[]>(
    () => (libraryData?.items ?? []).map(entry => ({ id: entry.gameId, title: entry.gameTitle })),
    [libraryData?.items]
  );

  function resolveGameTitle(gameId: string): string | undefined {
    return libraryGames.find(g => g.id === gameId)?.title;
  }

  function buildInitialGame(): SelectedGame | null {
    if (isEdit && item) {
      return {
        id: item.gameId,
        title: item.gameName ?? resolveGameTitle(item.gameId) ?? item.gameId,
      };
    }
    if (prefillGameId) {
      return { id: prefillGameId, title: resolveGameTitle(prefillGameId) ?? prefillGameId };
    }
    return null;
  }

  const [selectedGame, setSelectedGame] = useState<SelectedGame | null>(buildInitialGame);
  const [query, setQuery] = useState('');
  const [comboOpen, setComboOpen] = useState(false);
  const [priority, setPriority] = useState<Priority>(
    isEdit && item ? normalizePriority(item.priority) : 'medium'
  );
  const [targetPrice, setTargetPrice] = useState(
    isEdit && item?.targetPrice != null ? String(item.targetPrice) : ''
  );
  const [notes, setNotes] = useState(isEdit ? (item?.notes ?? '') : '');

  const { mutate: addToWishlist, isPending: isAdding } = useAddToWishlist();
  const { mutate: updateWishlistItem, isPending: isUpdating } = useUpdateWishlistItem();
  const isPending = isEdit ? isUpdating : isAdding;

  function resetForm() {
    setSelectedGame(buildInitialGame());
    setQuery('');
    setComboOpen(false);
    setPriority(isEdit && item ? normalizePriority(item.priority) : 'medium');
    setTargetPrice(isEdit && item?.targetPrice != null ? String(item.targetPrice) : '');
    setNotes(isEdit ? (item?.notes ?? '') : '');
  }

  function handleOpenChange(value: boolean) {
    setOpen(value);
    if (value) {
      resetForm();
    }
  }

  const matches = useMemo(() => {
    const q = query.trim().toLowerCase();
    const pool = q ? libraryGames.filter(g => g.title.toLowerCase().includes(q)) : libraryGames;
    return pool.slice(0, 6);
  }, [query, libraryGames]);

  const canSubmit = !!selectedGame && !!priority && !isPending;

  function pickGame(g: SelectedGame) {
    setSelectedGame(g);
    setQuery('');
    setComboOpen(false);
  }

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!selectedGame || !canSubmit) return;

    const parsedPrice = targetPrice.trim() !== '' ? parseFloat(targetPrice) : null;
    const trimmedNotes = notes.trim();

    function handleSuccess() {
      toast.success(t('pages.library.wishlist.dialog.success'));
      setOpen(false);
      onSuccess?.();
    }

    if (isEdit && item) {
      const hadPrice = item.targetPrice != null;
      const hadNotes = !!item.notes;

      updateWishlistItem(
        {
          id: item.id,
          data: {
            priority,
            ...(parsedPrice != null
              ? { targetPrice: parsedPrice }
              : hadPrice
                ? { clearTargetPrice: true as const }
                : {}),
            ...(trimmedNotes !== ''
              ? { notes: trimmedNotes }
              : hadNotes
                ? { clearNotes: true as const }
                : {}),
          },
        },
        { onSuccess: handleSuccess }
      );
      return;
    }

    addToWishlist(
      {
        gameId: selectedGame.id,
        priority,
        targetPrice: parsedPrice,
        notes: trimmedNotes !== '' ? trimmedNotes : null,
      },
      { onSuccess: handleSuccess }
    );
  }

  const editSubPriority = isEdit && item ? normalizePriority(item.priority) : priority;
  const editSubName = isEdit && item ? (item.gameName ?? selectedGame?.title ?? '') : '';

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      {!isControlled &&
        (trigger ? (
          <DialogTrigger asChild>{trigger}</DialogTrigger>
        ) : (
          <DialogTrigger asChild>
            <Button>{t('pages.library.wishlist.hero.addCta')}</Button>
          </DialogTrigger>
        ))}

      <DialogContent>
        <DialogHeader>
          <DialogTitle>
            {isEdit
              ? t('pages.library.wishlist.dialog.editTitle')
              : t('pages.library.wishlist.dialog.addTitle')}
          </DialogTitle>
          <DialogDescription>
            {isEdit
              ? t('pages.library.wishlist.dialog.editSub', {
                  name: editSubName,
                  priority: t(`pages.library.wishlist.priority.${editSubPriority}`),
                })
              : t('pages.library.wishlist.dialog.addSub')}
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={handleSubmit} className="space-y-4">
          {/* Game combobox */}
          <div className="space-y-1.5">
            <Label htmlFor="wishlist-game-combobox">
              {t('pages.library.wishlist.dialog.gameLabel')}
            </Label>
            {selectedGame ? (
              <div className="flex items-center gap-2 rounded-lg border border-border bg-muted px-3 py-2.5">
                <span className="flex-1 truncate text-sm font-medium text-foreground">
                  {selectedGame.title}
                </span>
                {!isEdit && (
                  <button
                    type="button"
                    onClick={() => setSelectedGame(null)}
                    aria-label={t('pages.library.wishlist.dialog.gameRemoveAria')}
                    className="rounded p-1 text-muted-foreground transition-colors hover:bg-card hover:text-foreground"
                  >
                    <X className="h-4 w-4" />
                  </button>
                )}
              </div>
            ) : (
              <div className="relative">
                <Input
                  id="wishlist-game-combobox"
                  role="combobox"
                  aria-expanded={comboOpen}
                  aria-autocomplete="list"
                  aria-controls="wishlist-game-listbox"
                  aria-label={t('pages.library.wishlist.dialog.gameComboAria')}
                  placeholder={t('pages.library.wishlist.dialog.gameComboPlaceholder')}
                  value={query}
                  autoComplete="off"
                  onFocus={() => setComboOpen(true)}
                  onChange={e => {
                    setQuery(e.target.value);
                    setComboOpen(true);
                  }}
                  onBlur={() => setComboOpen(false)}
                />
                {comboOpen && matches.length > 0 && (
                  <div
                    id="wishlist-game-listbox"
                    role="listbox"
                    className="absolute z-10 mt-1 max-h-60 w-full overflow-y-auto rounded-lg border border-border bg-card shadow-md"
                  >
                    {matches.map(g => (
                      <button
                        key={g.id}
                        type="button"
                        role="option"
                        aria-selected={false}
                        onMouseDown={e => e.preventDefault()}
                        onClick={() => pickGame(g)}
                        className="flex w-full items-center px-3 py-2 text-left text-sm text-foreground hover:bg-muted"
                      >
                        {g.title}
                      </button>
                    ))}
                  </div>
                )}
              </div>
            )}
          </div>

          {/* Priority radio-chips */}
          <div className="space-y-1.5">
            <Label id="wishlist-priority-label">
              {t('pages.library.wishlist.dialog.priorityLabel')}
            </Label>
            <div
              role="radiogroup"
              aria-labelledby="wishlist-priority-label"
              className="flex flex-wrap gap-2"
            >
              {PRIORITY_ORDER.map(key => {
                const isSelected = priority === key;
                return (
                  <button
                    key={key}
                    type="button"
                    role="radio"
                    aria-checked={isSelected}
                    onClick={() => setPriority(key)}
                    className={cn(
                      'rounded-full border px-3 py-1.5 text-sm font-semibold transition-colors',
                      priorityChipClasses(key, isSelected)
                    )}
                  >
                    {t(`pages.library.wishlist.priority.${key}`)}
                  </button>
                );
              })}
            </div>
          </div>

          {/* Target price (optional) */}
          <div className="space-y-1.5">
            <Label htmlFor="wishlist-target-price">
              {t('pages.library.wishlist.dialog.priceLabel')}
            </Label>
            <div className="relative">
              <span
                aria-hidden="true"
                className="pointer-events-none absolute left-3.5 top-1/2 -translate-y-1/2 text-sm text-muted-foreground"
              >
                €
              </span>
              <Input
                id="wishlist-target-price"
                type="number"
                min="0"
                max="999"
                step="0.5"
                placeholder={t('pages.library.wishlist.dialog.pricePlaceholder')}
                aria-label={t('pages.library.wishlist.dialog.priceAria')}
                value={targetPrice}
                onChange={e => setTargetPrice(e.target.value)}
                className="pl-7"
              />
            </div>
            <p className="text-xs text-muted-foreground">
              {t('pages.library.wishlist.dialog.priceHint')}
            </p>
          </div>

          {/* Notes (optional) */}
          <div className="space-y-1.5">
            <div className="flex items-center justify-between">
              <Label htmlFor="wishlist-notes">
                {t('pages.library.wishlist.dialog.notesLabel')}
              </Label>
              <span
                className={cn(
                  'text-xs',
                  notes.length > NOTES_NEAR_LIMIT ? 'text-destructive' : 'text-muted-foreground'
                )}
              >
                {t('pages.library.wishlist.dialog.notesCounter', { len: notes.length })}
              </span>
            </div>
            <Textarea
              id="wishlist-notes"
              rows={3}
              maxLength={NOTES_MAX}
              placeholder={t('pages.library.wishlist.dialog.notesPlaceholder')}
              value={notes}
              onChange={e => setNotes(e.target.value)}
            />
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setOpen(false)}>
              {t('pages.library.wishlist.dialog.cancel')}
            </Button>
            <Button type="submit" disabled={!canSubmit}>
              {isPending
                ? t('pages.library.wishlist.dialog.submitting')
                : isEdit
                  ? t('pages.library.wishlist.dialog.save')
                  : t('pages.library.wishlist.dialog.add')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

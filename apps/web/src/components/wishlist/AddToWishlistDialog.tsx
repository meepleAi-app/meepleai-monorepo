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

import React, { useMemo, useRef, useState } from 'react';

import { AlertCircle, X } from 'lucide-react';

import {
  normalizePriorityString,
  PRIORITY_ORDER,
  type Priority,
} from '@/app/(authenticated)/library/wishlist/_lib/wishlist-filters';
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

  /**
   * Add-mode default priority mirrors the mockup's "add from a specific game"
   * prefilled state: pre-selecting a game (`prefillGameId`) implies urgency, so
   * priority starts at 'high'. A blank add form (no prefill) still defaults to
   * 'medium'. Edit mode always reflects the existing item's priority.
   */
  function initialPriority(): Priority {
    if (isEdit && item) return normalizePriorityString(item.priority);
    return prefillGameId ? 'high' : 'medium';
  }

  const [selectedGame, setSelectedGame] = useState<SelectedGame | null>(buildInitialGame);
  const [query, setQuery] = useState('');
  const [comboOpen, setComboOpen] = useState(false);
  const [activeIndex, setActiveIndex] = useState(-1);
  const comboContainerRef = useRef<HTMLDivElement | null>(null);
  const [priority, setPriority] = useState<Priority>(initialPriority);
  const [targetPrice, setTargetPrice] = useState(
    isEdit && item?.targetPrice != null ? String(item.targetPrice) : ''
  );
  const [notes, setNotes] = useState(isEdit ? (item?.notes ?? '') : '');

  const {
    mutate: addToWishlist,
    isPending: isAdding,
    isError: isAddError,
    reset: resetAdd,
  } = useAddToWishlist();
  const {
    mutate: updateWishlistItem,
    isPending: isUpdating,
    isError: isUpdateError,
    reset: resetUpdate,
  } = useUpdateWishlistItem();
  const isPending = isEdit ? isUpdating : isAdding;
  const isError = isEdit ? isUpdateError : isAddError;
  const resetActiveMutation = isEdit ? resetUpdate : resetAdd;

  function resetForm() {
    setSelectedGame(buildInitialGame());
    setQuery('');
    setComboOpen(false);
    setActiveIndex(-1);
    setPriority(initialPriority());
    setTargetPrice(isEdit && item?.targetPrice != null ? String(item.targetPrice) : '');
    setNotes(isEdit ? (item?.notes ?? '') : '');
  }

  function handleOpenChange(value: boolean) {
    setOpen(value);
    if (value) {
      resetForm();
    } else {
      resetActiveMutation();
    }
  }

  /**
   * Radix's `DismissableLayer` listens for Escape on `document` with
   * `{ capture: true }` — it runs before any bubble-phase `onKeyDown` on the
   * combobox input, so `stopPropagation` there cannot stop it. The supported
   * way to scope Escape to the combobox is this `onEscapeKeyDown` prop
   * (forwarded to `DismissableLayer`): calling `preventDefault` here makes
   * Radix skip its own dismiss for this keypress, so the first Escape closes
   * only the listbox and a second Escape (listbox already closed) is left to
   * close the dialog normally.
   */
  function handleContentEscapeKeyDown(event: KeyboardEvent) {
    if (comboOpen) {
      event.preventDefault();
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
    setActiveIndex(-1);
  }

  function comboOptionId(g: SelectedGame): string {
    return `wishlist-game-option-${g.id}`;
  }

  /**
   * ARIA combobox keyboard pattern (APG): ArrowDown/ArrowUp move the active
   * descendant (opening the listbox if needed), Enter commits the active
   * match, Escape closes the listbox. `preventDefault` on Enter is required
   * so it doesn't trigger the form's implicit submission instead. Escape also
   * calls `stopPropagation` for good hygiene (scopes the keypress to this
   * input for any other bubble-phase listeners); the surrounding Radix
   * `Dialog` is kept open on this first Escape via the `onEscapeKeyDown` prop
   * on `DialogContent` below (see `handleContentEscapeKeyDown`) — Radix's own
   * Escape handling runs in the capture phase on `document`, earlier than
   * this bubble-phase handler, so `stopPropagation` here cannot reach it.
   * Once the listbox is already closed, Escape is left to propagate normally
   * so a second press closes the dialog.
   */
  function handleComboKeyDown(e: React.KeyboardEvent<HTMLInputElement>) {
    if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
      e.preventDefault();
      setComboOpen(true);
      const direction = e.key === 'ArrowDown' ? 1 : -1;
      setActiveIndex(i => {
        if (matches.length === 0) return -1;
        if (i < 0) return direction === 1 ? 0 : matches.length - 1;
        return Math.min(Math.max(i + direction, 0), matches.length - 1);
      });
      return;
    }
    if (e.key === 'Enter') {
      if (!comboOpen) return;
      e.preventDefault();
      const active = activeIndex >= 0 ? matches[activeIndex] : undefined;
      if (active) {
        pickGame(active);
      }
      return;
    }
    if (e.key === 'Escape') {
      if (!comboOpen) return;
      e.preventDefault();
      e.stopPropagation();
      setComboOpen(false);
      setActiveIndex(-1);
    }
  }

  /**
   * Closes the listbox on blur only when focus left the combobox container
   * entirely — keeps a keyboard user's Enter-select from racing a premature
   * close, and mirrors the mousedown-preventDefault used for option clicks.
   */
  function handleComboBlur(e: React.FocusEvent<HTMLInputElement>) {
    const next = e.relatedTarget as Node | null;
    if (next && comboContainerRef.current?.contains(next)) {
      return;
    }
    setComboOpen(false);
    setActiveIndex(-1);
  }

  function submitWishlist() {
    if (!selectedGame || !canSubmit) return;

    resetActiveMutation();

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

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    submitWishlist();
  }

  const editSubPriority = isEdit && item ? normalizePriorityString(item.priority) : priority;
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

      <DialogContent onEscapeKeyDown={handleContentEscapeKeyDown}>
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
              <div className="relative" ref={comboContainerRef}>
                <Input
                  id="wishlist-game-combobox"
                  role="combobox"
                  aria-expanded={comboOpen}
                  aria-autocomplete="list"
                  aria-controls="wishlist-game-listbox"
                  aria-activedescendant={
                    comboOpen && activeIndex >= 0 && matches[activeIndex]
                      ? comboOptionId(matches[activeIndex])
                      : undefined
                  }
                  aria-label={t('pages.library.wishlist.dialog.gameComboAria')}
                  placeholder={t('pages.library.wishlist.dialog.gameComboPlaceholder')}
                  value={query}
                  autoComplete="off"
                  onFocus={() => setComboOpen(true)}
                  onChange={e => {
                    setQuery(e.target.value);
                    setComboOpen(true);
                    setActiveIndex(-1);
                  }}
                  onKeyDown={handleComboKeyDown}
                  onBlur={handleComboBlur}
                />
                {comboOpen && matches.length > 0 && (
                  <div
                    id="wishlist-game-listbox"
                    role="listbox"
                    className="absolute z-10 mt-1 max-h-60 w-full overflow-y-auto rounded-lg border border-border bg-card shadow-md"
                  >
                    {matches.map((g, idx) => {
                      const isActive = idx === activeIndex;
                      return (
                        <button
                          key={g.id}
                          id={comboOptionId(g)}
                          type="button"
                          role="option"
                          aria-selected={isActive}
                          tabIndex={-1}
                          onMouseDown={e => e.preventDefault()}
                          onMouseEnter={() => setActiveIndex(idx)}
                          onClick={() => pickGame(g)}
                          className={cn(
                            'flex w-full items-center px-3 py-2 text-left text-sm text-foreground hover:bg-muted',
                            isActive && 'bg-muted'
                          )}
                        >
                          {g.title}
                        </button>
                      );
                    })}
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

          {/* Mutation error (add/update failed) */}
          {isError && (
            <div
              role="alert"
              className="flex flex-wrap items-center gap-3 rounded-lg border border-destructive/40 bg-destructive/10 px-4 py-3 text-sm text-destructive"
            >
              <AlertCircle className="h-4 w-4 shrink-0" aria-hidden="true" />
              <span className="flex-1">{t('pages.library.wishlist.dialog.error')}</span>
              <Button type="button" variant="outline" size="sm" onClick={submitWishlist}>
                {t('pages.library.wishlist.dialog.retry')}
              </Button>
            </div>
          )}

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => handleOpenChange(false)}>
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

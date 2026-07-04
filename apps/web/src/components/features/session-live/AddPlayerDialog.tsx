'use client';

/**
 * AddPlayerDialog — Issue #2505.
 *
 * Host-only dialog for adding a player (guest or registered user) to a live session.
 *
 * A11y:
 *   - role="dialog" aria-modal="true" aria-labelledby
 *   - Focus trap: Tab cycles within dialog focusables only
 *   - Escape closes the dialog (unlike EndgameDialog)
 *   - Auto-focus on first input when opened
 *   - prefers-reduced-motion respected
 *
 * Color is assigned automatically (first free from PlayerColorSchema.options).
 * No picker is shown to the user.
 *
 * Named export (orchestrator uses React.lazy with .then(m => ({ default: m.AddPlayerDialog }))).
 */

import { type ReactElement, useEffect, useRef, useCallback, useState, useId } from 'react';

import { useAddLivePlayer } from '@/hooks/mutations/useAddLivePlayer';
import { ApiError } from '@/lib/api/core/errors';
import { PlayerColorSchema, type PlayerColor } from '@/lib/api/schemas/live-sessions.schemas';
import { usePlayerSearch } from '@/lib/game-nights/hooks/usePlayerSearch';

// ─── Types ────────────────────────────────────────────────────────────────────

export interface AddPlayerDialogLabels {
  readonly dialogTitle: string;
  readonly guestTab: string;
  readonly registeredTab: string;
  readonly displayNameLabel: string;
  readonly displayNamePlaceholder: string;
  readonly searchUserPlaceholder: string;
  readonly confirmCta: string;
  readonly cancelCta: string;
  readonly errorNoColorAvailable: string;
  readonly errorDuplicateName: string;
  readonly errorColorTaken: string;
  readonly errorGeneric: string;
}

/** Minimal shape required for color-slot tracking — compatible with both LiveSessionPlayerDto and LivePlayerEntry. */
interface PlayerColorSlot {
  readonly color: PlayerColor;
}

export interface AddPlayerDialogProps {
  readonly sessionId: string;
  readonly players: readonly PlayerColorSlot[];
  readonly open: boolean;
  readonly onClose: () => void;
  readonly labels: AddPlayerDialogLabels;
}

// ─── Focus trap helper ────────────────────────────────────────────────────────

const FOCUSABLE_SELECTORS =
  'a[href], button:not([disabled]), textarea:not([disabled]), ' +
  'input:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])';

function getFocusables(container: HTMLElement): HTMLElement[] {
  return Array.from(container.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTORS)).filter(
    el => !el.closest('[aria-hidden="true"]')
  );
}

// ─── Component ────────────────────────────────────────────────────────────────

export function AddPlayerDialog({
  sessionId,
  players,
  open,
  onClose,
  labels,
}: AddPlayerDialogProps): ReactElement | null {
  const titleId = useId();
  const dialogRef = useRef<HTMLDivElement>(null);
  const firstInputRef = useRef<HTMLInputElement>(null);
  const previousFocusRef = useRef<HTMLElement | null>(null);

  const [mode, setMode] = useState<'guest' | 'registered'>('guest');
  const [displayName, setDisplayName] = useState('');
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedUserId, setSelectedUserId] = useState<string | undefined>(undefined);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const { mutate, isPending } = useAddLivePlayer(sessionId);

  const { data: searchResults = [], isFetching: isSearching } = usePlayerSearch({
    query: searchQuery,
    limit: 10,
    enabled: mode === 'registered',
  });

  // Save currently focused element; focus first input when opened
  useEffect(() => {
    if (!open) return;
    previousFocusRef.current = document.activeElement as HTMLElement;
    // Defer to allow DOM to render
    const id = setTimeout(() => {
      firstInputRef.current?.focus();
    }, 0);
    return () => {
      clearTimeout(id);
      previousFocusRef.current?.focus();
    };
  }, [open]);

  // Reset state when dialog opens
  useEffect(() => {
    if (open) {
      setMode('guest');
      setDisplayName('');
      setSearchQuery('');
      setSelectedUserId(undefined);
      setErrorMessage(null);
    }
  }, [open]);

  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent<HTMLDivElement>) => {
      if (e.key === 'Escape') {
        e.preventDefault();
        onClose();
        return;
      }

      if (e.key === 'Tab' && dialogRef.current) {
        const focusables = getFocusables(dialogRef.current);
        if (focusables.length === 0) return;

        const firstEl = focusables[0];
        const lastEl = focusables[focusables.length - 1];

        if (e.shiftKey) {
          if (document.activeElement === firstEl) {
            e.preventDefault();
            lastEl.focus();
          }
        } else {
          if (document.activeElement === lastEl) {
            e.preventDefault();
            firstEl.focus();
          }
        }
      }
    },
    [onClose]
  );

  const computeColor = useCallback(() => {
    const used = new Set(players.map(p => p.color));
    return PlayerColorSchema.options.find(c => !used.has(c));
  }, [players]);

  const handleSubmit = useCallback(
    (e: React.FormEvent) => {
      e.preventDefault();
      setErrorMessage(null);

      const trimmed = displayName.trim();
      if (!trimmed) return;

      const color = computeColor();
      if (color == null) {
        setErrorMessage(labels.errorNoColorAvailable);
        return;
      }

      mutate(
        { displayName: trimmed, color, userId: selectedUserId },
        {
          onSuccess: () => {
            onClose();
          },
          onError: err => {
            if (err instanceof ApiError && err.statusCode === 409) {
              const msg = err.message.toLowerCase();
              if (msg.includes('name')) {
                setErrorMessage(labels.errorDuplicateName);
              } else if (msg.includes('color')) {
                setErrorMessage(labels.errorColorTaken);
              } else {
                setErrorMessage(labels.errorGeneric);
              }
            } else {
              setErrorMessage(labels.errorGeneric);
            }
          },
        }
      );
    },
    [displayName, computeColor, mutate, selectedUserId, onClose, labels]
  );

  const handleSelectUser = useCallback((userId: string, name: string) => {
    setSelectedUserId(userId);
    setDisplayName(name);
    setSearchQuery('');
    setErrorMessage(null);
  }, []);

  const handleModeSwitch = useCallback((newMode: 'guest' | 'registered') => {
    setMode(newMode);
    setDisplayName('');
    setSearchQuery('');
    setSelectedUserId(undefined);
    setErrorMessage(null);
  }, []);

  if (!open) return null;

  return (
    /* Backdrop */
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-foreground/80
        motion-safe:transition-opacity motion-reduce:transition-none"
      onClick={onClose}
      aria-hidden="false"
    >
      {/* Dialog */}
      <div
        ref={dialogRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        data-slot="add-player-dialog"
        onKeyDown={handleKeyDown}
        onClick={e => e.stopPropagation()}
        className="w-full max-w-sm rounded-xl border border-border bg-card p-6 shadow-2xl
          motion-safe:animate-in motion-safe:fade-in motion-safe:duration-200
          motion-reduce:animate-none"
      >
        {/* Title */}
        <h2 id={titleId} className="mb-4 text-base font-semibold text-foreground">
          {labels.dialogTitle}
        </h2>

        {/* Mode tabs */}
        <div role="tablist" aria-label={labels.dialogTitle} className="mb-4 flex gap-2">
          <button
            type="button"
            role="tab"
            aria-selected={mode === 'guest'}
            data-slot="add-player-tab-guest"
            onClick={() => handleModeSwitch('guest')}
            className={[
              'rounded-md px-3 py-1.5 text-sm font-medium transition-colors',
              mode === 'guest'
                ? 'bg-primary text-primary-foreground'
                : 'bg-muted text-muted-foreground hover:bg-muted/80',
            ].join(' ')}
          >
            {labels.guestTab}
          </button>
          <button
            type="button"
            role="tab"
            aria-selected={mode === 'registered'}
            data-slot="add-player-tab-registered"
            onClick={() => handleModeSwitch('registered')}
            className={[
              'rounded-md px-3 py-1.5 text-sm font-medium transition-colors',
              mode === 'registered'
                ? 'bg-primary text-primary-foreground'
                : 'bg-muted text-muted-foreground hover:bg-muted/80',
            ].join(' ')}
          >
            {labels.registeredTab}
          </button>
        </div>

        {/* Form */}
        <form onSubmit={handleSubmit} noValidate>
          {mode === 'guest' ? (
            <div className="mb-4">
              <label
                htmlFor={`${titleId}-name`}
                className="mb-1 block text-sm font-medium text-foreground"
              >
                {labels.displayNameLabel}
              </label>
              <input
                ref={firstInputRef}
                id={`${titleId}-name`}
                type="text"
                value={displayName}
                onChange={e => {
                  setDisplayName(e.target.value);
                  setErrorMessage(null);
                }}
                placeholder={labels.displayNamePlaceholder}
                maxLength={100}
                required
                disabled={isPending}
                data-slot="add-player-display-name"
                className="w-full rounded-md border border-border bg-background px-3 py-2
                  text-sm text-foreground placeholder:text-muted-foreground
                  focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring
                  disabled:opacity-50"
              />
            </div>
          ) : (
            <div className="mb-4">
              {/* Search input */}
              <label
                htmlFor={`${titleId}-search`}
                className="mb-1 block text-sm font-medium text-foreground"
              >
                {labels.displayNameLabel}
              </label>
              <input
                ref={firstInputRef}
                id={`${titleId}-search`}
                type="search"
                value={searchQuery}
                onChange={e => {
                  setSearchQuery(e.target.value);
                  setSelectedUserId(undefined);
                  setDisplayName('');
                  setErrorMessage(null);
                }}
                placeholder={labels.searchUserPlaceholder}
                disabled={isPending}
                data-slot="add-player-search"
                className="w-full rounded-md border border-border bg-background px-3 py-2
                  text-sm text-foreground placeholder:text-muted-foreground
                  focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring
                  disabled:opacity-50"
              />

              {/* Selected user display */}
              {selectedUserId != null && displayName && (
                <p
                  className="mt-1 text-sm text-muted-foreground"
                  data-slot="add-player-selected-user"
                >
                  {displayName}
                </p>
              )}

              {/* Search results dropdown */}
              {searchResults.length > 0 && searchQuery.length > 0 && selectedUserId == null && (
                <ul
                  className="mt-1 flex flex-col rounded-md border border-border bg-card shadow-sm"
                  data-slot="add-player-search-results"
                >
                  {searchResults.map(user => (
                    <li key={user.id}>
                      <button
                        type="button"
                        onClick={() => handleSelectUser(user.id, user.displayName)}
                        className="flex w-full items-center justify-between px-3 py-2 text-left
                          text-sm text-foreground hover:bg-muted"
                        data-slot="add-player-search-result"
                      >
                        <span>{user.displayName}</span>
                        <span className="text-xs text-muted-foreground">{user.email}</span>
                      </button>
                    </li>
                  ))}
                </ul>
              )}

              {isSearching && (
                <p role="status" aria-live="polite" className="mt-1 text-xs text-muted-foreground">
                  …
                </p>
              )}
            </div>
          )}

          {/* Error message */}
          {errorMessage != null && (
            <p role="alert" data-slot="add-player-error" className="mb-3 text-sm text-destructive">
              {errorMessage}
            </p>
          )}

          {/* Actions */}
          <div className="flex gap-2 justify-end">
            <button
              type="button"
              onClick={onClose}
              disabled={isPending}
              data-slot="add-player-cancel"
              className="rounded-md border border-border px-4 py-2 text-sm font-medium
                text-foreground hover:bg-muted
                focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring
                disabled:opacity-50"
            >
              {labels.cancelCta}
            </button>
            <button
              type="submit"
              disabled={
                isPending ||
                (mode === 'guest'
                  ? !displayName.trim()
                  : selectedUserId == null && !displayName.trim())
              }
              data-slot="add-player-submit"
              className="rounded-md bg-primary px-4 py-2 text-sm font-medium
                text-primary-foreground hover:bg-primary/90
                focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring
                disabled:opacity-50"
            >
              {labels.confirmCta}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

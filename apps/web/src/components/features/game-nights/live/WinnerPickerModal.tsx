'use client';

import { useEffect, useState, type JSX } from 'react';

import type { WinnerCandidate } from '@/lib/game-nights/mapNightLive';

export interface WinnerPickerModalProps {
  readonly open: boolean;
  readonly candidates: readonly WinnerCandidate[];
  readonly pending?: boolean;
  /** Distinct 409/403 feedback surfaced inline (panel D6). */
  readonly errorMessage?: string | null;
  readonly onCancel: () => void;
  /** winnerId = a Participant.Id, or undefined for "no winner". */
  readonly onConfirm: (winnerId?: string) => void;
}

const NO_WINNER = '__none__';

/**
 * #2634 C4: the organizer picks the winner (or "no winner") when completing the live game.
 * Candidates come from the guarded night-live read model's roster — never the unguarded
 * GET /game-sessions/{id} (panel D4). Pending-locked while the completion mutation is in flight.
 */
export function WinnerPickerModal({
  open,
  candidates,
  pending,
  errorMessage,
  onCancel,
  onConfirm,
}: WinnerPickerModalProps): JSX.Element | null {
  const [selected, setSelected] = useState<string>(NO_WINNER);

  // Reset the choice each time the picker opens.
  useEffect(() => {
    if (open) setSelected(NO_WINNER);
  }, [open]);

  // Escape cancels (but not while a completion is in flight).
  useEffect(() => {
    if (!open) return undefined;
    const handle = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && !pending) onCancel();
    };
    document.addEventListener('keydown', handle);
    return () => document.removeEventListener('keydown', handle);
  }, [open, pending, onCancel]);

  if (!open) return null;

  const confirm = () => onConfirm(selected === NO_WINNER ? undefined : selected);

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center p-4 backdrop-blur-sm"
      style={{ background: 'rgba(0,0,0,0.45)' }}
      role="presentation"
      onClick={() => {
        if (!pending) onCancel();
      }}
    >
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="winner-picker-title"
        onClick={e => e.stopPropagation()}
        className="w-full max-w-sm rounded-xl border border-border bg-card p-5 shadow-lg"
      >
        <h2
          id="winner-picker-title"
          className="font-display text-lg font-extrabold text-foreground"
        >
          Chi ha vinto?
        </h2>
        <p className="mt-1 font-mono text-sm text-muted-foreground">
          Completa la partita scegliendo il vincitore.
        </p>

        <fieldset className="mt-3 space-y-1" disabled={pending}>
          {candidates.map(candidate => (
            <label
              key={candidate.id}
              className="flex cursor-pointer items-center gap-2 rounded-md px-2 py-1.5 hover:bg-muted"
            >
              <input
                type="radio"
                name="winner"
                value={candidate.id}
                checked={selected === candidate.id}
                onChange={() => setSelected(candidate.id)}
              />
              <span className="font-display text-sm text-foreground">{candidate.displayName}</span>
            </label>
          ))}
          <label className="flex cursor-pointer items-center gap-2 rounded-md px-2 py-1.5 hover:bg-muted">
            <input
              type="radio"
              name="winner"
              value={NO_WINNER}
              checked={selected === NO_WINNER}
              onChange={() => setSelected(NO_WINNER)}
            />
            <span className="font-display text-sm text-muted-foreground">Nessun vincitore</span>
          </label>
        </fieldset>

        {errorMessage ? (
          <p role="alert" className="mt-2 font-mono text-xs text-destructive">
            {errorMessage}
          </p>
        ) : null}

        <div className="mt-4 flex items-center justify-end gap-2">
          <button
            type="button"
            onClick={onCancel}
            disabled={pending}
            className="rounded-md border border-border bg-card px-4 py-2 font-display text-[13px] font-extrabold text-foreground hover:bg-muted disabled:cursor-not-allowed disabled:opacity-60"
          >
            Annulla
          </button>
          <button
            type="button"
            onClick={confirm}
            disabled={pending}
            className="rounded-md border border-entity-session/40 bg-entity-session px-4 py-2 font-display text-[13px] font-extrabold text-white hover:bg-entity-session/90 disabled:cursor-not-allowed disabled:opacity-60"
          >
            {pending ? 'Completamento…' : 'Completa'}
          </button>
        </div>
      </div>
    </div>
  );
}

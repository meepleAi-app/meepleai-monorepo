/**
 * GameNightDateTimePicker — Step 1 ("Quando") component.
 * Issue #950 W3 Components. Spec §5 (C2) + §12 Scenario 2 (conflict warning).
 *
 * Pure component: receives current ISO + conflict-check result + labels from
 * the orchestrator and emits intent via `onSetDate` / `onContinueAnyway`.
 */

'use client';

import { useId, type ReactElement } from 'react';

import type { ConflictResult } from '@/lib/game-nights/wizard-types';

export interface GameNightDateTimePickerLabels {
  readonly label: string;
  readonly helper: string;
  readonly conflictWarningTitle: string;
  /** Receives `{ count }` placeholder; the orchestrator formats it. */
  readonly conflictWarningBody: (count: number) => string;
  readonly conflictRoleOrganizer: string;
  readonly conflictRoleInvitee: string;
  readonly continueAnyway: string;
  readonly checking: string;
}

export interface GameNightDateTimePickerProps {
  /** Current ISO datetime (or null if unset). */
  readonly iso: string | null;
  readonly onSetDate: (iso: string) => void;
  /** Conflict check result; null while no check has run yet. */
  readonly conflictResult: ConflictResult | null;
  readonly isCheckingConflict?: boolean;
  /** Whether the user has explicitly overridden the conflict warning. */
  readonly overrideAccepted?: boolean;
  readonly onContinueAnyway?: () => void;
  readonly labels: GameNightDateTimePickerLabels;
}

/**
 * Converts an ISO string to the `YYYY-MM-DDTHH:MM` format expected by
 * `<input type="datetime-local">`. Returns `''` for null/invalid input.
 */
function isoToInputValue(iso: string | null): string {
  if (!iso) return '';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '';
  const pad = (n: number): string => n.toString().padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

export function GameNightDateTimePicker({
  iso,
  onSetDate,
  conflictResult,
  isCheckingConflict = false,
  overrideAccepted = false,
  onContinueAnyway,
  labels,
}: GameNightDateTimePickerProps): ReactElement {
  const inputId = useId();
  const value = isoToInputValue(iso);
  const showWarning = !overrideAccepted && conflictResult?.hasConflict === true;

  return (
    <section
      data-slot="game-night-create-step1"
      aria-labelledby={`${inputId}-label`}
      className="flex flex-col gap-4"
    >
      <label
        id={`${inputId}-label`}
        htmlFor={inputId}
        className="text-sm font-medium text-foreground"
      >
        {labels.label}
      </label>
      <input
        id={inputId}
        type="datetime-local"
        value={value}
        onChange={e => {
          const next = e.target.value;
          if (!next) return;
          // Convert local datetime-local back to ISO; the reducer stores the
          // canonical UTC form so conflict checks dedupe deterministically.
          onSetDate(new Date(next).toISOString());
        }}
        className="rounded-md border border-border bg-card px-3 py-2 text-foreground"
      />
      <p className="text-xs text-muted-foreground">{labels.helper}</p>

      {isCheckingConflict && (
        <p
          role="status"
          aria-live="polite"
          className="text-xs text-muted-foreground"
          data-slot="game-night-create-step1-checking"
        >
          {labels.checking}
        </p>
      )}

      {showWarning && conflictResult && (
        <div
          role="alert"
          data-slot="game-night-create-step1-conflict"
          className="rounded-md border border-warning bg-warning/10 p-3 text-sm"
        >
          <p className="font-medium text-foreground">{labels.conflictWarningTitle}</p>
          <p className="mt-1 text-muted-foreground">
            {labels.conflictWarningBody(conflictResult.conflicts.length)}
          </p>
          <ul className="mt-2 flex flex-col gap-1">
            {conflictResult.conflicts.map(c => (
              <li key={c.id} className="text-xs text-foreground">
                <span className="font-medium">{c.title}</span>
                <span className="ml-2 text-muted-foreground">
                  {c.role === 'organizer'
                    ? labels.conflictRoleOrganizer
                    : labels.conflictRoleInvitee}
                </span>
              </li>
            ))}
          </ul>
          {onContinueAnyway && (
            <button
              type="button"
              onClick={onContinueAnyway}
              className="mt-3 text-xs font-medium text-primary underline"
            >
              {labels.continueAnyway}
            </button>
          )}
        </div>
      )}
    </section>
  );
}

/**
 * PublicRsvpForm — anonymous RSVP form for /join/event/[code] (issue #1169).
 *
 * Single form that captures an optional display name + Accept / Decline action
 * for the public, token-bound game-night invitation surface. Mirrors the wire
 * shape expected by `POST /api/v1/game-nights/invitations/{token}/respond`:
 *
 *     { "response": "Accepted" | "Declined", "displayName"?: "max 120" }
 *
 * Two render modes:
 *   - `pending`       → empty form with both CTAs visible
 *   - `responded`     → "Already responded as 'Marco'" panel + change CTAs
 *
 * The mode is controlled by `currentResponse` (string) — when defined the
 * confirmation panel is rendered; when undefined the initial form is shown.
 * `initialDisplayName` seeds the input from a prior response (so changing
 * the answer keeps the persisted name).
 *
 * Pure presentational: emits `onSubmit(action, displayName?)`; the parent
 * orchestrator drives the network call, surface transitions, and i18n.
 */

'use client';

import { useId, useState, type FormEvent } from 'react';

import clsx from 'clsx';

import { Button } from '@/components/ui/primitives/button';
import type { RsvpAction } from '@/lib/api/game-night-invitations';

export interface PublicRsvpFormLabels {
  readonly sectionTitle: string;
  readonly displayNameLabel: string;
  readonly displayNamePlaceholder: string;
  readonly displayNameHelper: string;
  readonly displayNameTooLong: string;
  readonly accept: string;
  readonly decline: string;
  readonly submitting: string;
  /** Heading for the "already responded as X" confirmation panel. */
  readonly alreadyRespondedHeading: string;
  /**
   * Pre-interpolated string e.g. "You responded Accepted as 'Marco'.".
   * Caller resolves via `formatMessage`/i18n with placeholders for response +
   * name to keep the component i18n-agnostic.
   */
  readonly alreadyRespondedBody: string;
  /** CTA shown next to the confirmation panel — "Change response". */
  readonly changeResponse: string;
}

export interface PublicRsvpFormProps {
  readonly labels: PublicRsvpFormLabels;
  /**
   * Caller-derived current response. When defined the confirmation panel
   * renders instead of the form. Undefined → initial RSVP form.
   */
  readonly currentResponse: RsvpAction | undefined;
  /**
   * Seed for the display-name input when changing a prior response. May be
   * empty / null when the user previously responded anonymously.
   */
  readonly initialDisplayName?: string | null;
  /** In-flight action (Accepted/Declined) or null when idle. */
  readonly submittingAction: RsvpAction | null;
  /** When true all CTAs are disabled (terminal surfaces, error recovery). */
  readonly disabled?: boolean;
  /**
   * Emits the user's chosen action + optional trimmed display name. Parent
   * is responsible for the network call and FSM bookkeeping.
   */
  readonly onSubmit: (action: RsvpAction, displayName: string | null) => void;
  readonly className?: string;
}

export const PUBLIC_RSVP_DISPLAY_NAME_MAX_LENGTH = 120;

export function PublicRsvpForm({
  labels,
  currentResponse,
  initialDisplayName,
  submittingAction,
  disabled = false,
  onSubmit,
  className,
}: PublicRsvpFormProps): React.JSX.Element {
  const [displayName, setDisplayName] = useState<string>(initialDisplayName ?? '');
  const [touched, setTouched] = useState<boolean>(false);
  const inputId = useId();
  const helperId = `${inputId}-helper`;
  const errorId = `${inputId}-error`;

  const trimmed = displayName.trim();
  const isTooLong = trimmed.length > PUBLIC_RSVP_DISPLAY_NAME_MAX_LENGTH;
  const showError = touched && isTooLong;
  const anyPending = submittingAction !== null;
  const allDisabled = disabled || anyPending || isTooLong;
  const respondedMode = currentResponse !== undefined;

  function handleClick(action: RsvpAction) {
    return (event: FormEvent) => {
      event.preventDefault();
      setTouched(true);
      if (isTooLong) return;
      const normalized = trimmed.length > 0 ? trimmed : null;
      onSubmit(action, normalized);
    };
  }

  return (
    <section
      data-slot="public-rsvp-form"
      data-mode={respondedMode ? 'responded' : 'pending'}
      aria-label={labels.sectionTitle}
      className={clsx('rounded-lg border border-border bg-card p-4 md:p-5', className)}
    >
      {respondedMode ? (
        <div data-slot="public-rsvp-form-responded" className="flex flex-col gap-3">
          <div className="flex flex-col gap-1.5">
            <h2 className="font-display text-base font-extrabold text-foreground">
              {labels.alreadyRespondedHeading}
            </h2>
            <p className="text-sm text-muted-foreground">{labels.alreadyRespondedBody}</p>
          </div>
          <Button
            type="button"
            variant="outline"
            size="sm"
            data-slot="public-rsvp-change-cta"
            disabled={disabled}
            onClick={() => {
              // Re-mount the empty form by clearing the typed state and
              // re-opening the pending view. The parent passes
              // currentResponse=undefined to actually flip the surface; this
              // button only re-seeds the input from initialDisplayName so the
              // user does not lose their typed name across the toggle.
              setDisplayName(initialDisplayName ?? '');
              setTouched(false);
            }}
          >
            {labels.changeResponse}
          </Button>
        </div>
      ) : (
        <form
          data-slot="public-rsvp-form-pending"
          className="flex flex-col gap-4"
          onSubmit={event => event.preventDefault()}
          noValidate
        >
          <h2 className="font-display text-base font-extrabold text-foreground">
            {labels.sectionTitle}
          </h2>

          <div className="flex flex-col gap-1.5">
            <label
              htmlFor={inputId}
              className="font-display text-xs font-extrabold uppercase tracking-wide text-muted-foreground"
            >
              {labels.displayNameLabel}
            </label>
            <input
              id={inputId}
              data-slot="public-rsvp-display-name"
              type="text"
              value={displayName}
              onChange={event => setDisplayName(event.target.value)}
              onBlur={() => setTouched(true)}
              maxLength={PUBLIC_RSVP_DISPLAY_NAME_MAX_LENGTH + 1}
              placeholder={labels.displayNamePlaceholder}
              aria-describedby={showError ? errorId : helperId}
              aria-invalid={showError}
              autoComplete="name"
              disabled={disabled}
              className={clsx(
                'rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground',
                'focus:outline-none focus:ring-2 focus:ring-ring',
                showError && 'border-destructive focus:ring-destructive'
              )}
            />
            <p
              id={showError ? errorId : helperId}
              data-slot={showError ? 'public-rsvp-error' : 'public-rsvp-helper'}
              aria-live="polite"
              className={clsx('text-xs', showError ? 'text-destructive' : 'text-muted-foreground')}
            >
              {showError ? labels.displayNameTooLong : labels.displayNameHelper}
            </p>
          </div>

          <div className="flex flex-col gap-2 sm:flex-row sm:gap-3">
            <Button
              type="submit"
              data-slot="public-rsvp-accept"
              data-pending={submittingAction === 'Accepted' ? 'true' : 'false'}
              disabled={allDisabled}
              onClick={handleClick('Accepted')}
              className="flex-1 bg-success text-success-foreground hover:bg-success/90"
            >
              {submittingAction === 'Accepted' ? labels.submitting : labels.accept}
            </Button>
            <Button
              type="button"
              variant="outline"
              data-slot="public-rsvp-decline"
              data-pending={submittingAction === 'Declined' ? 'true' : 'false'}
              disabled={allDisabled}
              onClick={handleClick('Declined')}
              className="flex-1"
            >
              {submittingAction === 'Declined' ? labels.submitting : labels.decline}
            </Button>
          </div>
        </form>
      )}
    </section>
  );
}

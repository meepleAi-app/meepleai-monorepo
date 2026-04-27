/**
 * JoinForm — public Alpha waitlist form (FSM-aware).
 *
 * Wave A.2 (Issue #589). Mirrors mockup `sp3-join.jsx` lines 413-555 (`JoinForm`).
 * Spec §3.2 `JoinFormProps` + §3.5 endpoint contract + §3.4 i18n contract.
 *
 * Architecture:
 *   useWaitlistSubmit ─► state: default | submitting | success | error | already-on-list
 *                       result: { position, estimatedWeeks, alreadyOnList }
 *
 *   - `success` ⇒ render <JoinSuccessCard/>
 *   - `already-on-list` ⇒ render warning <Banner/> above the form (data preserved)
 *   - `error` ⇒ render error <Banner/> above the form (data preserved)
 *   - `submitting` ⇒ disable inputs + submit shows spinner
 *
 * GDPR Art. 7 binding (spec §3.2):
 *   - `newsletterOptIn` initial value MUST be `false` (no pre-flagged consent)
 *   - On success-reset, `newsletterOptIn` MUST stay `false` — this deliberately
 *     deviates from the mockup which resets `news: true`. The spec wins.
 *
 * Risk #4 mitigation (spec §6):
 *   - `stateOverride` allows storybook/visual tests to render any FSM branch
 *     deterministically. It is gated by `process.env.NODE_ENV !== 'production'`
 *     so a misuse in production code cannot bypass the real submit flow.
 */

'use client';

import { useState } from 'react';
import type { ChangeEvent, JSX } from 'react';

import clsx from 'clsx';

import type { JoinFormState, UseWaitlistSubmitReturn } from '@/hooks/useWaitlistSubmit';
import { GAME_OTHER_ID, isAllowedGameId } from '@/lib/join/games';
import type { GamePreference } from '@/lib/join/games';

import { GamePreferenceSelect } from './game-preference-select';
import { JoinSuccessCard } from './join-success-card';

import type { GamePreferenceSelectLabels } from './game-preference-select';
import type { JoinSuccessCardLabels } from './join-success-card';

export interface JoinFormLabels {
  readonly emailLabel: string;
  readonly emailPlaceholder: string;
  readonly emailErrorInvalid: string;
  readonly nameLabel: string;
  readonly nameOptional: string;
  readonly namePlaceholder: string;
  readonly nameHint: string;
  readonly gameErrorRequired: string;
  readonly gameErrorOtherTooShort: string;
  readonly newsletterLabel: string;
  readonly submitDefault: string;
  readonly submitting: string;
  readonly alreadyHaveInvite: string;
  readonly loginLink: string;
  /** Pre-formatted ICU string with {position} + {weeks} resolved by caller. */
  readonly bannerAlreadyOnList: string;
  readonly bannerErrorGeneric: string;
  readonly bannerErrorEmailField: string;
  readonly bannerAlreadyEmailField: string;
  /** Nested label objects forwarded to children (kept controlled at this level). */
  readonly select: GamePreferenceSelectLabels;
  readonly success: JoinSuccessCardLabels;
}

export interface JoinFormProps {
  readonly games: readonly GamePreference[];
  readonly waitlist: UseWaitlistSubmitReturn;
  readonly labels: JoinFormLabels;
  readonly loginHref: string;
  /**
   * Visual-test escape hatch — guarded by NODE_ENV. Forces the FSM to render a
   * specific branch without dispatching a real mutation. Production builds
   * silently ignore this prop.
   */
  readonly stateOverride?: JoinFormState;
  readonly className?: string;
}

interface FormData {
  readonly email: string;
  readonly name: string;
  readonly game: string;
  readonly other: string;
  readonly newsletterOptIn: boolean;
}

const INITIAL_FORM: FormData = {
  email: '',
  name: '',
  game: '',
  other: '',
  // GDPR Art. 7: explicit opt-in only — never default to true.
  newsletterOptIn: false,
};

interface ClientErrors {
  email?: string;
  game?: string;
}

function validate(form: FormData, labels: JoinFormLabels): ClientErrors {
  const errors: ClientErrors = {};
  // Minimal client-side guard — server validator is source of truth.
  if (!form.email.includes('@')) {
    errors.email = labels.emailErrorInvalid;
  }
  if (!form.game || !isAllowedGameId(form.game)) {
    errors.game = labels.gameErrorRequired;
  } else if (form.game === GAME_OTHER_ID && form.other.trim().length < 2) {
    errors.game = labels.gameErrorOtherTooShort;
  }
  return errors;
}

export function JoinForm({
  games,
  waitlist,
  labels,
  loginHref,
  stateOverride,
  className,
}: JoinFormProps): JSX.Element {
  const [form, setForm] = useState<FormData>(INITIAL_FORM);
  const [clientErrors, setClientErrors] = useState<ClientErrors>({});

  // Risk #4: gate the override behind NODE_ENV so production code paths cannot
  // be diverted by a stale prop in a deployed bundle.
  const effectiveState: JoinFormState =
    process.env.NODE_ENV !== 'production' && stateOverride ? stateOverride : waitlist.state;

  const isSubmitting = effectiveState === 'submitting';
  const showAlreadyBanner = effectiveState === 'already-on-list';
  const showErrorBanner = effectiveState === 'error';

  const setField =
    <K extends keyof FormData>(key: K) =>
    (e: ChangeEvent<HTMLInputElement>): void => {
      setForm(prev => ({ ...prev, [key]: e.target.value }));
    };

  const setGame = (id: string): void => {
    setForm(prev => ({
      ...prev,
      game: id,
      // Drop "other" free-text when the user picks a different game.
      other: id === GAME_OTHER_ID ? prev.other : '',
    }));
  };

  const setOther = (next: string): void => {
    setForm(prev => ({ ...prev, other: next }));
  };

  const handleSubmit = async (): Promise<void> => {
    const errors = validate(form, labels);
    setClientErrors(errors);
    if (Object.keys(errors).length > 0) return;

    await waitlist.submit({
      email: form.email,
      name: form.name.trim() === '' ? null : form.name.trim(),
      gamePreferenceId: form.game,
      gamePreferenceOther: form.game === GAME_OTHER_ID ? form.other.trim() : null,
      newsletterOptIn: form.newsletterOptIn,
    });
  };

  const handleResetSuccess = (): void => {
    waitlist.reset();
    // GDPR-binding: do NOT carry over `news: true` from the previous submission.
    setForm(INITIAL_FORM);
    setClientErrors({});
  };

  // ── Success branch (full-card replacement) ─────────────────────────────────
  if (effectiveState === 'success' && waitlist.result) {
    return (
      <JoinSuccessCard
        position={waitlist.result.position}
        estimatedWeeks={waitlist.result.estimatedWeeks}
        onResetClick={handleResetSuccess}
        labels={labels.success}
        className={className}
      />
    );
  }

  // For the email field, surface server-side hints in addition to client-side
  // validation. Field-level errors from a 4xx have priority; otherwise we mark
  // the field with the FSM-level hint when applicable.
  const serverEmailError = waitlist.fieldErrors?.email;
  const emailError =
    clientErrors.email ??
    serverEmailError ??
    (showErrorBanner ? labels.bannerErrorEmailField : undefined) ??
    (showAlreadyBanner ? labels.bannerAlreadyEmailField : undefined);

  return (
    <div data-slot="join-form" className={className}>
      {showAlreadyBanner && <Banner tone="warning">{labels.bannerAlreadyOnList}</Banner>}
      {showErrorBanner && <Banner tone="error">{labels.bannerErrorGeneric}</Banner>}

      <Field
        id="join-email"
        label={labels.emailLabel}
        type="email"
        placeholder={labels.emailPlaceholder}
        value={form.email}
        onChange={setField('email')}
        error={emailError}
        disabled={isSubmitting}
        required
      />

      <Field
        id="join-name"
        label={labels.nameLabel}
        optionalLabel={labels.nameOptional}
        placeholder={labels.namePlaceholder}
        value={form.name}
        onChange={setField('name')}
        hint={labels.nameHint}
        disabled={isSubmitting}
      />

      <GamePreferenceSelect
        value={form.game}
        onChange={setGame}
        otherText={form.other}
        onOtherText={setOther}
        error={clientErrors.game}
        games={games}
        labels={labels.select}
        disabled={isSubmitting}
      />

      <label className="mb-3.5 flex cursor-pointer items-start gap-2">
        <input
          type="checkbox"
          checked={form.newsletterOptIn}
          onChange={e => setForm(prev => ({ ...prev, newsletterOptIn: e.target.checked }))}
          disabled={isSubmitting}
          className={clsx(
            'mt-0.5 h-3.5 w-3.5 flex-shrink-0 cursor-pointer',
            'accent-[hsl(var(--c-game))]',
            isSubmitting && 'cursor-not-allowed opacity-65'
          )}
        />
        <span className="text-[11px] leading-[1.55] text-[hsl(var(--text-sec))]">
          {labels.newsletterLabel}
        </span>
      </label>

      <button
        type="button"
        onClick={handleSubmit}
        disabled={isSubmitting}
        className={clsx(
          'inline-flex w-full items-center justify-center gap-2',
          'px-3.5 py-2.5 rounded-md',
          'font-display text-[14px] font-bold',
          'border-0 bg-[hsl(var(--c-game))] text-white',
          'transition-all duration-150 ease-out',
          'hover:brightness-110',
          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[hsl(var(--c-game))] focus-visible:ring-offset-2',
          isSubmitting
            ? 'cursor-not-allowed opacity-65 shadow-none'
            : 'shadow-[0_4px_14px_hsl(var(--c-game)/.3)]'
        )}
      >
        {isSubmitting && <Spinner />}
        {isSubmitting ? labels.submitting : labels.submitDefault}
      </button>

      <div className="mt-3 text-center font-mono text-[11px] tracking-[0.02em] text-[hsl(var(--text-muted))]">
        {labels.alreadyHaveInvite}{' '}
        <a href={loginHref} className="font-display font-bold text-[hsl(var(--c-game))]">
          {labels.loginLink}
        </a>
      </div>
    </div>
  );
}

// ───────────────────────────────────────────────────────────────────────────
// Internal sub-primitives — kept private since they're tightly coupled to the
// mockup's layout. If we end up reusing them across surfaces, lift to v2/.
// ───────────────────────────────────────────────────────────────────────────

interface FieldProps {
  readonly id: string;
  readonly label: string;
  readonly optionalLabel?: string;
  readonly type?: string;
  readonly placeholder?: string;
  readonly value: string;
  readonly onChange: (e: ChangeEvent<HTMLInputElement>) => void;
  readonly error?: string;
  readonly hint?: string;
  readonly disabled?: boolean;
  readonly required?: boolean;
}

function Field({
  id,
  label,
  optionalLabel,
  type = 'text',
  placeholder,
  value,
  onChange,
  error,
  hint,
  disabled = false,
  required = false,
}: FieldProps): JSX.Element {
  const errorId = error ? `${id}-error` : undefined;
  const hintId = !error && hint ? `${id}-hint` : undefined;

  return (
    <div className="mb-3">
      <label
        htmlFor={id}
        className="mb-1.5 block font-display text-[10px] font-bold uppercase tracking-[0.06em] text-[hsl(var(--text-sec))]"
      >
        {label}
        {optionalLabel && (
          <span className="ml-1 font-mono text-[hsl(var(--text-muted))]">{optionalLabel}</span>
        )}
      </label>
      <input
        id={id}
        type={type}
        placeholder={placeholder}
        value={value}
        onChange={onChange}
        disabled={disabled}
        required={required}
        aria-invalid={Boolean(error)}
        aria-describedby={errorId ?? hintId}
        style={{
          borderColor: error ? 'hsl(var(--c-danger))' : undefined,
          backgroundColor: error ? 'hsl(var(--c-danger) / 0.07)' : undefined,
        }}
        className={clsx(
          'block w-full px-3 py-2.5',
          'rounded-md border-[1.5px] border-border bg-background',
          'font-body text-[13px] text-foreground',
          'outline-none transition-colors duration-150 ease-out',
          'focus-visible:ring-2 focus-visible:ring-[hsl(var(--c-game))] focus-visible:ring-offset-2',
          disabled && 'cursor-not-allowed opacity-65'
        )}
      />
      {error && (
        <div
          id={errorId}
          role="alert"
          className="mt-1 text-[11px] font-semibold text-[hsl(var(--c-danger))]"
        >
          {error}
        </div>
      )}
      {hint && !error && (
        <div id={hintId} className="mt-1 text-[11px] text-[hsl(var(--text-muted))]">
          {hint}
        </div>
      )}
    </div>
  );
}

interface BannerProps {
  readonly tone: 'warning' | 'error';
  readonly children: React.ReactNode;
}

function Banner({ tone, children }: BannerProps): JSX.Element {
  // Toggle by tone — both branches use semantic CSS vars (no hex literals).
  const colorClass =
    tone === 'error'
      ? 'bg-[hsl(var(--c-danger)/.1)] border-[hsl(var(--c-danger)/.25)] text-[hsl(var(--c-danger))]'
      : 'bg-[hsl(var(--c-warning)/.1)] border-[hsl(var(--c-warning)/.25)] text-[hsl(var(--c-warning))]';

  return (
    <div
      role={tone === 'error' ? 'alert' : 'status'}
      className={clsx(
        'mb-3 flex items-start gap-2.5',
        'px-3 py-2.5 rounded-md',
        'border text-[12px] font-semibold leading-[1.45]',
        colorClass
      )}
    >
      <span aria-hidden="true" className="flex-shrink-0 text-[14px] leading-tight">
        {tone === 'error' ? '✕' : '!'}
      </span>
      <span>{children}</span>
    </div>
  );
}

function Spinner(): JSX.Element {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" aria-hidden="true" className="animate-spin">
      <circle
        cx="12"
        cy="12"
        r="9"
        stroke="currentColor"
        strokeWidth="2.5"
        fill="none"
        opacity="0.25"
      />
      <path
        d="M21 12a9 9 0 0 0-9-9"
        stroke="currentColor"
        strokeWidth="2.5"
        strokeLinecap="round"
        fill="none"
      />
    </svg>
  );
}

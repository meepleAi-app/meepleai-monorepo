/**
 * GlossaryEditorModal — issue #952.
 *
 * Floats above the libro-game `TranslateViewer` when the user taps an inline
 * glossary pill on a translated paragraph. Locks the EN source term, lets the
 * user edit the IT translation, and dispatches an upsert via
 * `useUpsertGlossary`.
 *
 * State-04 collision (target IT already in use by another entry) is OUT OF
 * SCOPE for this PR — the backend `UpsertGlossaryEntryCommandHandler` does not
 * detect cross-entry duplicates; collision UI is tracked as a follow-up.
 *
 * Spec: `docs/superpowers/specs/2026-05-19-glossary-editor-modal-952.md`
 */

'use client';

import { useCallback, useEffect, useId, useReducer, useRef, type ReactElement } from 'react';

import { useTranslation } from '@/hooks/useTranslation';
import type { GamebookGlossaryEntry } from '@/lib/api/gamebook-glossary';
import { useUpsertGlossary } from '@/lib/gamebook/hooks/useGamebookGlossary';

// ---------------------------------------------------------------------------
// Public types
// ---------------------------------------------------------------------------

export interface GlossaryEditorModalProps {
  /** The campaign owning the glossary; required for the upsert mutation key. */
  readonly campaignId: string;
  /** The entry being edited; `null` collapses the modal to nothing. */
  readonly entry: GamebookGlossaryEntry | null;
  /** Called when the user dismisses the modal (Escape, scrim, X button). */
  readonly onClose: () => void;
  /** Called after a successful PUT — parent decides whether to also close. */
  readonly onSaved?: (saved: GamebookGlossaryEntry) => void;
  /** Force layout for visual tests; auto-derived from viewport otherwise. */
  readonly forceLayout?: 'mobile' | 'desktop';
}

// ---------------------------------------------------------------------------
// Reducer (4 transitions; see spec §6 hardened)
// ---------------------------------------------------------------------------

type ModalState = {
  itValue: string;
  initialIt: string;
  status: 'idle' | 'saving' | 'error';
  errorMessage: string | null;
};

type ModalAction =
  | { type: 'edit'; value: string }
  | { type: 'submit' }
  | { type: 'submit_ok' }
  | { type: 'submit_fail'; message: string };

function modalReducer(state: ModalState, action: ModalAction): ModalState {
  switch (action.type) {
    case 'edit':
      return { ...state, itValue: action.value, status: 'idle', errorMessage: null };
    case 'submit':
      return { ...state, status: 'saving', errorMessage: null };
    case 'submit_ok':
      return { ...state, status: 'idle', errorMessage: null };
    case 'submit_fail':
      return { ...state, status: 'error', errorMessage: action.message };
    default:
      return state;
  }
}

function makeInitialState(entry: GamebookGlossaryEntry): ModalState {
  return {
    itValue: entry.termIt,
    initialIt: entry.termIt,
    status: 'idle',
    errorMessage: null,
  };
}

// Sentinel state used when the modal is mounted with `entry === null`. The
// component returns null in that case, so this value is never read by JSX.
const EMPTY_MODAL_STATE: ModalState = {
  itValue: '',
  initialIt: '',
  status: 'idle',
  errorMessage: null,
};

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

export function GlossaryEditorModal({
  campaignId,
  entry,
  onClose,
  onSaved,
  forceLayout,
}: GlossaryEditorModalProps): ReactElement | null {
  const { t } = useTranslation();
  const upsert = useUpsertGlossary(campaignId);
  const titleId = useId();
  const inputRef = useRef<HTMLInputElement | null>(null);
  // Reducer is initialized with a sentinel "empty" state when entry is null;
  // the early-return below ensures the modal doesn't render in that case, so
  // the sentinel is never observed by the rendered tree.
  const [state, dispatch] = useReducer(
    modalReducer,
    entry,
    e => (e ? makeInitialState(e) : EMPTY_MODAL_STATE)
  );

  // Escape closes — listener attached only while the modal is mounted.
  useEffect(() => {
    if (!entry) return undefined;
    const handler = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [entry, onClose]);

  // Focus the IT input on mount — the natural starting action for the user.
  useEffect(() => {
    if (entry) inputRef.current?.focus();
  }, [entry]);

  const handleSubmit = useCallback(() => {
    if (!entry) return;
    dispatch({ type: 'submit' });
    upsert.mutate(
      { entryId: entry.id, termEn: entry.termEn, termIt: state.itValue },
      {
        onSuccess: saved => {
          dispatch({ type: 'submit_ok' });
          onSaved?.(saved);
        },
        onError: err => {
          dispatch({ type: 'submit_fail', message: err.message });
        },
      }
    );
  }, [entry, state.itValue, upsert, onSaved]);

  if (!entry) return null;

  const isDirty = state.itValue !== state.initialIt;
  // Layout: explicit prop wins; otherwise mobile is the safe default at SSR
  // time (matchMedia is jsdom-flaky and the production breakpoint is owned by
  // the parent layout via CSS). Visual tests pass `forceLayout` explicitly.
  const layout: 'mobile' | 'desktop' = forceLayout ?? 'mobile';

  return (
    <div data-slot="glossary-editor-modal">
      <div
        data-testid="glossary-editor-scrim"
        onClick={onClose}
        role="presentation"
      />
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        data-testid={
          layout === 'desktop'
            ? 'glossary-editor-dialog-desktop'
            : 'glossary-editor-dialog-mobile'
        }
      >
        <header>
          <h2 id={titleId}>{t('gamebook.glossaryEditor.title')}</h2>
          <button
            type="button"
            onClick={onClose}
            aria-label={t('gamebook.glossaryEditor.close')}
          >
            {t('gamebook.glossaryEditor.close')}
          </button>
        </header>

        <p>
          <span>{t('gamebook.glossaryEditor.termEnLabel')}: </span>
          {entry.termEn}
        </p>

        <label>
          {t('gamebook.glossaryEditor.termItLabel')}
          <input
            ref={inputRef}
            type="text"
            value={state.itValue}
            onChange={e => dispatch({ type: 'edit', value: e.target.value })}
            aria-label={t('gamebook.glossaryEditor.termItLabel')}
          />
        </label>

        {isDirty && (
          <p data-slot="glossary-editor-diff-hint">
            <span className="line-through">{state.initialIt}</span>
          </p>
        )}

        {state.status === 'error' && (
          <div role="alert" data-testid="glossary-editor-error">
            <p>{t('gamebook.glossaryEditor.error.saveFailed')}</p>
            <button type="button" onClick={handleSubmit}>
              {t('gamebook.glossaryEditor.retry')}
            </button>
          </div>
        )}

        <button
          type="button"
          disabled={!isDirty || state.status === 'saving'}
          onClick={handleSubmit}
        >
          {t('gamebook.glossaryEditor.save')}
        </button>
      </div>
    </div>
  );
}

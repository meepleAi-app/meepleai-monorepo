/**
 * GlossaryEditorModal — issue #952 + #1312.
 *
 * Floats above the libro-game `TranslateViewer` when the user taps an inline
 * glossary pill on a translated paragraph. Locks the EN source term, lets the
 * user edit the IT translation, and dispatches an upsert via
 * `useUpsertGlossary`.
 *
 * Issue #1312: state-04 collision UI shipped on top of the #952 base. When
 * the backend detects another entry on the same campaign already uses the
 * candidate IT translation, it returns 409 with `collidingEntryId` +
 * `collidingTermEn`. The modal then surfaces a `CollisionBanner` with two
 * actions: `[Overwrite]` (currently dismisses pending product decision —
 * see footnote) and `[Change translation]` (returns focus to the input).
 *
 * Spec: `docs/superpowers/specs/2026-05-19-glossary-editor-modal-952.md`
 */

'use client';

import { useCallback, useEffect, useId, useReducer, useRef, type ReactElement } from 'react';

import { useTranslation } from '@/hooks/useTranslation';
import {
  GlossaryTermCollisionError,
  type GamebookGlossaryEntry,
} from '@/lib/api/gamebook-glossary';
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
// Reducer (#1312 extends to 5 transitions; collision state added)
// ---------------------------------------------------------------------------

interface CollidingEntry {
  readonly id: string;
  readonly termEn: string;
}

type ModalState = {
  itValue: string;
  initialIt: string;
  status: 'idle' | 'saving' | 'error' | 'collision';
  errorMessage: string | null;
  collidingEntry: CollidingEntry | null;
};

type ModalAction =
  | { type: 'edit'; value: string }
  | { type: 'submit' }
  | { type: 'submit_ok' }
  | { type: 'submit_fail'; message: string }
  | { type: 'submit_collision'; collidingEntry: CollidingEntry }
  | { type: 'change_value' };

function modalReducer(state: ModalState, action: ModalAction): ModalState {
  switch (action.type) {
    case 'edit':
      return {
        ...state,
        itValue: action.value,
        status: 'idle',
        errorMessage: null,
        collidingEntry: null,
      };
    case 'submit':
      return { ...state, status: 'saving', errorMessage: null, collidingEntry: null };
    case 'submit_ok':
      return { ...state, status: 'idle', errorMessage: null, collidingEntry: null };
    case 'submit_fail':
      return { ...state, status: 'error', errorMessage: action.message, collidingEntry: null };
    case 'submit_collision':
      return {
        ...state,
        status: 'collision',
        errorMessage: null,
        collidingEntry: action.collidingEntry,
      };
    case 'change_value':
      // Issue #1312 AC-5: dismiss the collision banner WITHOUT resetting the
      // input value. Status returns to idle so the user can re-edit and resubmit.
      return { ...state, status: 'idle', errorMessage: null, collidingEntry: null };
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
    collidingEntry: null,
  };
}

// Sentinel state used when the modal is mounted with `entry === null`. The
// component returns null in that case, so this value is never read by JSX.
const EMPTY_MODAL_STATE: ModalState = {
  itValue: '',
  initialIt: '',
  status: 'idle',
  errorMessage: null,
  collidingEntry: null,
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

  // Focus the IT input on mount.
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
          // Issue #1312: 409 collision is dispatched into a dedicated state
          // so the FE can render the recovery banner instead of the generic
          // error path.
          if (err instanceof GlossaryTermCollisionError) {
            dispatch({
              type: 'submit_collision',
              collidingEntry: {
                id: err.collidingEntryId,
                termEn: err.collidingTermEn,
              },
            });
            return;
          }
          dispatch({ type: 'submit_fail', message: err.message });
        },
      }
    );
  }, [entry, state.itValue, upsert, onSaved]);

  const handleChangeTranslation = useCallback(() => {
    dispatch({ type: 'change_value' });
    // Defer focus to next tick so the state update + re-render commit first.
    queueMicrotask(() => inputRef.current?.focus());
  }, []);

  const handleOverwrite = useCallback(() => {
    // FE AC-4 (deferred): pending product decision on overwrite semantics
    // (DELETE-then-PUT vs `force: true` flag). For now, dismissing the
    // collision banner mirrors the [Change translation] flow — the user is
    // never silently misled. Tracked as a P2 follow-up.
    dispatch({ type: 'change_value' });
  }, []);

  if (!entry) return null;

  const isDirty = state.itValue !== state.initialIt;
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

        {state.status === 'collision' && state.collidingEntry !== null && (
          <div role="alert" data-testid="glossary-editor-collision">
            <h3>{t('gamebook.glossaryEditor.collision.title')}</h3>
            <p>
              {t('gamebook.glossaryEditor.collision.body', {
                termEn: state.collidingEntry.termEn,
              })}
            </p>
            <button type="button" onClick={handleOverwrite}>
              {t('gamebook.glossaryEditor.collision.overwrite')}
            </button>
            <button type="button" onClick={handleChangeTranslation}>
              {t('gamebook.glossaryEditor.collision.changeTranslation')}
            </button>
          </div>
        )}

        {/* Issue #1312 AC-6: desktop side-panel surfacing the conflicting entry. */}
        {layout === 'desktop' &&
          state.status === 'collision' &&
          state.collidingEntry !== null && (
            <aside
              data-testid="glossary-editor-collision-side-panel"
              aria-label={t('gamebook.glossaryEditor.collision.sidePanelLabel')}
            >
              <p>{state.collidingEntry.termEn}</p>
              <p>{state.itValue}</p>
            </aside>
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

/**
 * TranslateViewer.steps.ts
 *
 * UI step mapping and labels for the 4-step loading skeleton.
 * Decoupled from the internal FSM `Phase` enum to allow independent evolution.
 *
 * DEC-1 (Fowler SRP): `UiStep` type projects the internal `Phase` enum
 * onto user-facing steps (uploading, ocr, translating, glossary-check).
 * DEC-2 (Crispin testability): Glossary-check signal is `appliedTerms.length > 0`.
 */

import type { Phase } from './TranslateViewer';

/**
 * User-facing UI step names for the 4-step skeleton + labels.
 * Independent of the internal Phase FSM.
 */
export type UiStep = 'uploading' | 'ocr' | 'translating' | 'glossary-check';

/**
 * Internationalized labels (IT) for UI steps and controls.
 * Per DEC-7: Hardcoded IT, extracted into const for reuse across LoadingSkeleton, AbortButton.
 */
export const LABELS = {
  uploading: 'Caricamento foto…',
  ocr: 'Sto leggendo il libro…',
  translating: 'Sto traducendo…',
  'glossary-check': 'Cerco parole nel glossario…',
  abort: 'Annulla',
  abortAriaLabel: 'Annulla la traduzione in corso',
  timeoutError: 'Traduzione interrotta: superato il limite di 20 secondi. Riprova.',
} as const;

/**
 * Maps internal FSM Phase to user-facing UiStep.
 *
 * DEC-1: `uploading` phase → `uploading` step (no change).
 * DEC-1: `segmenting` phase → `ocr` step.
 * DEC-1: `translating` phase → either `translating` or `glossary-check` based on glossary signal.
 * DEC-2: Glossary-check is triggered when `appliedTerms.length > 0` during `translating`.
 *
 * Returns `null` for idle, segments_ready, translated phases (no skeleton shown).
 *
 * @param phase - Internal FSM phase
 * @param sse - SSE hook state containing appliedTerms and isComplete signals
 * @returns UiStep or null if no skeleton should be shown
 */
export function deriveUiStep(
  phase: Phase,
  sse: { readonly appliedTerms: readonly string[]; readonly isComplete: boolean }
): UiStep | null {
  switch (phase) {
    case 'uploading':
      return 'uploading';
    case 'segmenting':
      return 'ocr';
    case 'translating':
      // Glossary-check is shown when appliedTerms have been populated.
      // If appliedTerms is empty, we're still translating without glossary lookups.
      return sse.appliedTerms.length > 0 ? 'glossary-check' : 'translating';
    case 'idle':
    case 'segments_ready':
    case 'translated':
      return null;
  }
}

/**
 * Determines if the given UiStep should show the abort button.
 *
 * DEC-3: Abort is visible from step 2+ (ocr, translating, glossary-check).
 * Abort is hidden during `uploading` (step 1) because it's too fast to abort usefully.
 *
 * @param uiStep - Current UI step or null if no skeleton
 * @returns true if abort button should be visible
 */
export function isAbortableStep(uiStep: UiStep | null): boolean {
  return uiStep === 'ocr' || uiStep === 'translating' || uiStep === 'glossary-check';
}

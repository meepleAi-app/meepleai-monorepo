/**
 * Encounter route FSM helpers — Issue #1484.
 *
 * Pure mappers shared by the `/library/[gameId]/play/[campaignId]/encounter`
 * orchestrator (`_content.tsx`). Kept separate from the JSX glue so the
 * non-trivial state/error mapping is unit-testable in isolation.
 */

import type {
  EncounterErrorKind,
  EncounterStatus,
} from '@/components/features/gamebook/EncounterCheatsheetView';
import { EncounterParseError } from '@/lib/api/gamebook-encounter';

/** React Query mutation status values consumed by {@link deriveEncounterStatus}. */
export type MutationStatus = 'idle' | 'pending' | 'success' | 'error';

/**
 * Maps the parse mutation status to the component's FSM state. The rename is
 * intentional (pending→parsing, success→rendered) to keep the view vocabulary
 * aligned with the mockup states rather than React Query internals.
 */
export function deriveEncounterStatus(status: MutationStatus): EncounterStatus {
  switch (status) {
    case 'pending':
      return 'parsing';
    case 'success':
      return 'rendered';
    case 'error':
      return 'error';
    case 'idle':
    default:
      return 'idle';
  }
}

/**
 * Classifies a parse failure into the recoverable error kind surfaced to the
 * user: 409 → parse-failed (retry the photo), 404 → not-found (re-capture),
 * everything else → generic.
 */
export function mapEncounterError(error: unknown): EncounterErrorKind {
  if (error instanceof EncounterParseError) {
    if (error.status === 409) return 'parse-failed';
    if (error.status === 404) return 'not-found';
  }
  return 'generic';
}

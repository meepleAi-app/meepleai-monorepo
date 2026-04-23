import type { CardStatus, LifecycleState, OwnershipBadge } from '../types';

export interface MappedStatus {
  ownership?: OwnershipBadge;
  lifecycle?: LifecycleState;
}

/**
 * Maps the legacy `CardStatus` enum (12 values, 3 mixed axes) onto the
 * two-axis model (ownership, lifecycle).
 *
 * Merges:
 *  - `inprogress` → `active` (synonym)
 *  - `paused` → `idle` (semantically equivalent)
 *  - `indexed` → {} (internal KB pipeline state, never user-facing)
 */
export function mapLegacyStatus(status: CardStatus | undefined): MappedStatus {
  switch (status) {
    case 'owned':
      return { ownership: 'owned' };
    case 'wishlist':
      return { ownership: 'wishlist' };
    case 'archived':
      return { ownership: 'archived' };
    case 'active':
    case 'inprogress':
      return { lifecycle: 'active' };
    case 'idle':
    case 'paused':
      return { lifecycle: 'idle' };
    case 'completed':
      return { lifecycle: 'completed' };
    case 'setup':
      return { lifecycle: 'setup' };
    case 'processing':
      return { lifecycle: 'processing' };
    case 'failed':
      return { lifecycle: 'failed' };
    case 'indexed':
    case undefined:
    default:
      return {};
  }
}

/**
 * Resolves final `{ownership, lifecycle}` from new props or legacy status.
 * New props win; legacy status fills gaps only.
 */
export function resolveStatus(input: {
  ownership?: OwnershipBadge;
  lifecycle?: LifecycleState;
  legacyStatus?: CardStatus;
}): MappedStatus {
  const fromLegacy = mapLegacyStatus(input.legacyStatus);
  return {
    ownership: input.ownership ?? fromLegacy.ownership,
    lifecycle: input.lifecycle ?? fromLegacy.lifecycle,
  };
}

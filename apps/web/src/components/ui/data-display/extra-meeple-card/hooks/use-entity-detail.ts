import type { MeepleEntityType } from '../../meeple-card-styles';

// ============================================================================
// Generic Entity Detail Dispatcher Hook
// ============================================================================

interface EntityDetailResult {
  data: unknown | null;
  loading: boolean;
  error: Error | null;
}

const STUB_RESULT: EntityDetailResult = { data: null, loading: false, error: null };

/**
 * Generic entity detail dispatcher.
 * Routes to entity-specific hooks for implemented types.
 * Returns { data: null, loading: false, error: null } for unimplemented types.
 *
 * NOTE: React hooks cannot be called conditionally, so existing entity types
 * (game, agent, chatSession, kb) continue to use their dedicated hooks directly
 * inside their respective drawer content components. This hook provides a
 * unified interface for NEW entity types that don't yet have dedicated hooks.
 *
 * Future work will refactor existing hooks into this dispatcher pattern.
 */
export function useEntityDetail(type: MeepleEntityType, id: string): EntityDetailResult {
  void type; // reserved for future routing
  void id;
  return STUB_RESULT;
}

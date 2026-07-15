'use client';

import { useCallback } from 'react';

import type { ConnectionPip } from '@/components/ui/data-display/connection-bar';
import { useCascadeNavigationStore } from '@/lib/stores/cascade-navigation-store';

/**
 * Hook that wires ConnectionBar pip clicks to the cascade navigation store.
 * Rules (from spec):
 *   isEmpty === true → create action for this entity type, delegated to the
 *                      caller-supplied `onCreateEntity` callback (noop if absent)
 *   count >= 1       → openDeckStack (DeckStack resolves to drawer when 1 item)
 */
export function useConnectionBarNav(
  sourceEntityId: string,
  onCreateEntity?: (entityType: ConnectionPip['entityType']) => void
) {
  const openDeckStack = useCascadeNavigationStore(s => s.openDeckStack);

  const handlePipClick = useCallback(
    (pip: ConnectionPip, anchorRect: DOMRect) => {
      if (pip.isEmpty) {
        // count === 0: create a new entity of this type via the caller's flow.
        onCreateEntity?.(pip.entityType);
        return;
      }
      // count >= 1: show DeckStack (auto-resolves to drawer when exactly 1 item)
      openDeckStack(pip.entityType, sourceEntityId, anchorRect);
    },
    [sourceEntityId, openDeckStack, onCreateEntity]
  );

  return { handlePipClick };
}

'use client';

import { useCallback } from 'react';

import type { ConnectionPip } from '@/components/ui/data-display/connection-bar';
import { useCascadeNavigationStore } from '@/lib/stores/cascade-navigation-store';

/**
 * Hook that wires ConnectionBar pip clicks to the cascade navigation store.
 * Rules (from spec):
 *   isEmpty === true → create action (handled by caller via onCreateEntity)
 *   count >= 1       → openDeckStack (DeckStack resolves to drawer when 1 item)
 */
export function useConnectionBarNav(sourceEntityId: string) {
  const openDeckStack = useCascadeNavigationStore(s => s.openDeckStack);

  const handlePipClick = useCallback(
    (pip: ConnectionPip, anchorRect: DOMRect) => {
      if (pip.isEmpty) {
        // count === 0: create action — caller handles via onCreateEntity callback
        return;
      }
      // count >= 1: show DeckStack (DeckStack auto-resolves to drawer when it has exactly 1 item)
      openDeckStack(pip.entityType, sourceEntityId, anchorRect);
    },
    [sourceEntityId, openDeckStack]
  );

  return { handlePipClick };
}

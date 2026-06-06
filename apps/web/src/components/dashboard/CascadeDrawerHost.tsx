'use client';

/**
 * CascadeDrawerHost — mounts ExtraMeepleCardDrawer driven by cascade-navigation-store.
 *
 * Issue #1929 Task C Macro 2 — WP5.
 *
 * Renders the ExtraMeepleCardDrawer whenever the cascade-navigation-store
 * transitions to state='drawer'. The host listens to the Zustand store and
 * passes through entityType/entityId/open/onClose props so the drawer
 * lifecycle is fully store-driven (no local state required).
 *
 * Mount this component once inside DashboardClient so all ProssimiSection /
 * RecentiSection / FriendsActivitySection card clicks that call openDrawer()
 * get their drawer rendered without each section needing its own Drawer root.
 *
 * Pairs with:
 *   - useCascadeNavigationStore.openDrawer() (called by ProssimiSection WP4)
 *   - useCascadeNavigationStore.pushDrawer() (called by GameNightEventDrawerContent WP3)
 *   - ExtraMeepleCardDrawer (drawer renderer with cascade stack support)
 */

import { ExtraMeepleCardDrawer } from '@/components/ui/data-display/extra-meeple-card/ExtraMeepleCardDrawer';
import type { DrawerEntityType } from '@/components/ui/data-display/extra-meeple-card/ExtraMeepleCardDrawer';
import { useCascadeNavigationStore } from '@/lib/stores/cascade-navigation-store';

export function CascadeDrawerHost() {
  const state = useCascadeNavigationStore(s => s.state);
  const activeEntityType = useCascadeNavigationStore(s => s.activeEntityType);
  const activeEntityId = useCascadeNavigationStore(s => s.activeEntityId);
  const closeCascade = useCascadeNavigationStore(s => s.closeCascade);

  const isOpen = state === 'drawer' && activeEntityType !== null && activeEntityId !== null;

  return (
    <ExtraMeepleCardDrawer
      entityType={(activeEntityType ?? 'game') as DrawerEntityType}
      entityId={activeEntityId ?? ''}
      open={isOpen}
      onClose={closeCascade}
      data-testid="cascade-drawer-host"
    />
  );
}

'use client';

import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card';

interface DrawerContentProps {
  entityType: MeepleEntityType;
  entityId: string;
  activeTab?: string;
  onNavigate?: () => void;
}

/**
 * DrawerContent renders entity-specific content inside HandDrawer.
 * Fully implemented in Task 3 — this is the base stub.
 */
export function DrawerContent({ entityType, entityId, activeTab, onNavigate }: DrawerContentProps) {
  return (
    <div className="flex-1 overflow-y-auto p-4">
      <p className="text-white/40 text-sm">
        {entityType} — {entityId}
      </p>
    </div>
  );
}

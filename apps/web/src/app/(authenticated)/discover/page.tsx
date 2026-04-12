'use client';

import { HubLayout } from '@/components/layout/HubLayout';
import { useMiniNavConfig } from '@/hooks/useMiniNavConfig';

export default function DiscoverPage() {
  useMiniNavConfig({
    breadcrumb: 'Scopri',
    tabs: [{ id: 'all', label: 'Scopri', href: '/discover' }],
    activeTabId: 'all',
  });

  return (
    <HubLayout searchPlaceholder="Cerca giochi, agenti...">
      <div className="flex flex-col items-center gap-3 py-16 px-4 text-center text-[var(--nh-text-muted,#94a3b8)]">
        <span className="text-5xl">🔍</span>
        <p className="font-medium">Scopri in arrivo.</p>
        <p className="text-sm">
          Qui troverai giochi dalla community, proposte e catalogo condiviso.
        </p>
      </div>
    </HubLayout>
  );
}

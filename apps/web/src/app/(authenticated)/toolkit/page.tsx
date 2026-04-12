'use client';

import { HubLayout } from '@/components/layout/HubLayout';
import { useMiniNavConfig } from '@/hooks/useMiniNavConfig';

export default function ToolkitHubPage() {
  useMiniNavConfig({
    breadcrumb: 'Toolkit',
    tabs: [{ id: 'all', label: 'Toolkit', href: '/toolkit' }],
    activeTabId: 'all',
  });

  return (
    <HubLayout searchPlaceholder="Cerca tool..." showViewToggle>
      <div className="flex flex-col items-center gap-3 py-16 px-4 text-center text-[var(--nh-text-muted,#94a3b8)]">
        <span className="text-5xl">🛠️</span>
        <p className="font-medium">Toolkit in arrivo.</p>
        <p className="text-sm">Qui troverai timer, contatori, mazzi e altri strumenti di gioco.</p>
      </div>
    </HubLayout>
  );
}

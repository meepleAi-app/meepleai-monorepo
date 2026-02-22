'use client';

/**
 * AgentsNavConfig — Registers MiniNav tabs + ActionBar actions for /agents
 * Issue #5043 — Agents + Slots MiniNav + ActionBar
 *
 * Tabs: My Agents · Slots
 * ActionBar: Create Agent (primary) · Manage Slots
 *
 * Include in agents/page.tsx:
 *   <AgentsNavConfig />
 */

import { useEffect } from 'react';

import { Grid2x2, Plus } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { useSetNavConfig } from '@/hooks/useSetNavConfig';

export function AgentsNavConfig() {
  const setNavConfig = useSetNavConfig();
  const router = useRouter();

  useEffect(() => {
    setNavConfig({
      miniNav: [
        { id: 'my-agents', label: 'My Agents', href: '/agents' },
        { id: 'slots',     label: 'Slots',     href: '/agents?tab=slots' },
      ],
      actionBar: [
        {
          id: 'create-agent',
          label: 'Create Agent',
          icon: Plus,
          variant: 'primary',
          onClick: () => router.push('/agents?action=create'),
        },
        {
          id: 'manage-slots',
          label: 'Manage Slots',
          icon: Grid2x2,
          onClick: () => router.push('/agents?tab=slots'),
        },
      ],
    });
  }, [setNavConfig, router]);

  return null;
}

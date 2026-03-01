/**
 * Agents Section Layout
 * Issue #5039 — User Route Consolidation
 * Issue #5043 — Agents + ActionBar
 *
 * Registers MiniNav tabs and FloatingActionBar for the /agents section.
 * "Crea Agente" dispatches a custom event so AgentsPage can open its sheet.
 */

'use client';

import { type ReactNode, useEffect } from 'react';

import { Bot, Layers, Plus } from 'lucide-react';

import { useSetNavConfig } from '@/context/NavigationContext';

export default function AgentsLayout({ children }: { children: ReactNode }) {
  const setNavConfig = useSetNavConfig();

  useEffect(() => {
    setNavConfig({
      miniNav: [
        { id: 'agents', label: 'I miei agenti', href: '/agents', icon: Bot },
        { id: 'slots', label: 'Slot agenti', href: '/agents?tab=slots', icon: Layers },
      ],
      actionBar: [
        {
          id: 'create-agent',
          label: 'Crea Agente',
          icon: Plus,
          variant: 'primary',
          onClick: () => {
            // Dispatch event so AgentsPage can open AgentCreationSheet
            document.dispatchEvent(new CustomEvent('agents:open-creation'));
          },
        },
      ],
    });
  }, [setNavConfig]);

  return <>{children}</>;
}

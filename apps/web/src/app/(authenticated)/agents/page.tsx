/**
 * Agents Page — Wave B.2 (Issue #634) brownfield migration to v2 design.
 *
 * Thin shell wiring `AgentsLibraryView` orchestrator (v2 layout, 5-state FSM)
 * to the existing `AgentCreationSheet` modal v1 (AC-13: orchestrator agnostic
 * of modal lifecycle). The orchestrator owns hero/filters/grid/empty surfaces;
 * this page hosts the modal and the recents-store side effect.
 */

'use client';

import { useEffect, useState } from 'react';

import { AgentCreationSheet } from '@/components/agent/config';
import { useRecentsStore } from '@/stores/use-recents';

import { AgentsLibraryView } from './_components/AgentsLibraryView';

export default function AgentsPage() {
  const [creationSheetOpen, setCreationSheetOpen] = useState(false);

  useEffect(() => {
    useRecentsStore.getState().push({
      id: 'section-agents',
      entity: 'agent',
      title: 'Agents',
      href: '/agents',
    });
  }, []);

  return (
    <>
      <AgentsLibraryView onCreateAgent={() => setCreationSheetOpen(true)} />
      <AgentCreationSheet isOpen={creationSheetOpen} onClose={() => setCreationSheetOpen(false)} />
    </>
  );
}

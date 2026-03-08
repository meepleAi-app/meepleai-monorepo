/**
 * Agent Creation Wizard Page - /chat/agents/create (Issue #4915)
 *
 * 4-step wizard for users to create their own custom AI agents:
 * 1. Select a game from library
 * 2. Choose agent type (Tutor / Arbitro / Stratega / Narratore)
 * 3. Name the agent + pick knowledge base PDFs
 * 4. Review & confirm
 */

'use client';

import { Suspense } from 'react';

import dynamic from 'next/dynamic';

const AgentCreationWizard = dynamic(
  () =>
    import('@/components/chat-unified/AgentCreationWizard').then(mod => ({
      default: mod.AgentCreationWizard,
    })),
  {
    ssr: false,
    loading: () => (
      <div className="flex h-dvh items-center justify-center">
        <div className="text-center">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-amber-500 mx-auto mb-4" />
          <p className="text-muted-foreground font-nunito">Caricamento...</p>
        </div>
      </div>
    ),
  }
);

export default function CreateAgentPage() {
  return (
    <Suspense>
      <AgentCreationWizard />
    </Suspense>
  );
}

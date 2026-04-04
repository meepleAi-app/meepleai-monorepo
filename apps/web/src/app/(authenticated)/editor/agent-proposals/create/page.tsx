'use client';

import { useRouter } from 'next/navigation';

import { Button } from '@/components/ui/primitives/button';

/**
 * Agent proposal creation page.
 * Typology proposals were removed in the agent system simplification.
 * Agent definitions are now managed directly in the admin panel.
 */
export default function CreateProposalPage() {
  const router = useRouter();

  return (
    <div className="container mx-auto p-6 max-w-2xl">
      <div className="text-center py-24 space-y-4">
        <h1 className="text-2xl font-bold">Feature Removed</h1>
        <p className="text-muted-foreground">
          Typology proposals have been replaced by Agent Definitions. Agent configurations are now
          managed directly by administrators.
        </p>
        <Button onClick={() => router.push('/editor/agent-proposals')}>Back to Proposals</Button>
      </div>
    </div>
  );
}

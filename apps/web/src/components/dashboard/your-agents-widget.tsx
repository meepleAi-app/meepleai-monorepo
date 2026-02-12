/**
 * Your Agents Widget (Issue #4091)
 * Dashboard widget showing recent agent interactions
 */

'use client';

import React from 'react';

import { ArrowRight } from 'lucide-react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { Button } from '@/components/ui/primitives/button';
import { useRecentAgents } from '@/hooks/queries/useRecentAgents';

export function YourAgentsWidget() {
  const router = useRouter();
  // Use real API (Issue #4126)
  const { data: recentAgents = [], isLoading: _isLoading } = useRecentAgents(3);

  return (
    <div className="bg-card rounded-xl p-6 border border-border/50">
      <div className="flex items-center justify-between mb-4">
        <h2 className="text-xl font-bold font-quicksand">Your Agents</h2>
        <Link href="/agents">
          <Button variant="ghost" size="sm" className="gap-1">
            View All
            <ArrowRight className="h-4 w-4" />
          </Button>
        </Link>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        {recentAgents.map(agent => (
          <MeepleCard
            key={agent.id}
            entity="agent"
            variant="compact"
            title={agent.name}
            subtitle={`${agent.type} • ${agent.invocationCount} uses`}
            onClick={() => {
              router.push(`/agents/${agent.id}/chat`);
            }}
          />
        ))}
      </div>
    </div>
  );
}

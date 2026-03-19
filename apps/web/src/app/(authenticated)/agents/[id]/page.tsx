/**
 * Agent Detail Page - Chat with AI Agent
 *
 * Displays agent information using AgentCharacterSheet — an RPG-style two-column layout
 * with a sticky portrait on the left and scrollable sections (KB, Chat, History) on the right.
 * No tabs — all sections visible at once.
 */

import { Metadata } from 'next';
import { notFound } from 'next/navigation';

import { AgentCharacterSheet } from '@/components/agent/AgentCharacterSheet';
import { DeckTrackerSync } from '@/components/layout/DeckTrackerSync';
import type { AgentDetailData } from '@/components/ui/data-display/extra-meeple-card/types';
import { api } from '@/lib/api';

interface AgentPageProps {
  params: { id: string };
}

export async function generateMetadata({ params }: AgentPageProps): Promise<Metadata> {
  try {
    const agent = await api.agents.getById(params.id);

    if (!agent) {
      return {
        title: 'Agent Not Found | MeepleAI',
      };
    }

    return {
      title: `${agent.name} | MeepleAI Agents`,
      description: `Chat with ${agent.name} - ${agent.type} agent with knowledge base integration`,
    };
  } catch {
    return {
      title: 'Agent Not Found | MeepleAI',
    };
  }
}

export default async function AgentPage({ params }: AgentPageProps) {
  let agent;

  try {
    agent = await api.agents.getById(params.id);
  } catch {
    notFound();
  }

  if (!agent) {
    notFound();
  }

  const agentHref = `/agents/${agent.id}`;

  // Build AgentDetailData for the CharacterSheet
  const agentDetailData: AgentDetailData = {
    id: agent.id,
    name: agent.name,
    type: agent.type,
    strategyName: agent.strategyName,
    strategyParameters: agent.strategyParameters,
    isActive: agent.isActive,
    isIdle: agent.isIdle,
    invocationCount: agent.invocationCount,
    lastInvokedAt: agent.lastInvokedAt,
    createdAt: agent.createdAt,
    gameId: agent.gameId ?? undefined,
    gameName: agent.gameName ?? undefined,
  };

  return (
    <div className="container max-w-7xl py-8">
      <DeckTrackerSync
        entity="agent"
        id={agent.id}
        title={agent.name}
        href={agentHref}
        subtitle={agent.isActive ? 'Active' : 'Inactive'}
      />

      {/* Header */}
      <div className="mb-6">
        <h1 className="text-3xl font-bold tracking-tight mb-2">{agent.name}</h1>
        <p className="text-muted-foreground">Agente AI alimentato dalla Knowledge Base</p>
      </div>

      {/* Character Sheet */}
      <AgentCharacterSheet data={agentDetailData} />
    </div>
  );
}

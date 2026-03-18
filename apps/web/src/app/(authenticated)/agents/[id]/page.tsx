/**
 * Agent Detail Page - Chat with AI Agent
 *
 * Displays agent information with MeepleCard hero + AgentExtraMeepleCard (tabbed interface).
 * AgentExtraMeepleCard includes: Chat (SSE streaming), Overview, Stats, History, KB tabs.
 *
 * The embedded chat tab validates agent readiness (KB populated, RAG initialized)
 * before enabling the chat interface.
 */

import { Metadata } from 'next';
import { notFound } from 'next/navigation';

import { DeckTrackerSync } from '@/components/layout/DeckTrackerSync';
import { AgentExtraMeepleCard } from '@/components/ui/data-display/extra-meeple-card/entities/AgentExtraMeepleCard';
import type { AgentDetailData } from '@/components/ui/data-display/extra-meeple-card/types';
import { MeepleCard, type MeepleCardMetadata } from '@/components/ui/data-display/meeple-card';
import { getNavigationLinks } from '@/config/entity-navigation';
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

  // Build metadata for MeepleCard
  const metadata: MeepleCardMetadata[] = [
    {
      label: 'Tipo',
      value: agent.type,
    },
    {
      label: 'Invocazioni',
      value: agent.invocationCount.toString(),
    },
  ];

  if (agent.lastInvokedAt) {
    const lastUsed = new Date(agent.lastInvokedAt);
    const diffMs = Date.now() - lastUsed.getTime();
    const diffMins = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMs / 3600000);
    const diffDays = Math.floor(diffMs / 86400000);

    let relativeTime = '';
    if (diffMins < 1) relativeTime = 'Proprio ora';
    else if (diffMins < 60) relativeTime = `${diffMins}m fa`;
    else if (diffHours < 24) relativeTime = `${diffHours}h fa`;
    else relativeTime = `${diffDays}d fa`;

    metadata.push({
      label: 'Ultimo uso',
      value: relativeTime,
    });
  }

  const agentHref = `/agents/${agent.id}`;

  // Build AgentDetailData for the ExtraMeepleCard
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
        <h1 className="text-3xl font-bold tracking-tight mb-2">Agent Chat</h1>
        <p className="text-muted-foreground">
          Interact with AI agents powered by your knowledge base
        </p>
      </div>

      {/* Main Content: MeepleCard + AgentExtraMeepleCard */}
      <section className="flex flex-col lg:flex-row lg:items-start gap-6">
        {/* Left: Agent Hero Card */}
        <MeepleCard
          entity="agent"
          variant="hero"
          title={agent.name}
          subtitle={agent.isActive ? 'Active' : 'Inactive'}
          metadata={metadata}
          navigateTo={getNavigationLinks('agent', { id: agent.id })}
        />

        {/* Right: Tabbed Chat + Info Interface */}
        <AgentExtraMeepleCard data={agentDetailData} enableChat />
      </section>
    </div>
  );
}

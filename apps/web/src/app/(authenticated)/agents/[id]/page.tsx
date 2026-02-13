/**
 * Agent Detail Page - Chat with AI Agent
 *
 * Displays agent information with MeepleCard + AgentInfoCard (tabbed chat interface).
 * Follows game page pattern: hero card + companion info card.
 *
 * POC: Agent chat page with SSE streaming, history, and KB context display
 */

import { Metadata } from 'next';
import { notFound } from 'next/navigation';

import { MeepleCard, type MeepleCardMetadata } from '@/components/ui/data-display/meeple-card';
import { AgentInfoCard } from '@/components/agent/AgentInfoCard';
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

  return (
    <div className="container max-w-7xl py-8">
      {/* Header */}
      <div className="mb-6">
        <h1 className="text-3xl font-bold tracking-tight mb-2">Agent Chat</h1>
        <p className="text-muted-foreground">
          Interact with AI agents powered by your knowledge base
        </p>
      </div>

      {/* Main Content: MeepleCard + AgentInfoCard */}
      <section className="flex flex-col lg:flex-row lg:items-start gap-6">
        {/* Left: Agent Hero Card */}
        <MeepleCard
          entity="agent"
          variant="hero"
          title={agent.name}
          subtitle={agent.isActive ? 'Active' : 'Inactive'}
          metadata={metadata}
        />

        {/* Right: Tabbed Chat Interface */}
        <AgentInfoCard agentId={agent.id} agentName={agent.name} />
      </section>
    </div>
  );
}

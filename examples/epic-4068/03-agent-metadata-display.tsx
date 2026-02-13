/**
 * Example 3: Agent Metadata Display
 * Epic #4068 - Issue #4184
 *
 * Shows agent status, model info, invocation stats
 */

import React from 'react';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { AgentStatusBadge } from '@/components/ui/agent/AgentStatusBadge';
import { AgentModelInfo } from '@/components/ui/agent/AgentModelInfo';
import { AgentStatsDisplay } from '@/components/ui/agent/AgentStatsDisplay';
import { AgentStatus } from '@/types/agent';
import type { AgentMetadata } from '@/types/agent';

export function AgentMetadataExample() {
  // Example agents with different states
  const agents = [
    {
      id: '1',
      name: 'Azul Rules Expert',
      strategy: 'RAG Strategy',
      metadata: {
        status: AgentStatus.Active,
        model: {
          name: 'GPT-4o-mini',
          temperature: 0.7,
          maxTokens: 2000
        },
        invocationCount: 342,
        lastExecuted: '2026-02-12T10:00:00Z',
        avgResponseTime: 450,
        capabilities: ['RAG', 'Vision']
      } as AgentMetadata
    },
    {
      id: '2',
      name: 'Strategy Analyzer',
      strategy: 'Analysis Agent',
      metadata: {
        status: AgentStatus.Idle,
        model: {
          name: 'Claude-3-Sonnet',
          temperature: 0.5,
          maxTokens: 4000
        },
        invocationCount: 1523,
        lastExecuted: '2026-02-12T08:30:00Z',
        capabilities: ['RAG', 'Code']
      } as AgentMetadata
    },
    {
      id: '3',
      name: 'Image Describer',
      strategy: 'Vision Model',
      metadata: {
        status: AgentStatus.Training,
        model: {
          name: 'GPT-4-Vision',
          temperature: 0.3
        },
        invocationCount: 45,
        capabilities: ['Vision']
      } as AgentMetadata
    },
    {
      id: '4',
      name: 'Broken Agent',
      strategy: 'Error State',
      metadata: {
        status: AgentStatus.Error,
        model: { name: 'GPT-3.5-Turbo' },
        invocationCount: 12,
        lastExecuted: '2026-02-12T09:00:00Z',
        capabilities: []
      } as AgentMetadata
    }
  ];

  return (
    <div className="space-y-8 p-8">
      <section>
        <h1 className="text-2xl font-bold mb-4">Agent Metadata Display</h1>
        <p className="text-muted-foreground">
          Enhanced agent cards with status badges, model info, and invocation statistics (Epic #4068 - Issue #4184)
        </p>
      </section>

      <section>
        <h2 className="text-xl font-semibold mb-4">Agent Cards (Grid View)</h2>

        <div className="grid grid-cols-4 gap-4">
          {agents.map(agent => (
            <MeepleCard
              key={agent.id}
              entity="agent"
              variant="grid"
              title={agent.name}
              subtitle={agent.strategy}
              agentMetadata={agent.metadata}
              tags={createTagsFromKeys('agent', agent.metadata.capabilities.map(c => c.toLowerCase()))}
            />
          ))}
        </div>
      </section>

      <section>
        <h2 className="text-xl font-semibold mb-4">Status Badge Examples</h2>

        <div className="flex gap-4">
          <div className="p-4 border rounded">
            <AgentStatusBadge status={AgentStatus.Active} />
            <p className="text-xs text-muted-foreground mt-2">Active (green ●)</p>
          </div>

          <div className="p-4 border rounded">
            <AgentStatusBadge status={AgentStatus.Idle} />
            <p className="text-xs text-muted-foreground mt-2">Idle (gray ○)</p>
          </div>

          <div className="p-4 border rounded">
            <AgentStatusBadge status={AgentStatus.Training} />
            <p className="text-xs text-muted-foreground mt-2">Training (yellow ◐)</p>
          </div>

          <div className="p-4 border rounded">
            <AgentStatusBadge status={AgentStatus.Error} />
            <p className="text-xs text-muted-foreground mt-2">Error (red ✕)</p>
          </div>
        </div>
      </section>

      <section>
        <h2 className="text-xl font-semibold mb-4">Model Info Examples</h2>

        <div className="space-y-4">
          {agents.map(agent => (
            <div key={agent.id} className="p-4 border rounded">
              <h3 className="font-medium mb-2">{agent.name}</h3>
              <AgentModelInfo model={agent.metadata.model} variant="badge" />
              <p className="text-xs text-muted-foreground mt-2">
                Hover model badge to see temperature, max tokens, and other parameters
              </p>
            </div>
          ))}
        </div>
      </section>

      <section>
        <h2 className="text-xl font-semibold mb-4">Stats Display Examples</h2>

        <div className="space-y-4">
          {agents.map(agent => (
            <div key={agent.id} className="p-4 border rounded">
              <h3 className="font-medium mb-2">{agent.name}</h3>
              <AgentStatsDisplay metadata={agent.metadata} layout="horizontal" showIcons />
              <p className="text-xs text-muted-foreground mt-2">
                Invocation count: {agent.metadata.invocationCount} formatted
                {agent.metadata.lastExecuted && ` • Last run: ${agent.metadata.lastExecuted}`}
              </p>
            </div>
          ))}
        </div>
      </section>

      <section>
        <h2 className="text-xl font-semibold mb-4">Complete Agent Card (All Features)</h2>

        <div className="max-w-sm">
          <MeepleCard
            entity="agent"
            variant="featured"
            title="Ultimate Agent"
            subtitle="All Features Enabled"
            agentMetadata={{
              status: AgentStatus.Active,
              model: {
                name: 'GPT-4o',
                temperature: 0.8,
                maxTokens: 4000
              },
              invocationCount: 5432,
              lastExecuted: '2026-02-12T11:30:00Z',
              avgResponseTime: 1200,
              capabilities: ['RAG', 'Vision', 'Code', 'Functions']
            }}
            tags={createTagsFromKeys('agent', ['rag', 'vision', 'code'])}
            maxVisibleTags={3}
            quickActions={[
              { icon: Brain, label: 'Chat', onClick: () => alert('Open chat') },
              { icon: Eye, label: 'Stats', onClick: () => alert('View stats') },
              { icon: Code2, label: 'Configure', onClick: () => alert('Configure agent') }
            ]}
          />
        </div>

        <p className="mt-4 text-sm text-muted-foreground">
          Complete agent card with status badge, model info tooltip, stats display, capability tags, and quick actions
        </p>
      </section>

      <section>
        <h2 className="text-xl font-semibold mb-4">Code Example: Formatting Utilities</h2>

        <pre className="bg-muted p-4 rounded text-sm overflow-auto">
{`import { formatInvocationCount, formatRelativeTime, formatResponseTime } from '@/lib/agent/formatters';

// Invocation count formatting
formatInvocationCount(342);       // "342"
formatInvocationCount(1234);      // "1.2K"
formatInvocationCount(3456789);   // "3.5M"

// Relative time formatting
formatRelativeTime('2026-02-12T11:55:00Z'); // "5 minutes ago" (if now is 12:00)
formatRelativeTime('2026-02-12T10:00:00Z'); // "2 hours ago"
formatRelativeTime('2026-02-10T10:00:00Z'); // "2 days ago"

// Response time formatting
formatResponseTime(45);    // "45ms"
formatResponseTime(1200);  // "1.2s"
formatResponseTime(150000);// "2.5m"`}
        </pre>
      </section>
    </div>
  );
}

export default AgentMetadataExample;

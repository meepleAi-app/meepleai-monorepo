/**
 * AgentSelector — Custom + system agent picker for a selected game
 *
 * Fetches user-owned agents for the selected game via API.
 * Always shows the 5 system agents (auto, tutor, arbitro, stratega, narratore).
 * Custom agents are displayed above system agents with a "Create new" link.
 */

'use client';

import React, { useEffect, useState, useCallback } from 'react';

import { Bot, Plus } from 'lucide-react';
import Link from 'next/link';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { api } from '@/lib/api';
import { cn } from '@/lib/utils';

import { DEFAULT_AGENTS } from './constants';

import type { AgentOption, CustomAgent } from './types';

// ── Sub-components ──────────────────────────────────────────────────────────

function SystemAgentGrid({
  agents,
  selectedAgentType,
  onSelect,
}: {
  agents: AgentOption[];
  selectedAgentType: string | null;
  onSelect: (agentType: string) => void;
}) {
  return (
    <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
      {agents.map(agent => (
        <button
          key={agent.id}
          onClick={() => onSelect(agent.type)}
          className={cn(
            'text-left rounded-xl transition-all duration-200 focus:outline-none focus:ring-2 focus:ring-amber-500/40',
            selectedAgentType === agent.type
              ? 'ring-2 ring-amber-500 scale-[1.02]'
              : 'hover:scale-[1.01]'
          )}
          aria-pressed={selectedAgentType === agent.type}
          data-testid={`agent-card-${agent.type}`}
        >
          <MeepleCard
            entity="agent"
            variant="compact"
            title={agent.name}
            subtitle={agent.description}
            badge={agent.icon}
            className={cn(selectedAgentType === agent.type && 'border-amber-500')}
          />
        </button>
      ))}
    </div>
  );
}

function CustomAgentGridSection({
  agents,
  selectedCustomAgentId,
  onSelect,
  gameId,
  isLoading,
}: {
  agents: CustomAgent[];
  selectedCustomAgentId: string | null;
  onSelect: (agentId: string) => void;
  gameId: string;
  isLoading: boolean;
}) {
  if (isLoading) {
    return (
      <div className="flex gap-3 mb-4">
        {Array.from({ length: 2 }).map((_, i) => (
          <div key={i} className="h-20 w-32 rounded-xl bg-muted/50 animate-pulse" />
        ))}
      </div>
    );
  }

  if (agents.length === 0) return null;

  return (
    <div className="mb-4">
      <div className="flex items-center justify-between mb-2">
        <p className="text-xs font-semibold uppercase tracking-wide text-amber-700 dark:text-amber-400 font-nunito">
          I tuoi agent
        </p>
        <Link
          href={`/chat/agents/create?gameId=${gameId}`}
          className="flex items-center gap-1 text-xs text-amber-600 hover:text-amber-700 dark:text-amber-400 dark:hover:text-amber-300 font-nunito transition-colors"
          data-testid="create-agent-link"
        >
          <Plus className="h-3 w-3" />
          Crea nuovo agent
        </Link>
      </div>
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
        {agents.map(agent => (
          <button
            key={agent.id}
            onClick={() => onSelect(agent.id)}
            className={cn(
              'text-left rounded-xl transition-all duration-200 focus:outline-none focus:ring-2 focus:ring-amber-500/40',
              selectedCustomAgentId === agent.id
                ? 'ring-2 ring-amber-500 scale-[1.02]'
                : 'hover:scale-[1.01]'
            )}
            aria-pressed={selectedCustomAgentId === agent.id}
            data-testid={`custom-agent-card-${agent.id}`}
          >
            <MeepleCard
              entity="agent"
              variant="compact"
              title={agent.name}
              subtitle={agent.type}
              badge="🤖"
              className={cn(
                'border-amber-300/50',
                selectedCustomAgentId === agent.id && 'border-amber-500'
              )}
            />
          </button>
        ))}
      </div>
      <div className="mt-2 border-t border-border/30 pt-2">
        <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground font-nunito">
          Agent di sistema
        </p>
      </div>
    </div>
  );
}

// ── Main Component ──────────────────────────────────────────────────────────

export interface AgentSelectorProps {
  gameId: string | null;
  onSelectSystemAgent: (agentType: string) => void;
  onSelectCustomAgent: (agentId: string) => void;
  selectedAgentType: string | null;
  selectedCustomAgentId: string | null;
  className?: string;
  /** Called when custom agents finish loading — exposes agent list and loading state */
  onCustomAgentsResolved?: (agents: CustomAgent[], isLoading: boolean) => void;
}

export function AgentSelector({
  gameId,
  onSelectSystemAgent,
  onSelectCustomAgent,
  selectedAgentType,
  selectedCustomAgentId,
  className,
  onCustomAgentsResolved,
}: AgentSelectorProps) {
  const [customAgents, setCustomAgents] = useState<CustomAgent[]>([]);
  const [isLoadingCustom, setIsLoadingCustom] = useState(false);

  // Fetch custom agents when gameId changes
  useEffect(() => {
    if (!gameId) {
      setCustomAgents([]);
      onCustomAgentsResolved?.([], false);
      return;
    }

    let cancelled = false;
    setIsLoadingCustom(true);

    api.agents
      .getUserAgentsForGame(gameId)
      .then(result => {
        if (!cancelled) {
          const agents = result.map(a => ({ id: a.id, name: a.name, type: a.type }));
          setCustomAgents(agents);
          onCustomAgentsResolved?.(agents, false);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setCustomAgents([]);
          onCustomAgentsResolved?.([], false);
        }
      })
      .finally(() => {
        if (!cancelled) setIsLoadingCustom(false);
      });

    return () => {
      cancelled = true;
    };
  }, [gameId, onCustomAgentsResolved]);

  const handleSystemSelect = useCallback(
    (agentType: string) => {
      onSelectSystemAgent(agentType);
    },
    [onSelectSystemAgent]
  );

  const handleCustomSelect = useCallback(
    (agentId: string) => {
      onSelectCustomAgent(agentId);
    },
    [onSelectCustomAgent]
  );

  return (
    <section
      className={cn(
        'p-6 rounded-2xl bg-white/70 dark:bg-card/70 backdrop-blur-md border border-border/50',
        className
      )}
      data-testid="agent-selection-section"
    >
      <div className="flex items-center gap-2 mb-4">
        <Bot className="h-5 w-5 text-amber-600 dark:text-amber-400" />
        <h2 className="text-lg font-semibold font-quicksand text-foreground">
          Seleziona un agente
        </h2>
      </div>

      {/* Custom agents (only when a game is selected) */}
      {gameId && gameId !== '' && (
        <CustomAgentGridSection
          agents={customAgents}
          selectedCustomAgentId={selectedCustomAgentId}
          onSelect={handleCustomSelect}
          gameId={gameId}
          isLoading={isLoadingCustom}
        />
      )}

      <SystemAgentGrid
        agents={DEFAULT_AGENTS}
        selectedAgentType={selectedAgentType}
        onSelect={handleSystemSelect}
      />
    </section>
  );
}

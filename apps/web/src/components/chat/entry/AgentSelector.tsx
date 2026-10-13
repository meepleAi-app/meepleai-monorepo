/**
 * AgentSelector — picker for the 5 chat personas (auto, tutor, arbitro, stratega, narratore)
 *
 * Issue #4138: this used to ALSO fetch "i tuoi agent" — user-owned agents for the selected game,
 * via `api.agents.getUserAgentsForGame`. That half is gone, and it is worth being precise about
 * what it was: those entries came from `user_library_entries.CustomAgentConfigJson`, and NOTHING
 * on the answer path read that configuration (`Personality` / `DetailLevel` / `PersonalNotes` have
 * no reader outside UserLibrary and its DTOs; `AskQuestionQueryHandler`, `StreamQaQueryHandler`,
 * `RagPromptAssemblyService` and `PlaygroundChatCommandHandler` never touch it). So the selector
 * offered a choice that could not change a single answer.
 *
 * What remains is a real choice: the persona is `ChatThread.AgentType`, a separate concept that
 * shares the name and that this retirement does NOT touch.
 */

'use client';

import React from 'react';

import { Bot } from 'lucide-react';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { cn } from '@/lib/utils';

import { DEFAULT_AGENTS } from './constants';

import type { AgentOption } from './types';

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
            headingLevel={2}
          />
        </button>
      ))}
    </div>
  );
}

// ── Main Component ──────────────────────────────────────────────────────────

export interface AgentSelectorProps {
  onSelectSystemAgent: (agentType: string) => void;
  selectedAgentType: string | null;
  className?: string;
}

export function AgentSelector({
  onSelectSystemAgent,
  selectedAgentType,
  className,
}: AgentSelectorProps) {
  return (
    <section
      className={cn(
        'p-6 rounded-2xl bg-card/70 dark:bg-card/70 backdrop-blur-md border border-border/50',
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

      <SystemAgentGrid
        agents={DEFAULT_AGENTS}
        selectedAgentType={selectedAgentType}
        onSelect={onSelectSystemAgent}
      />
    </section>
  );
}

/**
 * AgentSelector - Multi-agent switching component (Task #4, Issue #12)
 *
 * Features:
 * - Dropdown selector for Tutor, Arbitro, Stratega, Narratore, Orchestrator (auto)
 * - Agent descriptions and icons
 * - Selection persistence per conversation
 *
 * #3393 (finding C9c): the hardcoded "online" status indicators and the no-op
 * 30s poll (which only ever re-set every agent to 'online') were removed — no
 * real per-agent online/offline signal exists in the backend.
 */

'use client';

import { Bot, Sparkles, Shield, Zap, BookOpen, Target } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export type AgentType = 'auto' | 'tutor' | 'arbitro' | 'stratega' | 'narratore';

export interface Agent {
  id: AgentType;
  name: string;
  description: string;
  icon: typeof Bot;
}

export interface AgentSelectorProps {
  /** Currently selected agent ID */
  value: AgentType;
  /** Callback when agent changes */
  onChange: (agentId: AgentType) => void;
  /** Additional class name */
  className?: string;
  /** Disabled state */
  disabled?: boolean;
}

// ============================================================================
// Agent Definitions
// ============================================================================

export const AGENT_NAMES: Record<AgentType, string> = {
  auto: 'Auto (Orchestrator)',
  tutor: 'Tutor',
  arbitro: 'Arbitro',
  stratega: 'Stratega',
  narratore: 'Narratore',
};

const AGENTS: Record<AgentType, Agent> = {
  auto: {
    id: 'auto',
    name: 'Auto (Orchestrator)',
    description: 'Automatically routes to best agent',
    icon: Zap,
  },
  tutor: {
    id: 'tutor',
    name: 'Tutor',
    description: 'Setup, rules Q&A, tutorials',
    icon: Bot,
  },
  arbitro: {
    id: 'arbitro',
    name: 'Arbitro',
    description: 'Move validation, rules arbitration',
    icon: Shield,
  },
  stratega: {
    id: 'stratega',
    name: 'Stratega',
    description: 'Strategic analysis and move suggestions',
    icon: Target,
  },
  narratore: {
    id: 'narratore',
    name: 'Narratore',
    description: 'Game lore, atmosphere, and narrative',
    icon: BookOpen,
  },
};

// ============================================================================
// Main Component
// ============================================================================

export function AgentSelector({ value, onChange, className, disabled }: AgentSelectorProps) {
  return (
    <div className={cn('flex items-center gap-3', className)}>
      <span className="text-sm text-muted-foreground font-nunito">Agent:</span>
      <Select value={value} onValueChange={onChange} disabled={disabled}>
        <SelectTrigger className="w-[280px] font-nunito">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {(Object.keys(AGENTS) as AgentType[]).map(agentId => {
            const agent = AGENTS[agentId];
            const Icon = agent.icon;

            return (
              <SelectItem key={agentId} value={agentId} className="cursor-pointer">
                <div className="flex items-center gap-3 py-1">
                  {/* Icon */}
                  <div className="flex h-8 w-8 items-center justify-center rounded-full bg-primary/10 shrink-0">
                    <Icon className="h-4 w-4 text-primary" />
                  </div>

                  {/* Name & Description */}
                  <div className="flex-1 min-w-0">
                    <span className="font-medium text-sm">{agent.name}</span>
                    <p className="text-xs text-muted-foreground truncate">{agent.description}</p>
                  </div>
                </div>
              </SelectItem>
            );
          })}
        </SelectContent>
      </Select>

      {/* Current Agent Badge */}
      {value !== 'auto' && (
        <Badge variant="outline" className="gap-1 font-nunito">
          <Sparkles className="h-3 w-3" />
          {AGENTS[value].name}
        </Badge>
      )}
    </div>
  );
}

export default AgentSelector;

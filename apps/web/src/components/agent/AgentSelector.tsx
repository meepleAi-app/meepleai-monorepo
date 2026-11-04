/**
 * AgentSelector - chooses the intent of the chat's answers (Task #4, Issue #12; #4404)
 *
 * Features:
 * - Dropdown selector for the three answer intents: Auto, Tutor, Risposta secca
 * - Descriptions and icons
 * - Selection persistence per conversation (the thread stores it)
 *
 * #4404 (ADR-098 §5): the selector used to offer five «agents» (Auto, Tutor, Arbitro, Stratega, Narratore). There is
 * one agent (ADR-094); what the user chooses is the SHAPE of its answer, and the list is the one in
 * `@/lib/chat/answer-intent`, which the backend compares by test. The name of the component and of its `AgentType`
 * stay for now so the callers do not move; they stand for an answer intent.
 *
 * #3393 (finding C9c): the hardcoded "online" status indicators and the no-op
 * 30s poll (which only ever re-set every agent to 'online') were removed — no
 * real per-agent online/offline signal exists in the backend.
 */

'use client';

import { Bot, Sparkles, Zap, ListChecks, type LucideIcon } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import {
  ANSWER_INTENTS,
  ANSWER_INTENT_DESCRIPTIONS,
  ANSWER_INTENT_LABELS,
  type AnswerIntent,
} from '@/lib/chat/answer-intent';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export type AgentType = AnswerIntent;

export interface Agent {
  id: AgentType;
  name: string;
  description: string;
  icon: LucideIcon;
}

export interface AgentSelectorProps {
  /** Currently selected intent (a stored value outside the list must be read with `intentOf` first) */
  value: AgentType;
  /** Callback when the intent changes */
  onChange: (agentId: AgentType) => void;
  /** Additional class name */
  className?: string;
  /** Disabled state */
  disabled?: boolean;
}

// ============================================================================
// Intent definitions
// ============================================================================

export const AGENT_NAMES: Record<AgentType, string> = ANSWER_INTENT_LABELS;

const ICONS: Record<AgentType, LucideIcon> = {
  auto: Zap,
  tutor: Bot,
  secca: ListChecks,
};

const AGENTS: Record<AgentType, Agent> = Object.fromEntries(
  ANSWER_INTENTS.map(id => [
    id,
    {
      id,
      name: ANSWER_INTENT_LABELS[id],
      description: ANSWER_INTENT_DESCRIPTIONS[id],
      icon: ICONS[id],
    },
  ])
) as Record<AgentType, Agent>;

// ============================================================================
// Main Component
// ============================================================================

export function AgentSelector({ value, onChange, className, disabled }: AgentSelectorProps) {
  return (
    <div className={cn('flex items-center gap-3', className)}>
      <span className="text-sm text-muted-foreground font-nunito">Risposta:</span>
      <Select value={value} onValueChange={onChange} disabled={disabled}>
        <SelectTrigger className="w-[280px] font-nunito">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {ANSWER_INTENTS.map(agentId => {
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

      {/* Current intent badge */}
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

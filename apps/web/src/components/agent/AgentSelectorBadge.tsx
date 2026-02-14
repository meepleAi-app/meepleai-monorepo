/**
 * ISSUE-3777: Agent Selector Badge
 * Shows current active agent (Tutor/Arbitro/Decisore) with click-to-switch
 */

'use client';

import { BookOpen, Scale, Trophy } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
import { Button } from '@/components/ui/primitives/button';
import type { AgentType } from '@/lib/api/orchestrator-client';
import { cn } from '@/lib/utils';

interface AgentSelectorBadgeProps {
  currentAgent: AgentType;
  onSwitch?: (agent: AgentType) => void;
  disabled?: boolean;
  showSwitcher?: boolean;
}

const AGENT_CONFIG: Record<
  AgentType,
  { icon: React.ElementType; label: string; color: string; description: string }
> = {
  tutor: {
    icon: BookOpen,
    label: 'Tutor',
    color: 'bg-blue-500/10 text-blue-600 border-blue-500/20 dark:bg-blue-500/20 dark:text-blue-400',
    description: 'Rules, setup, and tutorial questions',
  },
  arbitro: {
    icon: Scale,
    label: 'Arbitro',
    color:
      'bg-yellow-500/10 text-yellow-600 border-yellow-500/20 dark:bg-yellow-500/20 dark:text-yellow-400',
    description: 'Move validation and rules arbitration',
  },
  decisore: {
    icon: Trophy,
    label: 'Decisore',
    color:
      'bg-purple-500/10 text-purple-600 border-purple-500/20 dark:bg-purple-500/20 dark:text-purple-400',
    description: 'Strategic analysis and move suggestions',
  },
};

export function AgentSelectorBadge({
  currentAgent,
  onSwitch,
  disabled = false,
  showSwitcher = false,
}: AgentSelectorBadgeProps) {
  const config = AGENT_CONFIG[currentAgent];
  const Icon = config.icon;

  if (!showSwitcher || !onSwitch) {
    // Simple badge display
    return (
      <TooltipProvider>
        <Tooltip>
          <TooltipTrigger asChild>
            <Badge
              variant="outline"
              className={cn('flex items-center gap-1.5 px-2 py-1', config.color)}
            >
              <Icon className="h-3.5 w-3.5" />
              <span className="font-medium">{config.label}</span>
            </Badge>
          </TooltipTrigger>
          <TooltipContent>
            <p className="text-xs">{config.description}</p>
          </TooltipContent>
        </Tooltip>
      </TooltipProvider>
    );
  }

  // Full switcher with buttons
  return (
    <div className="flex items-center gap-1">
      {Object.entries(AGENT_CONFIG).map(([type, cfg]) => {
        const AgentIcon = cfg.icon;
        const isActive = type === currentAgent;

        return (
          <TooltipProvider key={type}>
            <Tooltip>
              <TooltipTrigger asChild>
                <Button
                  variant={isActive ? 'default' : 'ghost'}
                  size="sm"
                  onClick={() => onSwitch(type as AgentType)}
                  disabled={disabled || isActive}
                  className={cn(
                    'h-8 px-2 gap-1.5',
                    isActive && cfg.color,
                    !isActive && 'hover:bg-accent'
                  )}
                >
                  <AgentIcon className="h-3.5 w-3.5" />
                  <span className="text-xs font-medium">{cfg.label}</span>
                </Button>
              </TooltipTrigger>
              <TooltipContent side="bottom">
                <p className="text-xs">{cfg.description}</p>
              </TooltipContent>
            </Tooltip>
          </TooltipProvider>
        );
      })}
    </div>
  );
}

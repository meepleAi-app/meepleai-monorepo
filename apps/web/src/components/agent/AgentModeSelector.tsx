/**
 * AgentModeSelector Component - Issue #2413
 *
 * Visual selector for AI agent operation modes:
 * - Rules Clarifier: Answers questions about game rules
 * - Strategy Advisor: Suggests optimal moves during gameplay
 * - Setup Assistant: Helps with game setup and initial state
 *
 * @see https://github.com/DegrassiAaron/meepleai-monorepo/issues/2413
 */

'use client';

import { BookOpenCheck, Target, Settings } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Label } from '@/components/ui/primitives/label';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/overlays/tooltip';
import { cn } from '@/lib/utils';

export type AgentMode = 'RulesClarifier' | 'StrategyAdvisor' | 'SetupAssistant';

export interface AgentModeSelectorProps {
  /** Currently selected agent mode */
  value: AgentMode;
  /** Callback when mode selection changes */
  onChange: (mode: AgentMode) => void;
  /** Disable all mode selections */
  disabled?: boolean;
  /** Additional CSS classes */
  className?: string;
}

const MODE_CONFIG = {
  RulesClarifier: {
    icon: BookOpenCheck,
    label: 'Rules Clarifier',
    description: 'Get answers to rule questions and clarifications',
    tooltip: 'Ask about specific rules, card interactions, or game mechanics',
    color: 'text-blue-500',
    bgColor: 'bg-blue-50 dark:bg-blue-950',
  },
  StrategyAdvisor: {
    icon: Target,
    label: 'Strategy Advisor',
    description: 'Receive strategic suggestions and optimal move analysis',
    tooltip: 'Get tactical advice, move recommendations, and strategic insights',
    color: 'text-green-500',
    bgColor: 'bg-green-50 dark:bg-green-950',
  },
  SetupAssistant: {
    icon: Settings,
    label: 'Setup Assistant',
    description: 'Help with game setup, initial configuration, and variants',
    tooltip: 'Assistance with setup steps, component placement, and variant rules',
    color: 'text-purple-500',
    bgColor: 'bg-purple-50 dark:bg-purple-950',
  },
} as const;

export function AgentModeSelector({
  value,
  onChange,
  disabled = false,
  className,
}: AgentModeSelectorProps) {
  return (
    <div className={cn('space-y-4', className)}>
      <Label htmlFor="agent-mode-selector">Agent Mode</Label>
      <TooltipProvider>
        <div
          id="agent-mode-selector"
          className="grid grid-cols-1 md:grid-cols-3 gap-4"
          role="radiogroup"
          aria-label="Select agent mode"
        >
          {Object.entries(MODE_CONFIG).map(([mode, config]) => {
            const Icon = config.icon;
            const isSelected = value === mode;
            const modeKey = mode as AgentMode;

            return (
              <Tooltip key={mode}>
                <TooltipTrigger asChild>
                  <Button
                    type="button"
                    variant="outline"
                    onClick={() => !disabled && onChange(modeKey)}
                    disabled={disabled}
                    className="h-auto p-0"
                    role="radio"
                    aria-checked={isSelected}
                    aria-label={`${config.label}: ${config.description}`}
                  >
                    <Card
                      className={cn(
                        'w-full transition-all border-2',
                        isSelected && 'ring-2 ring-primary border-primary',
                        !isSelected && 'border-border',
                        disabled && 'opacity-50 cursor-not-allowed'
                      )}
                    >
                      <CardHeader className="pb-3">
                        <div className="flex items-center gap-3">
                          <div
                            className={cn(
                              'p-2 rounded-lg',
                              isSelected ? config.bgColor : 'bg-muted'
                            )}
                          >
                            <Icon className={cn('h-5 w-5', isSelected && config.color)} />
                          </div>
                          <CardTitle className="text-base">{config.label}</CardTitle>
                        </div>
                      </CardHeader>
                      <CardContent>
                        <CardDescription className="text-left text-sm">
                          {config.description}
                        </CardDescription>
                      </CardContent>
                    </Card>
                  </Button>
                </TooltipTrigger>
                <TooltipContent>
                  <p>{config.tooltip}</p>
                </TooltipContent>
              </Tooltip>
            );
          })}
        </div>
      </TooltipProvider>
    </div>
  );
}

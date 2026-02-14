/**
 * AgentModelInfo - AI Model Information Display
 * Issue #4184 - Agent-Specific Metadata & Status Display
 *
 * Displays AI model name, badge, and parameters in tooltip (temperature, max_tokens).
 */

'use client';

import React from 'react';

import { Bot, Cpu } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/overlays/tooltip';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export interface ModelParameters {
  /** Temperature setting (0-2) */
  temperature?: number;
  /** Maximum tokens */
  maxTokens?: number;
  /** Top P sampling */
  topP?: number;
  /** Additional custom parameters */
  [key: string]: unknown;
}

export interface AgentModelInfoProps {
  /** Model name (e.g., "GPT-4o-mini", "Claude 3.5 Sonnet", "Gemini Pro") */
  modelName: string;
  /** Model parameters */
  parameters?: ModelParameters;
  /** Show model icon badge */
  showIcon?: boolean;
  /** Size variant */
  size?: 'sm' | 'md';
  /** Custom className */
  className?: string;
}

// ============================================================================
// Helper Functions
// ============================================================================

/**
 * Get model icon based on model name
 */
function getModelIcon(modelName: string): typeof Bot | typeof Cpu {
  const name = modelName.toLowerCase();
  if (name.includes('gpt') || name.includes('claude') || name.includes('gemini')) {
    return Bot;
  }
  return Cpu;
}

/**
 * Get model badge variant based on model family
 */
function getModelVariant(modelName: string): 'default' | 'secondary' | 'outline' {
  const name = modelName.toLowerCase();
  if (name.includes('gpt-4') || name.includes('claude')) return 'default';
  if (name.includes('gemini')) return 'secondary';
  return 'outline';
}

// ============================================================================
// Component
// ============================================================================

export const AgentModelInfo = React.memo(function AgentModelInfo({
  modelName,
  parameters,
  showIcon = true,
  size = 'sm',
  className,
}: AgentModelInfoProps) {
  const Icon = getModelIcon(modelName);
  const variant = getModelVariant(modelName);

  // Format parameters for tooltip
  const parametersList = parameters
    ? [
        parameters.temperature !== undefined && `Temperature: ${parameters.temperature}`,
        parameters.maxTokens !== undefined && `Max Tokens: ${parameters.maxTokens.toLocaleString()}`,
        parameters.topP !== undefined && `Top P: ${parameters.topP}`,
      ].filter(Boolean)
    : [];

  return (
    <TooltipProvider>
      <Tooltip>
        <TooltipTrigger asChild>
          <span className={cn('inline-flex items-center gap-1.5', className)} data-testid="agent-model-info">
            {showIcon && <Icon className={cn('flex-shrink-0', size === 'sm' ? 'w-3.5 h-3.5' : 'w-4 h-4')} aria-hidden="true" />}
            <Badge variant={variant} className={size === 'sm' ? 'text-[10px] px-1.5 py-0' : undefined}>
              {modelName}
            </Badge>
          </span>
        </TooltipTrigger>
        {parametersList.length > 0 && (
          <TooltipContent>
            <div className="space-y-1">
              <p className="text-xs font-semibold">Model Parameters</p>
              {parametersList.map((param, idx) => (
                <p key={idx} className="text-[10px] text-muted-foreground">
                  {param}
                </p>
              ))}
            </div>
          </TooltipContent>
        )}
      </Tooltip>
    </TooltipProvider>
  );
});

AgentModelInfo.displayName = 'AgentModelInfo';

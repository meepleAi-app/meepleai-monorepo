'use client';

import React from 'react';

import { Blend, MessageSquare, GitBranch } from 'lucide-react';

import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { Slider } from '@/components/ui/primitives/slider';
import { cn } from '@/lib/utils';

import type { StrategySpecificProps } from './types';

/**
 * StrategySpecificSettings Component
 *
 * Controls for strategy-specific parameters:
 * - Hybrid Alpha (keyword vs vector weight)
 * - Context Window (for Contextual strategy)
 * - Max Hops (for Agentic strategy)
 */
export function StrategySpecificSettings({
  settings,
  onChange,
  activeStrategy,
  className,
}: StrategySpecificProps): React.JSX.Element {
  const handleHybridAlphaChange = (value: number[]) => {
    onChange({ ...settings, hybridAlpha: value[0] });
  };

  const handleContextWindowChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const value = Math.max(1, Math.min(20, parseInt(e.target.value) || 1));
    onChange({ ...settings, contextWindow: value });
  };

  const handleMaxHopsChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const value = Math.max(1, Math.min(10, parseInt(e.target.value) || 1));
    onChange({ ...settings, maxHops: value });
  };

  const showHybridAlpha = activeStrategy === 'Hybrid';
  const showContextWindow = activeStrategy === 'Contextual';
  const showMaxHops = activeStrategy === 'Agentic' || activeStrategy === 'MultiQuery';

  if (!showHybridAlpha && !showContextWindow && !showMaxHops) {
    return (
      <div className={cn('text-sm text-muted-foreground text-center py-4', className)}>
        No strategy-specific settings for <span className="font-medium">{activeStrategy}</span>
      </div>
    );
  }

  return (
    <div className={cn('space-y-6', className)}>
      {/* Hybrid Alpha */}
      {showHybridAlpha && (
        <div className="space-y-2">
          <div className="flex items-center justify-between">
            <TooltipProvider>
              <Tooltip>
                <TooltipTrigger asChild>
                  <Label className="flex items-center gap-2 cursor-help">
                    <Blend className="h-4 w-4 text-purple-500" />
                    Hybrid Alpha
                  </Label>
                </TooltipTrigger>
                <TooltipContent side="right" className="max-w-xs">
                  <p>
                    Controls the balance between keyword (BM25) and vector (semantic) search.
                    0 = pure keyword, 1 = pure vector, 0.5 = balanced hybrid.
                  </p>
                </TooltipContent>
              </Tooltip>
            </TooltipProvider>
            <span className="text-sm font-mono text-muted-foreground">
              {settings.hybridAlpha.toFixed(2)}
            </span>
          </div>
          <Slider
            value={[settings.hybridAlpha]}
            onValueChange={handleHybridAlphaChange}
            min={0}
            max={1}
            step={0.05}
            className="w-full"
          />
          <div className="flex justify-between text-xs text-muted-foreground">
            <span>Keyword (BM25)</span>
            <span>Vector (Semantic)</span>
          </div>
        </div>
      )}

      {/* Context Window */}
      {showContextWindow && (
        <div className="space-y-2">
          <div className="flex items-center justify-between">
            <TooltipProvider>
              <Tooltip>
                <TooltipTrigger asChild>
                  <Label className="flex items-center gap-2 cursor-help">
                    <MessageSquare className="h-4 w-4 text-blue-500" />
                    Context Window
                  </Label>
                </TooltipTrigger>
                <TooltipContent side="right" className="max-w-xs">
                  <p>
                    Number of previous conversation messages to include as context.
                    Higher values provide more context but use more tokens.
                  </p>
                </TooltipContent>
              </Tooltip>
            </TooltipProvider>
            <Input
              type="number"
              value={settings.contextWindow}
              onChange={handleContextWindowChange}
              min={1}
              max={20}
              className="w-16 h-8 text-right font-mono"
            />
          </div>
          <p className="text-xs text-muted-foreground">
            Include last {settings.contextWindow} messages in context
          </p>
        </div>
      )}

      {/* Max Hops */}
      {showMaxHops && (
        <div className="space-y-2">
          <div className="flex items-center justify-between">
            <TooltipProvider>
              <Tooltip>
                <TooltipTrigger asChild>
                  <Label className="flex items-center gap-2 cursor-help">
                    <GitBranch className="h-4 w-4 text-green-500" />
                    Max Hops
                  </Label>
                </TooltipTrigger>
                <TooltipContent side="right" className="max-w-xs">
                  <p>
                    Maximum number of retrieval iterations. More hops allow for deeper
                    exploration but increase latency. Used by Agentic and MultiQuery strategies.
                  </p>
                </TooltipContent>
              </Tooltip>
            </TooltipProvider>
            <Input
              type="number"
              value={settings.maxHops}
              onChange={handleMaxHopsChange}
              min={1}
              max={10}
              className="w-16 h-8 text-right font-mono"
            />
          </div>
          <p className="text-xs text-muted-foreground">
            Maximum {settings.maxHops} retrieval iterations
          </p>
        </div>
      )}
    </div>
  );
}

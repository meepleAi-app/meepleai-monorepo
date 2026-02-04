'use client';

import React from 'react';

import { Thermometer, Hash, Percent, Type } from 'lucide-react';

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

import type { GenerationParamsProps } from './types';

/**
 * GenerationParams Component
 *
 * Controls for LLM generation parameters:
 * - Temperature (0-2)
 * - Top-K (1-100)
 * - Top-P (0-1)
 * - Max Tokens (100-4000)
 */
export function GenerationParams({
  params,
  onChange,
  className,
}: GenerationParamsProps): React.JSX.Element {
  const handleTemperatureChange = (value: number[]) => {
    onChange({ ...params, temperature: value[0] });
  };

  const handleTopKChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const value = Math.max(1, Math.min(100, parseInt(e.target.value) || 1));
    onChange({ ...params, topK: value });
  };

  const handleTopPChange = (value: number[]) => {
    onChange({ ...params, topP: value[0] });
  };

  const handleMaxTokensChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const value = Math.max(100, Math.min(4000, parseInt(e.target.value) || 100));
    onChange({ ...params, maxTokens: value });
  };

  return (
    <div className={cn('space-y-6', className)}>
      {/* Temperature */}
      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <TooltipProvider>
            <Tooltip>
              <TooltipTrigger asChild>
                <Label className="flex items-center gap-2 cursor-help">
                  <Thermometer className="h-4 w-4 text-orange-500" />
                  Temperature
                </Label>
              </TooltipTrigger>
              <TooltipContent side="right" className="max-w-xs">
                <p>
                  Controls randomness in output. Lower values (0.0-0.3) produce more focused,
                  deterministic responses. Higher values (0.7-2.0) increase creativity and variety.
                </p>
              </TooltipContent>
            </Tooltip>
          </TooltipProvider>
          <span className="text-sm font-mono text-muted-foreground">
            {params.temperature.toFixed(1)}
          </span>
        </div>
        <Slider
          value={[params.temperature]}
          onValueChange={handleTemperatureChange}
          min={0}
          max={2}
          step={0.1}
          className="w-full"
        />
        <div className="flex justify-between text-xs text-muted-foreground">
          <span>Focused</span>
          <span>Creative</span>
        </div>
      </div>

      {/* Top-K */}
      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <TooltipProvider>
            <Tooltip>
              <TooltipTrigger asChild>
                <Label className="flex items-center gap-2 cursor-help">
                  <Hash className="h-4 w-4 text-blue-500" />
                  Top-K
                </Label>
              </TooltipTrigger>
              <TooltipContent side="right" className="max-w-xs">
                <p>
                  Limits vocabulary to the top K most likely tokens. Lower values increase
                  coherence but may reduce variety. Set to 1 for greedy decoding.
                </p>
              </TooltipContent>
            </Tooltip>
          </TooltipProvider>
          <Input
            type="number"
            value={params.topK}
            onChange={handleTopKChange}
            min={1}
            max={100}
            className="w-20 h-8 text-right font-mono"
          />
        </div>
      </div>

      {/* Top-P */}
      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <TooltipProvider>
            <Tooltip>
              <TooltipTrigger asChild>
                <Label className="flex items-center gap-2 cursor-help">
                  <Percent className="h-4 w-4 text-purple-500" />
                  Top-P (Nucleus)
                </Label>
              </TooltipTrigger>
              <TooltipContent side="right" className="max-w-xs">
                <p>
                  Selects tokens from the smallest set whose cumulative probability exceeds P.
                  Values near 1.0 consider more tokens; lower values are more focused.
                </p>
              </TooltipContent>
            </Tooltip>
          </TooltipProvider>
          <span className="text-sm font-mono text-muted-foreground">
            {params.topP.toFixed(2)}
          </span>
        </div>
        <Slider
          value={[params.topP]}
          onValueChange={handleTopPChange}
          min={0}
          max={1}
          step={0.05}
          className="w-full"
        />
      </div>

      {/* Max Tokens */}
      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <TooltipProvider>
            <Tooltip>
              <TooltipTrigger asChild>
                <Label className="flex items-center gap-2 cursor-help">
                  <Type className="h-4 w-4 text-green-500" />
                  Max Tokens
                </Label>
              </TooltipTrigger>
              <TooltipContent side="right" className="max-w-xs">
                <p>
                  Maximum number of tokens in the generated response. Longer responses
                  require more tokens and cost more.
                </p>
              </TooltipContent>
            </Tooltip>
          </TooltipProvider>
          <Input
            type="number"
            value={params.maxTokens}
            onChange={handleMaxTokensChange}
            min={100}
            max={4000}
            step={100}
            className="w-24 h-8 text-right font-mono"
          />
        </div>
        <div className="text-xs text-muted-foreground text-right">
          Range: 100 - 4,000
        </div>
      </div>
    </div>
  );
}

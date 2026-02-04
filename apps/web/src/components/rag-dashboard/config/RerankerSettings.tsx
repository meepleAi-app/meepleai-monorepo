'use client';

import React from 'react';

import { ArrowUpDown, Layers } from 'lucide-react';

import { Switch } from '@/components/ui/forms/switch';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { cn } from '@/lib/utils';

import { RERANKER_MODELS } from './types';

import type { RerankerSettingsProps, RerankerModelId } from './types';

/**
 * RerankerSettings Component
 *
 * Controls for reranker configuration:
 * - Enable/Disable toggle
 * - Model selection
 * - Top-N reranked results
 */
export function RerankerSettings({
  settings,
  onChange,
  className,
}: RerankerSettingsProps): React.JSX.Element {
  const handleEnabledChange = (enabled: boolean) => {
    onChange({ ...settings, enabled });
  };

  const handleModelChange = (model: string) => {
    onChange({ ...settings, model: model as RerankerModelId });
  };

  const handleTopNChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const value = Math.max(3, Math.min(20, parseInt(e.target.value) || 3));
    onChange({ ...settings, topN: value });
  };

  return (
    <div className={cn('space-y-6', className)}>
      {/* Enable Toggle */}
      <div className="flex items-center justify-between">
        <TooltipProvider>
          <Tooltip>
            <TooltipTrigger asChild>
              <Label className="flex items-center gap-2 cursor-help">
                <ArrowUpDown className="h-4 w-4 text-blue-500" />
                Enable Reranker
              </Label>
            </TooltipTrigger>
            <TooltipContent side="right" className="max-w-xs">
              <p>
                Reranking improves retrieval quality by re-scoring initial results
                with a cross-encoder model. Adds latency but significantly improves relevance.
              </p>
            </TooltipContent>
          </Tooltip>
        </TooltipProvider>
        <Switch
          checked={settings.enabled}
          onCheckedChange={handleEnabledChange}
        />
      </div>

      {/* Model Selection */}
      <div className={cn('space-y-2', !settings.enabled && 'opacity-50 pointer-events-none')}>
        <TooltipProvider>
          <Tooltip>
            <TooltipTrigger asChild>
              <Label className="flex items-center gap-2 cursor-help">
                <Layers className="h-4 w-4 text-purple-500" />
                Reranker Model
              </Label>
            </TooltipTrigger>
            <TooltipContent side="right" className="max-w-xs">
              <p>
                Choose between speed (MiniLM L6) and accuracy (BGE Large).
                Larger models are slower but produce better rankings.
              </p>
            </TooltipContent>
          </Tooltip>
        </TooltipProvider>
        <Select
          value={settings.model}
          onValueChange={handleModelChange}
          disabled={!settings.enabled}
        >
          <SelectTrigger className="w-full">
            <SelectValue placeholder="Select model" />
          </SelectTrigger>
          <SelectContent>
            {RERANKER_MODELS.map((model) => (
              <SelectItem key={model.id} value={model.id}>
                {model.name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {/* Top-N */}
      <div className={cn('space-y-2', !settings.enabled && 'opacity-50 pointer-events-none')}>
        <div className="flex items-center justify-between">
          <TooltipProvider>
            <Tooltip>
              <TooltipTrigger asChild>
                <Label className="flex items-center gap-2 cursor-help">
                  Rerank Top-N
                </Label>
              </TooltipTrigger>
              <TooltipContent side="right" className="max-w-xs">
                <p>
                  Number of results to rerank. Should be greater than Top Results
                  to allow the reranker to filter effectively.
                </p>
              </TooltipContent>
            </Tooltip>
          </TooltipProvider>
          <Input
            type="number"
            value={settings.topN}
            onChange={handleTopNChange}
            min={3}
            max={20}
            disabled={!settings.enabled}
            className="w-16 h-8 text-right font-mono"
          />
        </div>
        <p className="text-xs text-muted-foreground">
          Reranks top {settings.topN} initial results before final selection
        </p>
      </div>
    </div>
  );
}

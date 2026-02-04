'use client';

import React from 'react';

import { Cpu, Shield, Microscope } from 'lucide-react';

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
import { Label } from '@/components/ui/primitives/label';
import { cn } from '@/lib/utils';

import { LLM_MODELS } from './types';

import type { ModelSelectorProps, LlmModelId } from './types';

/**
 * ModelSelector Component
 *
 * Controls for LLM model selection:
 * - Primary Model
 * - Fallback Model (optional)
 * - Evaluation Model (for CRAG, optional)
 */
export function ModelSelector({
  selection,
  onChange,
  showEvaluationModel = false,
  className,
}: ModelSelectorProps): React.JSX.Element {
  const handlePrimaryChange = (model: string) => {
    onChange({ ...selection, primaryModel: model as LlmModelId });
  };

  const handleFallbackChange = (model: string) => {
    onChange({
      ...selection,
      fallbackModel: model === 'none' ? null : (model as LlmModelId),
    });
  };

  const handleEvaluationChange = (model: string) => {
    onChange({
      ...selection,
      evaluationModel: model === 'none' ? null : (model as LlmModelId),
    });
  };

  // Group models by provider
  type LlmModel = (typeof LLM_MODELS)[number];
  const modelsByProvider = LLM_MODELS.reduce<Record<string, LlmModel[]>>(
    (acc, model) => {
      if (!acc[model.provider]) acc[model.provider] = [];
      acc[model.provider].push(model);
      return acc;
    },
    {}
  );

  return (
    <div className={cn('space-y-6', className)}>
      {/* Primary Model */}
      <div className="space-y-2">
        <TooltipProvider>
          <Tooltip>
            <TooltipTrigger asChild>
              <Label className="flex items-center gap-2 cursor-help">
                <Cpu className="h-4 w-4 text-blue-500" />
                Primary Model
              </Label>
            </TooltipTrigger>
            <TooltipContent side="right" className="max-w-xs">
              <p>
                The main LLM used for generation. Choose based on your needs:
                GPT-4o for quality, Claude for nuance, Gemini for speed.
              </p>
            </TooltipContent>
          </Tooltip>
        </TooltipProvider>
        <Select value={selection.primaryModel} onValueChange={handlePrimaryChange}>
          <SelectTrigger className="w-full">
            <SelectValue placeholder="Select model" />
          </SelectTrigger>
          <SelectContent>
            {Object.entries(modelsByProvider).map(([provider, models]) => (
              <React.Fragment key={provider}>
                <div className="px-2 py-1.5 text-xs font-semibold text-muted-foreground">
                  {provider}
                </div>
                {models.map((model) => (
                  <SelectItem key={model.id} value={model.id}>
                    {model.name}
                  </SelectItem>
                ))}
              </React.Fragment>
            ))}
          </SelectContent>
        </Select>
      </div>

      {/* Fallback Model */}
      <div className="space-y-2">
        <TooltipProvider>
          <Tooltip>
            <TooltipTrigger asChild>
              <Label className="flex items-center gap-2 cursor-help">
                <Shield className="h-4 w-4 text-green-500" />
                Fallback Model
              </Label>
            </TooltipTrigger>
            <TooltipContent side="right" className="max-w-xs">
              <p>
                Used when the primary model fails or is unavailable.
                Consider a faster/cheaper model from a different provider.
              </p>
            </TooltipContent>
          </Tooltip>
        </TooltipProvider>
        <Select
          value={selection.fallbackModel ?? 'none'}
          onValueChange={handleFallbackChange}
        >
          <SelectTrigger className="w-full">
            <SelectValue placeholder="Select fallback" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="none">
              <span className="text-muted-foreground">No fallback</span>
            </SelectItem>
            {Object.entries(modelsByProvider).map(([provider, models]) => (
              <React.Fragment key={provider}>
                <div className="px-2 py-1.5 text-xs font-semibold text-muted-foreground">
                  {provider}
                </div>
                {models.map((model) => (
                  <SelectItem
                    key={model.id}
                    value={model.id}
                    disabled={model.id === selection.primaryModel}
                  >
                    {model.name}
                  </SelectItem>
                ))}
              </React.Fragment>
            ))}
          </SelectContent>
        </Select>
      </div>

      {/* Evaluation Model (for CRAG) */}
      {showEvaluationModel && (
        <div className="space-y-2">
          <TooltipProvider>
            <Tooltip>
              <TooltipTrigger asChild>
                <Label className="flex items-center gap-2 cursor-help">
                  <Microscope className="h-4 w-4 text-purple-500" />
                  Evaluation Model
                </Label>
              </TooltipTrigger>
              <TooltipContent side="right" className="max-w-xs">
                <p>
                  Used in CRAG (Corrective RAG) to evaluate retrieval quality
                  and decide whether to refine the search. A capable model improves accuracy.
                </p>
              </TooltipContent>
            </Tooltip>
          </TooltipProvider>
          <Select
            value={selection.evaluationModel ?? 'none'}
            onValueChange={handleEvaluationChange}
          >
            <SelectTrigger className="w-full">
              <SelectValue placeholder="Select evaluation model" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="none">
                <span className="text-muted-foreground">Use primary model</span>
              </SelectItem>
              {Object.entries(modelsByProvider).map(([provider, models]) => (
                <React.Fragment key={provider}>
                  <div className="px-2 py-1.5 text-xs font-semibold text-muted-foreground">
                    {provider}
                  </div>
                  {models.map((model) => (
                    <SelectItem key={model.id} value={model.id}>
                      {model.name}
                    </SelectItem>
                  ))}
                </React.Fragment>
              ))}
            </SelectContent>
          </Select>
        </div>
      )}
    </div>
  );
}

/**
 * Model Selector - AI model selection with tier filtering and cost display
 * Issue #4775: ModelSelector API Integration
 *
 * Features:
 * - Fetch available models from API via React Query
 * - Provider icons and badges
 * - Cost per 1K tokens display
 * - Tier-based filtering
 * - Controlled component (value/onChange props)
 * - Loading, empty, and error states
 */

'use client';

import { useMemo } from 'react';
import { Loader2, AlertCircle, Sparkles, Zap } from 'lucide-react';

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { useAiModels } from '@/hooks/queries/useAiModels';
import type { AiModelDto, AiProvider } from '@/lib/api/schemas/ai-models.schemas';

export interface ModelSelectorProps {
  /** Currently selected model ID */
  value?: string;
  /** Callback when model changes */
  onChange: (modelId: string, model: AiModelDto | null) => void;
  /** Whether the selector is disabled */
  disabled?: boolean;
  /** Optional placeholder text */
  placeholder?: string;
  /** Optional className for styling */
  className?: string;
}

/** Provider display configuration */
const PROVIDER_CONFIG: Record<AiProvider, { label: string; icon: string; color: string }> = {
  openai: { label: 'OpenAI', icon: '🟢', color: 'text-green-600 dark:text-green-400' },
  anthropic: { label: 'Anthropic', icon: '🟤', color: 'text-amber-700 dark:text-amber-400' },
  google: { label: 'Google', icon: '🔵', color: 'text-blue-600 dark:text-blue-400' },
  meta: { label: 'Meta', icon: '🔷', color: 'text-blue-500 dark:text-blue-300' },
  deepseek: { label: 'DeepSeek', icon: '🟣', color: 'text-purple-600 dark:text-purple-400' },
  openrouter: { label: 'OpenRouter', icon: '🔀', color: 'text-indigo-600 dark:text-indigo-400' },
};

function formatCost(cost: number): string {
  if (cost === 0) return 'Free';
  if (cost < 0.001) return `$${cost.toFixed(4)}`;
  return `$${cost.toFixed(3)}`;
}

export function ModelSelector({
  value,
  onChange,
  disabled = false,
  placeholder = 'Choose AI model...',
  className,
}: ModelSelectorProps) {
  // Fetch active models
  const { data: modelsData, isLoading, error } = useAiModels({ status: 'active', page: 1, pageSize: 50 });

  const models = useMemo(() => modelsData?.items ?? [], [modelsData]);

  // Find selected model for display
  const selectedModel = models.find((m) => m.id === value);

  const handleValueChange = (modelId: string) => {
    const model = models.find((m) => m.id === modelId) ?? null;
    onChange(modelId, model);
  };

  if (error) {
    return (
      <div className="p-3 bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg">
        <div className="flex items-center gap-2">
          <AlertCircle className="h-4 w-4 text-red-500" />
          <p className="text-sm text-red-700 dark:text-red-300">
            Failed to load models. Please try again.
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-2">
      <label className="block text-sm font-medium">
        AI Model
        <span className="ml-1 text-red-500">*</span>
      </label>

      <Select
        value={value}
        onValueChange={handleValueChange}
        disabled={disabled || isLoading}
      >
        <SelectTrigger className={className} aria-label="Select AI model">
          {isLoading ? (
            <div className="flex items-center gap-2">
              <Loader2 className="h-4 w-4 animate-spin" />
              <span className="text-muted-foreground">Loading models...</span>
            </div>
          ) : selectedModel ? (
            <div className="flex items-center gap-2">
              <span>{PROVIDER_CONFIG[selectedModel.provider]?.icon ?? '🤖'}</span>
              <span>{selectedModel.displayName}</span>
              <span className="text-xs text-muted-foreground">
                {formatCost(selectedModel.cost.inputCostPer1kTokens)}/1K
              </span>
            </div>
          ) : (
            <SelectValue placeholder={placeholder} />
          )}
        </SelectTrigger>

        <SelectContent>
          {models.length > 0 ? (
            models.map((model) => {
              const provider = PROVIDER_CONFIG[model.provider];
              return (
                <SelectItem
                  key={model.id}
                  value={model.id}
                  className="cursor-pointer"
                >
                  <div className="flex items-center justify-between gap-3 w-full">
                    <div className="flex items-center gap-2 flex-1 min-w-0">
                      <span className="text-base shrink-0">{provider?.icon ?? '🤖'}</span>
                      <div className="flex flex-col min-w-0">
                        <span className="font-medium truncate">{model.displayName}</span>
                        <span className="text-xs text-muted-foreground truncate">
                          {provider?.label ?? model.provider}
                        </span>
                      </div>
                    </div>
                    <div className="flex items-center gap-1.5 shrink-0">
                      {model.isPrimary && (
                        <span className="px-1.5 py-0.5 rounded text-[10px] bg-amber-100 dark:bg-amber-500/20 text-amber-700 dark:text-amber-300">
                          <Zap className="h-2.5 w-2.5 inline mr-0.5" />
                          Default
                        </span>
                      )}
                      <span className="text-xs text-muted-foreground whitespace-nowrap">
                        {formatCost(model.cost.inputCostPer1kTokens)}/1K
                      </span>
                    </div>
                  </div>
                </SelectItem>
              );
            })
          ) : (
            <div className="p-3 text-sm text-muted-foreground text-center">
              <div className="flex flex-col items-center gap-2">
                <Sparkles className="h-8 w-8 text-muted-foreground/50" />
                <p>No models available</p>
              </div>
            </div>
          )}
        </SelectContent>
      </Select>

      {selectedModel && (
        <div className="flex items-center gap-4 text-xs text-muted-foreground">
          <span>Input: {formatCost(selectedModel.cost.inputCostPer1kTokens)}/1K tokens</span>
          <span>Output: {formatCost(selectedModel.cost.outputCostPer1kTokens)}/1K tokens</span>
        </div>
      )}

      {!value && !isLoading && (
        <p className="text-xs text-muted-foreground">
          Model determines response quality and cost. Default model is recommended for most uses.
        </p>
      )}
    </div>
  );
}

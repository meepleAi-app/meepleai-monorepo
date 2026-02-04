'use client';

/**
 * StrategyModelMappingEditor Component
 * Issue #3440: Admin UI for tier-strategy configuration
 *
 * Displays and allows editing strategy-to-model mappings.
 */

import { useState } from 'react';

import { Pencil, Save, X, ShieldCheck, Info } from 'lucide-react';

import { toast } from '@/components/layout/Toast';
import { Badge } from '@/components/ui/data-display/badge';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/data-display/tooltip';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { useUpdateStrategyModelMapping } from '@/hooks/queries/useTierStrategy';
import type { StrategyModelMappingDto } from '@/lib/api';
import { LLM_PROVIDERS } from '@/lib/api/schemas/tier-strategy.schemas';
import { cn } from '@/lib/utils';

interface StrategyModelMappingEditorProps {
  mappings: StrategyModelMappingDto[];
}

interface EditingState {
  strategy: string;
  provider: string;
  primaryModel: string;
  fallbackModels: string;
}

const STRATEGY_COLORS: Record<string, string> = {
  FAST: 'border-l-green-500',
  BALANCED: 'border-l-blue-500',
  PRECISE: 'border-l-purple-500',
  EXPERT: 'border-l-orange-500',
  CONSENSUS: 'border-l-pink-500',
  CUSTOM: 'border-l-amber-500',
};

export function StrategyModelMappingEditor({ mappings }: StrategyModelMappingEditorProps) {
  const [editing, setEditing] = useState<EditingState | null>(null);
  const updateMutation = useUpdateStrategyModelMapping();

  const handleEdit = (mapping: StrategyModelMappingDto) => {
    setEditing({
      strategy: mapping.strategy,
      provider: mapping.provider,
      primaryModel: mapping.primaryModel,
      fallbackModels: mapping.fallbackModels.join(', '),
    });
  };

  const handleCancel = () => {
    setEditing(null);
  };

  const handleSave = async () => {
    if (!editing) return;

    if (!editing.provider || !editing.primaryModel) {
      toast.error('Provider and primary model are required');
      return;
    }

    try {
      const fallbackModels = editing.fallbackModels
        .split(',')
        .map(m => m.trim())
        .filter(m => m.length > 0);

      await updateMutation.mutateAsync({
        strategy: editing.strategy,
        provider: editing.provider,
        primaryModel: editing.primaryModel,
        fallbackModels: fallbackModels.length > 0 ? fallbackModels : undefined,
      });

      toast.success(`Updated ${editing.strategy} model mapping`);
      setEditing(null);
    } catch (_error) {
      toast.error('Failed to update mapping');
    }
  };

  return (
    <TooltipProvider>
      <div className="space-y-4">
        {mappings.map(mapping => {
          const isEditing = editing?.strategy === mapping.strategy;
          const isUpdating =
            updateMutation.isPending && updateMutation.variables?.strategy === mapping.strategy;

          return (
            <div
              key={mapping.strategy}
              className={cn(
                'border rounded-lg p-4 border-l-4 transition-colors',
                STRATEGY_COLORS[mapping.strategy] || 'border-l-gray-500',
                isEditing && 'ring-2 ring-primary'
              )}
            >
              <div className="flex items-start justify-between gap-4">
                <div className="flex-1">
                  {/* Strategy Header */}
                  <div className="flex items-center gap-2 mb-3">
                    <h3 className="text-lg font-semibold">{mapping.strategy}</h3>
                    {mapping.adminOnly && (
                      <Tooltip>
                        <TooltipTrigger>
                          <ShieldCheck className="h-4 w-4 text-amber-500" />
                        </TooltipTrigger>
                        <TooltipContent>Admin-only strategy</TooltipContent>
                      </Tooltip>
                    )}
                    {mapping.isDefault && (
                      <Badge variant="outline" className="text-xs">
                        default
                      </Badge>
                    )}
                    {!mapping.isCustomizable && (
                      <Tooltip>
                        <TooltipTrigger>
                          <Info className="h-4 w-4 text-muted-foreground" />
                        </TooltipTrigger>
                        <TooltipContent>This strategy has a fixed model configuration</TooltipContent>
                      </Tooltip>
                    )}
                  </div>

                  {/* Configuration Fields */}
                  {isEditing ? (
                    <div className="space-y-3">
                      <div className="grid grid-cols-2 gap-4">
                        <div>
                          <label className="text-sm font-medium text-muted-foreground">
                            Provider
                          </label>
                          <Select
                            value={editing.provider}
                            onValueChange={(value: string) =>
                              setEditing(prev => (prev ? { ...prev, provider: value } : null))
                            }
                          >
                            <SelectTrigger>
                              <SelectValue placeholder="Select provider" />
                            </SelectTrigger>
                            <SelectContent>
                              {LLM_PROVIDERS.map(provider => (
                                <SelectItem key={provider} value={provider}>
                                  {provider}
                                </SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
                        </div>
                        <div>
                          <label className="text-sm font-medium text-muted-foreground">
                            Primary Model
                          </label>
                          <Input
                            value={editing.primaryModel}
                            onChange={(e: React.ChangeEvent<HTMLInputElement>) =>
                              setEditing(prev =>
                                prev ? { ...prev, primaryModel: e.target.value } : null
                              )
                            }
                            placeholder="e.g., anthropic/claude-sonnet-4.5"
                          />
                        </div>
                      </div>
                      <div>
                        <label className="text-sm font-medium text-muted-foreground">
                          Fallback Models (comma-separated)
                        </label>
                        <Input
                          value={editing.fallbackModels}
                          onChange={(e: React.ChangeEvent<HTMLInputElement>) =>
                            setEditing(prev =>
                              prev ? { ...prev, fallbackModels: e.target.value } : null
                            )
                          }
                          placeholder="e.g., openai/gpt-4o, google/gemini-pro"
                        />
                      </div>
                    </div>
                  ) : (
                    <div className="grid grid-cols-2 gap-x-8 gap-y-2 text-sm">
                      <div>
                        <span className="text-muted-foreground">Provider:</span>{' '}
                        <span className="font-medium">{mapping.provider}</span>
                      </div>
                      <div>
                        <span className="text-muted-foreground">Primary Model:</span>{' '}
                        <code className="text-xs bg-muted px-1 py-0.5 rounded">
                          {mapping.primaryModel}
                        </code>
                      </div>
                      <div className="col-span-2">
                        <span className="text-muted-foreground">Fallbacks:</span>{' '}
                        {mapping.fallbackModels.length > 0 ? (
                          <span className="font-medium">
                            {mapping.fallbackModels.map((m) => (
                              <code key={m} className="text-xs bg-muted px-1 py-0.5 rounded ml-1">
                                {m}
                              </code>
                            ))}
                          </span>
                        ) : (
                          <span className="text-muted-foreground italic">None</span>
                        )}
                      </div>
                    </div>
                  )}
                </div>

                {/* Action Buttons */}
                <div className="flex items-center gap-2">
                  {isEditing ? (
                    <>
                      <Button
                        size="sm"
                        variant="ghost"
                        onClick={handleCancel}
                        disabled={isUpdating}
                      >
                        <X className="h-4 w-4" />
                      </Button>
                      <Button size="sm" onClick={handleSave} disabled={isUpdating}>
                        <Save className="h-4 w-4 mr-1" />
                        Save
                      </Button>
                    </>
                  ) : (
                    <Button
                      size="sm"
                      variant="outline"
                      onClick={() => handleEdit(mapping)}
                      disabled={editing !== null}
                    >
                      <Pencil className="h-4 w-4 mr-1" />
                      Edit
                    </Button>
                  )}
                </div>
              </div>
            </div>
          );
        })}
      </div>

      {/* Help Text */}
      <div className="mt-6 bg-muted/50 rounded-lg p-4 border border-border/50">
        <h4 className="font-medium text-foreground mb-2">Model Configuration Guide</h4>
        <ul className="text-sm text-muted-foreground space-y-1">
          <li>
            • <strong>Provider</strong>: The LLM provider (OpenRouter, Anthropic, DeepSeek, etc.)
          </li>
          <li>
            • <strong>Primary Model</strong>: Main model used for the strategy (use format:
            provider/model-name)
          </li>
          <li>
            • <strong>Fallback Models</strong>: Backup models if primary fails (comma-separated)
          </li>
          <li>• Changes take effect immediately for new requests</li>
        </ul>
      </div>
    </TooltipProvider>
  );
}

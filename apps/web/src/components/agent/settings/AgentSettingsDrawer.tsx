/**
 * AgentSettingsDrawer - Runtime agent configuration drawer
 * Issue #3250 (FRONT-014) — Rewritten to use dynamic models from API
 *
 * Features:
 * - Dynamic model selector (fetched from GET /api/v1/models, tier-filtered)
 * - Cost estimate per model
 * - Temperature slider (0-2 range)
 * - Max tokens input
 * - Agent name editing
 * - PATCH config via /api/v1/agents/:id/configuration
 */

'use client';

import { useState, useCallback, useEffect } from 'react';

import {
  Settings,
  RotateCcw,
  Loader2,
  Zap,
  Thermometer,
  DollarSign,
} from 'lucide-react';
import { toast } from 'sonner';

import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetFooter } from '@/components/ui/navigation/sheet';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { Slider } from '@/components/ui/primitives/slider';
import { useAvailableModels, useAgentConfiguration, useUpdateAgentConfiguration } from '@/hooks/queries/useModels';
import { api } from '@/lib/api';

// ============================================================================
// Types
// ============================================================================

interface AgentSettingsDrawerProps {
  /** Whether drawer is open */
  isOpen: boolean;
  /** Callback to close drawer */
  onClose: () => void;
  /** Agent ID to configure */
  agentId: string;
  /** Optional agent display name */
  agentName?: string;
  /** User's tier for model filtering */
  userTier?: string;
  /** Callback after successful config update */
  onConfigUpdated?: () => void;
  /** Callback after name update */
  onNameUpdated?: (newName: string) => void;
}

interface FormState {
  modelId: string;
  temperature: number;
  maxTokens: number;
  name: string;
}

const DEFAULT_FREE_MODEL = 'meta-llama/llama-3.3-70b-instruct:free';

// ============================================================================
// Component
// ============================================================================

export function AgentSettingsDrawer({
  isOpen,
  onClose,
  agentId,
  agentName = '',
  userTier = 'free',
  onConfigUpdated,
  onNameUpdated,
}: AgentSettingsDrawerProps): React.JSX.Element {
  // Fetch models filtered by tier
  const { data: models = [], isLoading: modelsLoading } = useAvailableModels(userTier);

  // Fetch current agent config
  const { data: agentConfig, isLoading: configLoading } = useAgentConfiguration(isOpen ? agentId : null);

  // Mutation for PATCH
  const { mutate: patchConfig, isPending } = useUpdateAgentConfiguration(agentId);

  // Local form state
  const [form, setForm] = useState<FormState>({
    modelId: DEFAULT_FREE_MODEL,
    temperature: 0.3,
    maxTokens: 2048,
    name: agentName,
  });

  // Sync form when config loads
  useEffect(() => {
    if (agentConfig) {
      setForm(prev => ({
        ...prev,
        modelId: agentConfig.llmModel,
        temperature: agentConfig.temperature,
        maxTokens: agentConfig.maxTokens,
      }));
    }
  }, [agentConfig]);

  useEffect(() => {
    setForm(prev => ({ ...prev, name: agentName }));
  }, [agentName]);

  // Cost estimate for selected model
  const selectedModel = models.find(m => m.id === form.modelId);
  const costEstimate = selectedModel
    ? (selectedModel.costPer1kInputTokens * 2 + selectedModel.costPer1kOutputTokens * 1).toFixed(4)
    : null;

  // Handlers
  const handleApply = useCallback(() => {
    const configPayload: Record<string, unknown> = {};

    // Only include fields that changed from current config
    if (agentConfig) {
      if (form.modelId !== agentConfig.llmModel) configPayload.modelId = form.modelId;
      if (form.temperature !== agentConfig.temperature) configPayload.temperature = form.temperature;
      if (form.maxTokens !== agentConfig.maxTokens) configPayload.maxTokens = form.maxTokens;
    } else {
      configPayload.modelId = form.modelId;
      configPayload.temperature = form.temperature;
      configPayload.maxTokens = form.maxTokens;
    }

    // Patch config if anything changed
    if (Object.keys(configPayload).length > 0) {
      patchConfig(configPayload as any, {
        onSuccess: () => {
          toast.success('Configurazione aggiornata!', {
            description: 'Le modifiche si applicano alla prossima domanda.',
          });
          onConfigUpdated?.();
          onClose();
        },
        onError: (error: Error) => {
          toast.error('Errore', {
            description: error.message || 'Impossibile aggiornare la configurazione.',
          });
        },
      });
    }

    // Update name separately via existing PUT endpoint
    if (form.name.trim() && form.name.trim() !== agentName) {
      api.agents.updateUserAgent(agentId, { name: form.name.trim() })
        .then(() => {
          onNameUpdated?.(form.name.trim());
        })
        .catch(() => {
          toast.error('Errore aggiornamento nome agente');
        });
    }

    // Close if no config changes (name-only)
    if (Object.keys(configPayload).length === 0) {
      onClose();
    }
  }, [form, agentConfig, agentName, agentId, patchConfig, onConfigUpdated, onNameUpdated, onClose]);

  const handleReset = useCallback(() => {
    setForm({
      modelId: DEFAULT_FREE_MODEL,
      temperature: 0.3,
      maxTokens: 2048,
      name: agentName,
    });
    toast.info('Configurazione ripristinata ai valori predefiniti');
  }, [agentName]);

  const isLoading = modelsLoading || configLoading;

  return (
    <Sheet open={isOpen} onOpenChange={open => !open && onClose()}>
      <SheetContent side="right" className="w-[400px] flex flex-col bg-slate-900 border-slate-800">
        <SheetHeader className="border-b border-slate-800 pb-4">
          <SheetTitle className="flex items-center gap-2 text-white">
            <Settings className="h-5 w-5 text-cyan-400" />
            Impostazioni Agente
          </SheetTitle>
          <p className="text-xs text-slate-400 mt-1">
            Le modifiche si applicano alla prossima domanda
          </p>
        </SheetHeader>

        {/* Scrollable Content */}
        <div className="flex-1 overflow-y-auto py-6 space-y-6">
          {isLoading ? (
            <div className="flex items-center justify-center py-12">
              <Loader2 className="h-6 w-6 animate-spin text-cyan-400" />
            </div>
          ) : (
            <>
              {/* Agent Name */}
              <div className="space-y-2">
                <Label className="text-slate-200">Nome Agente</Label>
                <Input
                  value={form.name}
                  onChange={e => setForm(prev => ({ ...prev, name: e.target.value }))}
                  maxLength={100}
                  className="bg-slate-800 border-slate-700 text-white"
                  data-testid="agent-name-input"
                />
              </div>

              {/* Model Selector */}
              <div className="space-y-2">
                <Label className="flex items-center gap-2 text-slate-200">
                  <Zap className="h-4 w-4 text-purple-400" />
                  Modello AI
                </Label>
                <Select value={form.modelId} onValueChange={id => setForm(prev => ({ ...prev, modelId: id }))}>
                  <SelectTrigger className="bg-slate-800 border-slate-700 text-white" data-testid="model-selector">
                    <SelectValue placeholder="Seleziona modello" />
                  </SelectTrigger>
                  <SelectContent className="bg-slate-800 border-slate-700">
                    {models.map(model => (
                      <SelectItem
                        key={model.id}
                        value={model.id}
                        className="text-white hover:bg-slate-700"
                      >
                        <div className="flex items-center gap-2">
                          <span>{model.name}</span>
                          {model.tier !== 'Free' && (
                            <span className="text-[10px] px-1.5 py-0.5 bg-purple-500/20 text-purple-300 rounded">
                              {model.tier}
                            </span>
                          )}
                          {model.costPer1kInputTokens === 0 && model.costPer1kOutputTokens === 0 && (
                            <span className="text-[10px] px-1.5 py-0.5 bg-green-500/20 text-green-300 rounded">
                              FREE
                            </span>
                          )}
                        </div>
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                {selectedModel && (
                  <div className="flex items-center gap-1 text-[10px] text-slate-500">
                    {selectedModel.description}
                    {costEstimate && Number(costEstimate) > 0 && (
                      <span className="ml-auto flex items-center gap-0.5 text-amber-400">
                        <DollarSign className="h-3 w-3" />
                        ~${costEstimate}/query
                      </span>
                    )}
                  </div>
                )}
              </div>

              {/* Temperature Slider */}
              <div className="space-y-3">
                <Label className="flex items-center justify-between text-slate-200">
                  <span className="flex items-center gap-2">
                    <Thermometer className="h-4 w-4 text-orange-400" />
                    Temperatura
                  </span>
                  <span className="text-cyan-400 font-mono text-sm">
                    {form.temperature.toFixed(1)}
                  </span>
                </Label>
                <Slider
                  value={[form.temperature]}
                  onValueChange={values => setForm(prev => ({ ...prev, temperature: values[0] }))}
                  min={0}
                  max={2}
                  step={0.1}
                  className="w-full"
                  data-testid="temperature-slider"
                />
                <div className="flex justify-between text-[10px] text-slate-500">
                  <span>Preciso</span>
                  <span>Creativo</span>
                </div>
              </div>

              {/* Max Tokens */}
              <div className="space-y-2">
                <Label className="text-slate-200">Max Tokens</Label>
                <Input
                  type="number"
                  value={form.maxTokens}
                  onChange={e => {
                    const value = Math.max(1, Math.min(32000, parseInt(e.target.value) || 1));
                    setForm(prev => ({ ...prev, maxTokens: value }));
                  }}
                  min={1}
                  max={32000}
                  step={256}
                  className="bg-slate-800 border-slate-700 text-white"
                  data-testid="max-tokens-input"
                />
                <p className="text-[10px] text-slate-500">Range: 1 - 32000</p>
              </div>
            </>
          )}
        </div>

        {/* Footer Actions */}
        <SheetFooter className="border-t border-slate-800 pt-4 flex-row gap-3">
          <Button
            variant="ghost"
            onClick={handleReset}
            className="text-slate-400 hover:text-white"
            disabled={isPending || isLoading}
          >
            <RotateCcw className="h-4 w-4 mr-2" />
            Reset
          </Button>
          <div className="flex-1" />
          <Button
            variant="ghost"
            onClick={onClose}
            disabled={isPending}
          >
            Annulla
          </Button>
          <Button
            onClick={handleApply}
            disabled={isPending || isLoading}
            className="bg-cyan-500 hover:bg-cyan-600 text-black font-medium"
            data-testid="apply-config-btn"
          >
            {isPending ? (
              <Loader2 className="h-4 w-4 mr-2 animate-spin" />
            ) : (
              <Settings className="h-4 w-4 mr-2" />
            )}
            Applica
          </Button>
        </SheetFooter>
      </SheetContent>
    </Sheet>
  );
}

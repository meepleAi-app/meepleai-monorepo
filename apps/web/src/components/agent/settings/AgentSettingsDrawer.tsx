/**
 * AgentSettingsDrawer - Runtime agent configuration drawer
 * Issue #3250 (FRONT-014)
 *
 * Features:
 * - Model selector (tier-filtered)
 * - Temperature slider (0-2 range)
 * - RAG strategy radio group
 * - Max tokens input
 * - Advanced settings (collapsible)
 * - PATCH config on apply
 */

'use client';

import { useState, useCallback } from 'react';

import { useMutation } from '@tanstack/react-query';
import {
  Settings,
  ChevronDown,
  RotateCcw,
  Loader2,
  Zap,
  Thermometer,
  Search,
  SlidersHorizontal,
} from 'lucide-react';
import { toast } from 'sonner';

import {
  Collapsible,
  CollapsibleContent,
  CollapsibleTrigger,
} from '@/components/ui/data-display/collapsible';
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
import { RadioGroup, RadioGroupItem } from '@/components/ui/primitives/radio-group';
import { Slider } from '@/components/ui/primitives/slider';

// ============================================================================
// Types
// ============================================================================

export interface AgentRuntimeConfig {
  /** AI model ID */
  modelId: string;
  /** Temperature (0-2) */
  temperature: number;
  /** RAG strategy */
  ragStrategy: 'hybrid' | 'vector' | 'keyword';
  /** Max tokens (512-8192) */
  maxTokens: number;
  /** Top K for retrieval */
  topK: number;
  /** Minimum relevance score */
  minScore: number;
}

interface ModelOption {
  id: string;
  name: string;
  tier: 'free' | 'premium';
  description: string;
}

interface AgentSettingsDrawerProps {
  /** Whether drawer is open */
  isOpen: boolean;
  /** Callback to close drawer */
  onClose: () => void;
  /** Current session ID */
  sessionId: string;
  /** Current configuration */
  currentConfig: AgentRuntimeConfig;
  /** User's tier for model filtering */
  userTier?: 'free' | 'premium';
  /** Callback after successful config update */
  onConfigUpdated?: (config: AgentRuntimeConfig) => void;
}

// ============================================================================
// Constants
// ============================================================================

const AVAILABLE_MODELS: ModelOption[] = [
  { id: 'gpt-3.5-turbo', name: 'GPT-3.5 Turbo', tier: 'free', description: 'Fast and efficient' },
  { id: 'claude-3-haiku', name: 'Claude 3 Haiku', tier: 'free', description: 'Quick responses' },
  { id: 'gpt-4', name: 'GPT-4', tier: 'premium', description: 'Most capable' },
  { id: 'claude-3.5-sonnet', name: 'Claude 3.5 Sonnet', tier: 'premium', description: 'Best reasoning' },
];

const RAG_STRATEGIES = [
  { value: 'hybrid', label: 'Ibrida (Vector + Keyword)', description: 'Miglior accuratezza' },
  { value: 'vector', label: 'Solo Vector', description: 'Ricerca semantica' },
  { value: 'keyword', label: 'Solo Keyword', description: 'Ricerca esatta' },
] as const;

const DEFAULT_CONFIG: AgentRuntimeConfig = {
  modelId: 'gpt-3.5-turbo',
  temperature: 0.7,
  ragStrategy: 'hybrid',
  maxTokens: 2048,
  topK: 5,
  minScore: 0.7,
};

// ============================================================================
// Component
// ============================================================================

export function AgentSettingsDrawer({
  isOpen,
  onClose,
  sessionId,
  currentConfig,
  userTier = 'free',
  onConfigUpdated,
}: AgentSettingsDrawerProps): React.JSX.Element {
  // Local state for form
  const [config, setConfig] = useState<AgentRuntimeConfig>(currentConfig);
  const [isAdvancedOpen, setIsAdvancedOpen] = useState(false);

  // Filter models by user tier
  const availableModels = AVAILABLE_MODELS.filter(
    model => model.tier === 'free' || userTier === 'premium'
  );

  // PATCH mutation
  const { mutate: updateConfig, isPending } = useMutation({
    mutationFn: async (newConfig: AgentRuntimeConfig) => {
      const response = await fetch(`/api/v1/game-sessions/${sessionId}/agent/config`, {
        method: 'PATCH',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(newConfig),
      });
      if (!response.ok) {
        throw new Error('Errore aggiornamento configurazione');
      }
      return response.json();
    },
    onSuccess: () => {
      toast.success('Configurazione aggiornata!', {
        description: 'Le modifiche si applicano alla prossima domanda.',
      });
      onConfigUpdated?.(config);
      onClose();
    },
    onError: (error: Error) => {
      toast.error('Errore', {
        description: error.message || 'Impossibile aggiornare la configurazione.',
      });
    },
  });

  // Handlers
  const handleApply = useCallback(() => {
    updateConfig(config);
  }, [config, updateConfig]);

  const handleReset = useCallback(() => {
    setConfig(DEFAULT_CONFIG);
    toast.info('Configurazione ripristinata ai valori predefiniti');
  }, []);

  const handleTemperatureChange = useCallback((values: number[]) => {
    setConfig(prev => ({ ...prev, temperature: values[0] }));
  }, []);

  const handleModelChange = useCallback((modelId: string) => {
    setConfig(prev => ({ ...prev, modelId }));
  }, []);

  const handleStrategyChange = useCallback((strategy: AgentRuntimeConfig['ragStrategy']) => {
    setConfig(prev => ({ ...prev, ragStrategy: strategy }));
  }, []);

  const handleMaxTokensChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    const value = Math.max(512, Math.min(8192, parseInt(e.target.value) || 512));
    setConfig(prev => ({ ...prev, maxTokens: value }));
  }, []);

  const handleTopKChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    const value = Math.max(1, Math.min(20, parseInt(e.target.value) || 5));
    setConfig(prev => ({ ...prev, topK: value }));
  }, []);

  const handleMinScoreChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    const value = Math.max(0, Math.min(1, parseFloat(e.target.value) || 0.5));
    setConfig(prev => ({ ...prev, minScore: value }));
  }, []);

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
          {/* Model Selector */}
          <div className="space-y-2">
            <Label className="flex items-center gap-2 text-slate-200">
              <Zap className="h-4 w-4 text-purple-400" />
              Modello AI
            </Label>
            <Select value={config.modelId} onValueChange={handleModelChange}>
              <SelectTrigger className="bg-slate-800 border-slate-700 text-white">
                <SelectValue placeholder="Seleziona modello" />
              </SelectTrigger>
              <SelectContent className="bg-slate-800 border-slate-700">
                {availableModels.map(model => (
                  <SelectItem
                    key={model.id}
                    value={model.id}
                    className="text-white hover:bg-slate-700"
                  >
                    <div className="flex items-center gap-2">
                      <span>{model.name}</span>
                      {model.tier === 'premium' && (
                        <span className="text-[10px] px-1.5 py-0.5 bg-purple-500/20 text-purple-300 rounded">
                          PRO
                        </span>
                      )}
                    </div>
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <p className="text-[10px] text-slate-500">
              {availableModels.find(m => m.id === config.modelId)?.description}
            </p>
          </div>

          {/* Temperature Slider */}
          <div className="space-y-3">
            <Label className="flex items-center justify-between text-slate-200">
              <span className="flex items-center gap-2">
                <Thermometer className="h-4 w-4 text-orange-400" />
                Temperatura
              </span>
              <span className="text-cyan-400 font-mono text-sm">
                {config.temperature.toFixed(1)}
              </span>
            </Label>
            <Slider
              value={[config.temperature]}
              onValueChange={handleTemperatureChange}
              min={0}
              max={2}
              step={0.1}
              className="w-full"
            />
            <div className="flex justify-between text-[10px] text-slate-500">
              <span>Preciso</span>
              <span>Creativo</span>
            </div>
          </div>

          {/* RAG Strategy */}
          <div className="space-y-3">
            <Label className="flex items-center gap-2 text-slate-200">
              <Search className="h-4 w-4 text-cyan-400" />
              Strategia Ricerca
            </Label>
            <RadioGroup
              value={config.ragStrategy}
              onValueChange={handleStrategyChange as (value: string) => void}
              className="space-y-2"
            >
              {RAG_STRATEGIES.map(strategy => (
                <div
                  key={strategy.value}
                  className="flex items-center space-x-3 rounded-lg border border-slate-700 p-3 hover:border-slate-600 transition-colors cursor-pointer"
                >
                  <RadioGroupItem
                    value={strategy.value}
                    id={strategy.value}
                    className="border-cyan-400 text-cyan-400"
                  />
                  <label htmlFor={strategy.value} className="flex-1 cursor-pointer">
                    <span className="text-sm text-white">{strategy.label}</span>
                    <p className="text-[10px] text-slate-500">{strategy.description}</p>
                  </label>
                </div>
              ))}
            </RadioGroup>
          </div>

          {/* Max Tokens */}
          <div className="space-y-2">
            <Label className="text-slate-200">Max Tokens</Label>
            <Input
              type="number"
              value={config.maxTokens}
              onChange={handleMaxTokensChange}
              min={512}
              max={8192}
              step={256}
              className="bg-slate-800 border-slate-700 text-white"
            />
            <p className="text-[10px] text-slate-500">Range: 512 - 8192</p>
          </div>

          {/* Advanced Settings (Collapsible) */}
          <Collapsible open={isAdvancedOpen} onOpenChange={setIsAdvancedOpen}>
            <CollapsibleTrigger className="flex items-center justify-between w-full py-2 text-slate-200 hover:text-white transition-colors">
              <span className="flex items-center gap-2">
                <SlidersHorizontal className="h-4 w-4 text-slate-400" />
                Avanzate
              </span>
              <ChevronDown
                className={`h-4 w-4 text-slate-400 transition-transform ${
                  isAdvancedOpen ? 'rotate-180' : ''
                }`}
              />
            </CollapsibleTrigger>
            <CollapsibleContent className="space-y-4 pt-3">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label className="text-xs text-slate-300">Top K</Label>
                  <Input
                    type="number"
                    value={config.topK}
                    onChange={handleTopKChange}
                    min={1}
                    max={20}
                    className="bg-slate-800 border-slate-700 text-white text-sm"
                  />
                </div>
                <div className="space-y-2">
                  <Label className="text-xs text-slate-300">Min Score</Label>
                  <Input
                    type="number"
                    value={config.minScore}
                    onChange={handleMinScoreChange}
                    min={0}
                    max={1}
                    step={0.05}
                    className="bg-slate-800 border-slate-700 text-white text-sm"
                  />
                </div>
              </div>
            </CollapsibleContent>
          </Collapsible>
        </div>

        {/* Footer Actions */}
        <SheetFooter className="border-t border-slate-800 pt-4 flex-row gap-3">
          <Button
            variant="ghost"
            onClick={handleReset}
            className="text-slate-400 hover:text-white"
            disabled={isPending}
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
            disabled={isPending}
            className="bg-cyan-500 hover:bg-cyan-600 text-black font-medium"
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

/**
 * Phase Model Configuration Component - Issue #3245
 *
 * UI component for configuring LLM models per strategy phase.
 * Supports all 6 RAG strategies: FAST, BALANCED, PRECISE, EXPERT, CONSENSUS, CUSTOM.
 */

'use client';

import { useState, useEffect } from 'react';

import { AlertCircle, Info, Trash2, Plus } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
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
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { Slider } from '@/components/ui/primitives/slider';

// ========== Types ==========

export interface PhaseModelConfig {
  model: string;
  maxTokens: number;
  temperature: number;
}

export interface StrategyPhaseModels {
  // Standard phases
  retrieval?: PhaseModelConfig;
  analysis?: PhaseModelConfig;
  synthesis?: PhaseModelConfig;
  validation?: PhaseModelConfig;
  cragEvaluation?: PhaseModelConfig;
  selfReflection?: PhaseModelConfig;
  // EXPERT phases
  webSearch?: PhaseModelConfig;
  multiHop?: PhaseModelConfig;
  // CONSENSUS phases
  consensusVoter1?: PhaseModelConfig;
  consensusVoter2?: PhaseModelConfig;
  consensusVoter3?: PhaseModelConfig;
  consensusAggregator?: PhaseModelConfig;
}

export interface StrategyOptions {
  enableWebSearch?: boolean;
  enableMultiHop?: boolean;
  maxHops?: number;
  consensusThreshold?: number;
  enableCitationValidation?: boolean;
  enableSelfReflection?: boolean;
}

export interface PhaseModelConfigurationProps {
  strategy: string;
  phaseModels: StrategyPhaseModels;
  strategyOptions?: StrategyOptions;
  onChange: (phaseModels: StrategyPhaseModels, strategyOptions?: StrategyOptions) => void;
  disabled?: boolean;
}

// ========== Constants ==========

const AVAILABLE_MODELS = [
  { value: 'meta-llama/llama-3.3-70b-instruct:free', label: 'Llama 3.3 70B (Free)', cost: '$0' },
  { value: 'google/gemini-2.0-flash-exp:free', label: 'Gemini 2.0 Flash (Free)', cost: '$0' },
  { value: 'claude-3-5-haiku-20241022', label: 'Claude 3.5 Haiku', cost: '$0.25/1M' },
  { value: 'claude-3-5-sonnet-20241022', label: 'Claude 3.5 Sonnet', cost: '$3/1M' },
  { value: 'claude-3-5-opus-20241022', label: 'Claude 3.5 Opus', cost: '$15/1M' },
  { value: 'openai/gpt-4o', label: 'GPT-4o', cost: '$5/1M' },
  { value: 'openai/gpt-4o-mini', label: 'GPT-4o Mini', cost: '$0.15/1M' },
  { value: 'deepseek/deepseek-chat', label: 'DeepSeek Chat', cost: '$0.14/1M' },
] as const;

const STRATEGY_PHASES: Record<string, { required: string[]; optional: string[] }> = {
  FAST: { required: ['synthesis'], optional: [] },
  BALANCED: { required: ['synthesis', 'cragEvaluation'], optional: [] },
  PRECISE: { required: ['retrieval', 'analysis', 'synthesis', 'validation'], optional: ['selfReflection'] },
  EXPERT: { required: ['webSearch', 'multiHop', 'synthesis'], optional: [] },
  CONSENSUS: { required: ['consensusVoter1', 'consensusVoter2', 'consensusVoter3', 'consensusAggregator'], optional: [] },
  CUSTOM: { required: ['synthesis'], optional: ['retrieval', 'analysis', 'validation', 'cragEvaluation', 'selfReflection', 'webSearch', 'multiHop'] },
};

const PHASE_LABELS: Record<string, { label: string; description: string; defaultModel: string }> = {
  retrieval: { label: 'Retrieval', description: 'Analizza query, pianifica retrieval', defaultModel: 'claude-3-5-haiku-20241022' },
  analysis: { label: 'Analysis', description: 'Estrae regole, identifica pattern', defaultModel: 'claude-3-5-haiku-20241022' },
  synthesis: { label: 'Synthesis', description: 'Sintetizza risposta finale', defaultModel: 'claude-3-5-sonnet-20241022' },
  validation: { label: 'Validation', description: 'Verifica correttezza e citazioni', defaultModel: 'claude-3-5-haiku-20241022' },
  cragEvaluation: { label: 'CRAG Evaluation', description: 'Valuta rilevanza documenti', defaultModel: 'claude-3-5-haiku-20241022' },
  selfReflection: { label: 'Self-Reflection', description: 'Auto-valutazione risposta', defaultModel: 'claude-3-5-sonnet-20241022' },
  webSearch: { label: 'Web Search', description: 'Genera query per ricerca web', defaultModel: 'claude-3-5-haiku-20241022' },
  multiHop: { label: 'Multi-Hop', description: 'Ragionamento multi-step', defaultModel: 'claude-3-5-sonnet-20241022' },
  consensusVoter1: { label: 'Voter 1', description: 'Primo votante (Claude)', defaultModel: 'claude-3-5-sonnet-20241022' },
  consensusVoter2: { label: 'Voter 2', description: 'Secondo votante (GPT)', defaultModel: 'openai/gpt-4o' },
  consensusVoter3: { label: 'Voter 3', description: 'Terzo votante (DeepSeek)', defaultModel: 'deepseek/deepseek-chat' },
  consensusAggregator: { label: 'Aggregator', description: 'Combina voti, decide finale', defaultModel: 'claude-3-5-sonnet-20241022' },
};

// ========== Helper Components ==========

interface PhaseConfigRowProps {
  phaseName: string;
  config: PhaseModelConfig;
  isRequired: boolean;
  onChange: (config: PhaseModelConfig) => void;
  onRemove?: () => void;
  disabled?: boolean;
}

function PhaseConfigRow({ phaseName, config, isRequired, onChange, onRemove, disabled }: PhaseConfigRowProps) {
   
  const phaseInfo = PHASE_LABELS[phaseName] || { label: phaseName, description: '', defaultModel: 'claude-3-5-haiku-20241022' };

  return (
    <div className="grid grid-cols-12 gap-3 items-center p-3 border rounded-lg bg-card">
      {/* Phase Name */}
      <div className="col-span-3 flex items-center gap-2">
        <div>
          <div className="flex items-center gap-1.5">
            <span className="font-medium text-sm">{phaseInfo.label}</span>
            {isRequired && <Badge variant="secondary" className="text-xs">Required</Badge>}
          </div>
          <p className="text-xs text-muted-foreground">{phaseInfo.description}</p>
        </div>
      </div>

      {/* Model Selector */}
      <div className="col-span-4">
        <Select
          value={config.model}
          onValueChange={(value) => onChange({ ...config, model: value })}
          disabled={disabled}
        >
          <SelectTrigger className="w-full">
            <SelectValue placeholder="Seleziona modello" />
          </SelectTrigger>
          <SelectContent>
            {AVAILABLE_MODELS.map((model) => (
              <SelectItem key={model.value} value={model.value}>
                <div className="flex items-center justify-between w-full">
                  <span>{model.label}</span>
                  <Badge variant="outline" className="ml-2 text-xs">{model.cost}</Badge>
                </div>
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {/* Max Tokens */}
      <div className="col-span-2">
        <Input
          type="number"
          value={config.maxTokens}
          onChange={(e) => onChange({ ...config, maxTokens: parseInt(e.target.value) || 500 })}
          min={50}
          max={32000}
          disabled={disabled}
          className="w-full"
          placeholder="Max Tokens"
        />
      </div>

      {/* Temperature */}
      <div className="col-span-2">
        <div className="flex items-center gap-2">
          <Slider
            value={[config.temperature]}
            onValueChange={([value]) => onChange({ ...config, temperature: value })}
            min={0}
            max={2}
            step={0.1}
            disabled={disabled}
            className="flex-1"
          />
          <span className="text-xs w-8 text-center">{config.temperature.toFixed(1)}</span>
        </div>
      </div>

      {/* Remove Button (only for optional phases) */}
      <div className="col-span-1 flex justify-end">
        {!isRequired && onRemove && (
          <Button
            type="button"
            variant="ghost"
            size="icon"
            onClick={onRemove}
            disabled={disabled}
            className="h-8 w-8 text-destructive hover:text-destructive"
          >
            <Trash2 className="h-4 w-4" />
          </Button>
        )}
      </div>
    </div>
  );
}

// ========== Main Component ==========

export function PhaseModelConfiguration({
  strategy,
  phaseModels,
  strategyOptions,
  onChange,
  disabled = false,
}: PhaseModelConfigurationProps) {
  const [localPhaseModels, setLocalPhaseModels] = useState<StrategyPhaseModels>(phaseModels);
  const [localOptions, setLocalOptions] = useState<StrategyOptions>(strategyOptions || {});

   
  const phases = STRATEGY_PHASES[strategy] || STRATEGY_PHASES.CUSTOM;
  const isRagStrategy = ['FAST', 'BALANCED', 'PRECISE', 'EXPERT', 'CONSENSUS', 'CUSTOM'].includes(strategy);

  // Initialize required phases with defaults when strategy changes
  useEffect(() => {
    if (!isRagStrategy) return;

    setLocalPhaseModels((prevPhaseModels) => {
      const newPhaseModels = { ...prevPhaseModels };
      let hasChanges = false;

      phases.required.forEach((phase) => {
        if (!newPhaseModels[phase as keyof StrategyPhaseModels]) {
           
          const phaseInfo = PHASE_LABELS[phase];
          newPhaseModels[phase as keyof StrategyPhaseModels] = {
            model: phaseInfo?.defaultModel || 'claude-3-5-haiku-20241022',
            maxTokens: 500,
            temperature: 0.7,
          };
          hasChanges = true;
        }
      });

      if (hasChanges) {
        // Schedule onChange to run after state update
        setTimeout(() => {
          setLocalOptions((prevOptions) => {
            onChange(newPhaseModels, prevOptions);
            return prevOptions;
          });
        }, 0);
        return newPhaseModels;
      }
      return prevPhaseModels;
    });
  }, [strategy, isRagStrategy, phases.required, onChange]);

  // Update phase config
  const updatePhase = (phaseName: string, config: PhaseModelConfig) => {
    const newPhaseModels = {
      ...localPhaseModels,
      [phaseName]: config,
    };
    setLocalPhaseModels(newPhaseModels);
    onChange(newPhaseModels, localOptions);
  };

  // Remove optional phase
  const removePhase = (phaseName: string) => {
    const newPhaseModels = { ...localPhaseModels };
    delete newPhaseModels[phaseName as keyof StrategyPhaseModels];
    setLocalPhaseModels(newPhaseModels);
    onChange(newPhaseModels, localOptions);
  };

  // Add optional phase
  const addPhase = (phaseName: string) => {
     
    const phaseInfo = PHASE_LABELS[phaseName];
    const newPhaseModels = {
      ...localPhaseModels,
      [phaseName]: {
        model: phaseInfo?.defaultModel || 'claude-3-5-haiku-20241022',
        maxTokens: 500,
        temperature: 0.7,
      },
    };
    setLocalPhaseModels(newPhaseModels);
    onChange(newPhaseModels, localOptions);
  };

  // Update strategy options
  const updateOptions = (key: keyof StrategyOptions, value: unknown) => {
    const newOptions = { ...localOptions, [key]: value };
    setLocalOptions(newOptions);
    onChange(localPhaseModels, newOptions);
  };

  if (!isRagStrategy) {
    return (
      <Alert>
        <Info className="h-4 w-4" />
        <AlertDescription>
          La configurazione phase-model è disponibile solo per strategie RAG (FAST, BALANCED, PRECISE, EXPERT, CONSENSUS, CUSTOM).
        </AlertDescription>
      </Alert>
    );
  }

  const configuredPhases = Object.keys(localPhaseModels).filter(
    (phase) => localPhaseModels[phase as keyof StrategyPhaseModels]
  );
  const availableOptionalPhases = phases.optional.filter(
    (phase) => !configuredPhases.includes(phase)
  );

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          Configurazione Modelli per Fase
          <TooltipProvider>
            <Tooltip>
              <TooltipTrigger>
                <Info className="h-4 w-4 text-muted-foreground" />
              </TooltipTrigger>
              <TooltipContent className="max-w-sm">
                <p>
                  Configura quale modello LLM utilizzare per ogni fase della strategia {strategy}.
                  I modelli economici (Haiku, DeepSeek) sono consigliati per fasi di supporto,
                  mentre i modelli premium (Sonnet, Opus) per Synthesis.
                </p>
              </TooltipContent>
            </Tooltip>
          </TooltipProvider>
        </CardTitle>
        <CardDescription>
          Strategia: <Badge variant="outline">{strategy}</Badge> - {phases.required.length} fasi richieste
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {/* Header Row */}
        <div className="grid grid-cols-12 gap-3 text-xs font-medium text-muted-foreground px-3">
          <div className="col-span-3">Fase</div>
          <div className="col-span-4">Modello</div>
          <div className="col-span-2">Max Tokens</div>
          <div className="col-span-2">Temperature</div>
          <div className="col-span-1"></div>
        </div>

        {/* Required Phases */}
        {phases.required.map((phaseName) => (
          <PhaseConfigRow
            key={phaseName}
            phaseName={phaseName}
            config={localPhaseModels[phaseName as keyof StrategyPhaseModels] || {
               
              model: PHASE_LABELS[phaseName]?.defaultModel || 'claude-3-5-haiku-20241022',
              maxTokens: 500,
              temperature: 0.7,
            }}
            isRequired={true}
            onChange={(config) => updatePhase(phaseName, config)}
            disabled={disabled}
          />
        ))}

        {/* Configured Optional Phases */}
        {configuredPhases
          .filter((phase) => phases.optional.includes(phase))
          .map((phaseName) => (
            <PhaseConfigRow
              key={phaseName}
              phaseName={phaseName}
              config={localPhaseModels[phaseName as keyof StrategyPhaseModels] || {
                model: 'claude-3-5-haiku-20241022',
                maxTokens: 500,
                temperature: 0.7,
              }}
              isRequired={false}
              onChange={(config) => updatePhase(phaseName, config)}
              onRemove={() => removePhase(phaseName)}
              disabled={disabled}
            />
          ))}

        {/* Add Optional Phase */}
        {availableOptionalPhases.length > 0 && (
          <div className="flex items-center gap-2 pt-2">
            <Select
              onValueChange={addPhase}
              disabled={disabled}
            >
              <SelectTrigger className="w-[250px]">
                <Plus className="h-4 w-4 mr-2" />
                <SelectValue placeholder="Aggiungi fase opzionale" />
              </SelectTrigger>
              <SelectContent>
                {availableOptionalPhases.map((phase) => {
                   
                  const phaseLabel = PHASE_LABELS[phase]?.label || phase;
                  return (
                    <SelectItem key={phase} value={phase}>
                      {phaseLabel}
                    </SelectItem>
                  );
                })}
              </SelectContent>
            </Select>
          </div>
        )}

        {/* Strategy-specific Options */}
        {(strategy === 'EXPERT' || strategy === 'CONSENSUS' || strategy === 'CUSTOM') && (
          <div className="border-t pt-4 mt-4">
            <Label className="text-sm font-medium">Opzioni Strategia</Label>
            <div className="grid grid-cols-2 gap-4 mt-3">
              {(strategy === 'EXPERT' || strategy === 'CUSTOM') && (
                <div>
                  <Label htmlFor="maxHops" className="text-xs">Max Hops (Multi-Hop)</Label>
                  <Input
                    id="maxHops"
                    type="number"
                    value={localOptions.maxHops || 2}
                    onChange={(e) => updateOptions('maxHops', parseInt(e.target.value) || 2)}
                    min={1}
                    max={5}
                    disabled={disabled}
                    className="mt-1"
                  />
                </div>
              )}
              {(strategy === 'CONSENSUS' || strategy === 'CUSTOM') && (
                <div>
                  <Label htmlFor="consensusThreshold" className="text-xs">
                    Consensus Threshold ({((localOptions.consensusThreshold || 0.8) * 100).toFixed(0)}%)
                  </Label>
                  <Slider
                    value={[localOptions.consensusThreshold || 0.8]}
                    onValueChange={([value]) => updateOptions('consensusThreshold', value)}
                    min={0.5}
                    max={1}
                    step={0.05}
                    disabled={disabled}
                    className="mt-2"
                  />
                </div>
              )}
            </div>
          </div>
        )}

        {/* Validation Warning */}
        {phases.required.some((phase) => !localPhaseModels[phase as keyof StrategyPhaseModels]) && (
          <Alert variant="destructive">
            <AlertCircle className="h-4 w-4" />
            <AlertDescription>
              Configura tutte le fasi richieste: {phases.required.filter(
                (phase) => !localPhaseModels[phase as keyof StrategyPhaseModels]
               
              ).map((p) => PHASE_LABELS[p]?.label || p).join(', ')}
            </AlertDescription>
          </Alert>
        )}
      </CardContent>
    </Card>
  );
}

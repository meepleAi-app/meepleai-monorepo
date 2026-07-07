/**
 * AgentConfigFields (Issue #2732)
 *
 * Shared, fully-controlled presentational editor for the per-game AI agent
 * configuration. Extracted verbatim from the edit-mode fields of
 * `AgentConfigModal` (library) so the exact same 6-field editor can be reused
 * by the `/agents/[id]` Settings tab (`AgentSettingsForm`).
 *
 * Contract:
 *   - No internal state, no data fetching — the parent owns `value` and
 *     receives every change through `onChange`.
 *   - `disabled` puts every control in read-only mode.
 *   - `idPrefix` namespaces the element ids so two instances can coexist on the
 *     same page. Default `''` preserves the historical modal ids
 *     (`model`/`temperature`/`maxTokens`/`personality-*`/`detail-*`/`instructions`).
 */

'use client';

import type { ReactElement } from 'react';

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { RadioGroup, RadioGroupItem } from '@/components/ui/primitives/radio-group';
import { Slider } from '@/components/ui/primitives/slider';
import { Textarea } from '@/components/ui/primitives/textarea';
import {
  MODEL_OPTIONS,
  PERSONALITY_OPTIONS,
  DETAIL_LEVEL_OPTIONS,
  type AIModel,
  type AgentPersonality,
  type DetailLevel,
} from '@/lib/api';
import { cn } from '@/lib/utils';

const NOTES_MAX_LENGTH = 1000;

export interface AgentConfigFieldsValue {
  llmModel: AIModel;
  temperature: number;
  maxTokens: number;
  personality: AgentPersonality;
  detailLevel: DetailLevel;
  personalNotes: string;
}

export interface AgentConfigFieldsProps {
  value: AgentConfigFieldsValue;
  onChange: (value: AgentConfigFieldsValue) => void;
  disabled?: boolean;
  idPrefix?: string;
  className?: string;
}

export function AgentConfigFields({
  value,
  onChange,
  disabled = false,
  idPrefix = '',
  className,
}: AgentConfigFieldsProps): ReactElement {
  const modelId = `${idPrefix}model`;
  const temperatureId = `${idPrefix}temperature`;
  const maxTokensId = `${idPrefix}maxTokens`;
  const instructionsId = `${idPrefix}instructions`;

  return (
    <div data-slot="agent-config-fields" className={cn('space-y-6', className)}>
      {/* Model Selection */}
      <div className="space-y-2">
        <Label htmlFor={modelId} className="text-base font-semibold">
          🤖 Modello AI
        </Label>
        <Select
          value={value.llmModel}
          onValueChange={next => onChange({ ...value, llmModel: next as AIModel })}
          disabled={disabled}
        >
          <SelectTrigger id={modelId}>
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {MODEL_OPTIONS.map(model => (
              <SelectItem key={model.value} value={model.value}>
                {model.label} ({model.costLevel}) {model.isDefault && '⭐'}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <p className="text-sm text-muted-foreground">
          {MODEL_OPTIONS.find(m => m.value === value.llmModel)?.description}
        </p>
      </div>

      {/* Temperature Slider */}
      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <Label htmlFor={temperatureId} className="text-base font-semibold">
            ⚙️ Temperatura
          </Label>
          <span className="text-sm text-muted-foreground">{value.temperature.toFixed(2)}</span>
        </div>
        <Slider
          id={temperatureId}
          min={0}
          max={2}
          step={0.1}
          value={[value.temperature]}
          onValueChange={([next]) => onChange({ ...value, temperature: next })}
          disabled={disabled}
          className="w-full"
        />
        <div className="flex justify-between text-xs text-muted-foreground">
          <span>0.0 (Preciso)</span>
          <span>2.0 (Creativo)</span>
        </div>
      </div>

      {/* Max Tokens Input */}
      <div className="space-y-2">
        <Label htmlFor={maxTokensId} className="text-base font-semibold">
          📏 Max Tokens
        </Label>
        <Input
          id={maxTokensId}
          type="number"
          min={512}
          max={8192}
          step={256}
          value={value.maxTokens}
          onChange={e => onChange({ ...value, maxTokens: Number(e.target.value) })}
          disabled={disabled}
        />
        <p className="text-sm text-muted-foreground">Lunghezza massima della risposta (512-8192)</p>
      </div>

      {/* Personality Radio Buttons */}
      <div className="space-y-2">
        <Label className="text-base font-semibold">🎭 Personalità Agente</Label>
        <RadioGroup
          value={value.personality}
          onValueChange={next => onChange({ ...value, personality: next as AgentPersonality })}
          disabled={disabled}
        >
          {PERSONALITY_OPTIONS.map(option => (
            <div key={option.value} className="flex items-center space-x-2">
              <RadioGroupItem
                value={option.value}
                id={`${idPrefix}personality-${option.value}`}
                disabled={disabled}
              />
              <Label
                htmlFor={`${idPrefix}personality-${option.value}`}
                className="font-normal cursor-pointer"
              >
                <span className="font-medium">{option.label}</span>
                <span className="text-sm text-muted-foreground ml-2">({option.description})</span>
              </Label>
            </div>
          ))}
        </RadioGroup>
      </div>

      {/* Detail Level Radio Buttons */}
      <div className="space-y-2">
        <Label className="text-base font-semibold">📊 Livello Dettaglio</Label>
        <RadioGroup
          value={value.detailLevel}
          onValueChange={next => onChange({ ...value, detailLevel: next as DetailLevel })}
          disabled={disabled}
        >
          {DETAIL_LEVEL_OPTIONS.map(option => (
            <div key={option.value} className="flex items-center space-x-2">
              <RadioGroupItem
                value={option.value}
                id={`${idPrefix}detail-${option.value}`}
                disabled={disabled}
              />
              <Label
                htmlFor={`${idPrefix}detail-${option.value}`}
                className="font-normal cursor-pointer"
              >
                <span className="font-medium">{option.label}</span>
                <span className="text-sm text-muted-foreground ml-2">({option.description})</span>
              </Label>
            </div>
          ))}
        </RadioGroup>
      </div>

      {/* Custom Instructions Textarea */}
      <div className="space-y-2">
        <Label htmlFor={instructionsId} className="text-base font-semibold">
          📝 Note Personali
        </Label>
        <Textarea
          id={instructionsId}
          placeholder="Es: Spiega sempre le regole come se fossi principiante"
          value={value.personalNotes}
          onChange={e => onChange({ ...value, personalNotes: e.target.value })}
          maxLength={NOTES_MAX_LENGTH}
          rows={4}
          disabled={disabled}
        />
        <div className="text-xs text-right text-muted-foreground">
          {NOTES_MAX_LENGTH - value.personalNotes.length} caratteri rimanenti
        </div>
      </div>
    </div>
  );
}

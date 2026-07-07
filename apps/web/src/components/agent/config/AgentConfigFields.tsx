/**
 * AgentConfigFields — shared per-game AI config field group (Issue #2732).
 *
 * Controlled presentational component extracted verbatim from AgentConfigModal's
 * inline fields so the /agents/[id] Settings tab and the library modal share one
 * tested UI. No hooks, no data fetching. Emits partial patches via `onChange`.
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

export interface AgentConfigFieldsValue {
  llmModel: AIModel;
  temperature: number;
  maxTokens: number;
  personality: AgentPersonality;
  detailLevel: DetailLevel;
  /** Form-side value: empty string instead of null; consumers normalize to null. */
  personalNotes: string;
}

export interface AgentConfigFieldsProps {
  value: AgentConfigFieldsValue;
  onChange: (patch: Partial<AgentConfigFieldsValue>) => void;
  disabled?: boolean;
}

export function AgentConfigFields({
  value,
  onChange,
  disabled = false,
}: AgentConfigFieldsProps): ReactElement {
  return (
    <div className="space-y-6">
      {/* Model Selection */}
      <div className="space-y-2">
        <Label htmlFor="model" className="text-base font-semibold">
          🤖 Modello AI
        </Label>
        <Select
          value={value.llmModel}
          onValueChange={v => onChange({ llmModel: v as AIModel })}
          disabled={disabled}
        >
          <SelectTrigger id="model">
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
          <Label htmlFor="temperature" className="text-base font-semibold">
            ⚙️ Temperatura
          </Label>
          <span className="text-sm text-muted-foreground">{value.temperature.toFixed(2)}</span>
        </div>
        <Slider
          id="temperature"
          min={0}
          max={2}
          step={0.1}
          value={[value.temperature]}
          onValueChange={([v]) => onChange({ temperature: v })}
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
        <Label htmlFor="maxTokens" className="text-base font-semibold">
          📏 Max Tokens
        </Label>
        <Input
          id="maxTokens"
          type="number"
          min={512}
          max={8192}
          step={256}
          value={value.maxTokens}
          onChange={e => onChange({ maxTokens: Number(e.target.value) })}
          disabled={disabled}
        />
        <p className="text-sm text-muted-foreground">Lunghezza massima della risposta (512-8192)</p>
      </div>

      {/* Personality Radio Buttons */}
      <div className="space-y-2">
        <Label className="text-base font-semibold">🎭 Personalità Agente</Label>
        <RadioGroup
          value={value.personality}
          onValueChange={v => onChange({ personality: v as AgentPersonality })}
          disabled={disabled}
        >
          {PERSONALITY_OPTIONS.map(option => (
            <div key={option.value} className="flex items-center space-x-2">
              <RadioGroupItem
                value={option.value}
                id={`personality-${option.value}`}
                disabled={disabled}
              />
              <Label htmlFor={`personality-${option.value}`} className="font-normal cursor-pointer">
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
          onValueChange={v => onChange({ detailLevel: v as DetailLevel })}
          disabled={disabled}
        >
          {DETAIL_LEVEL_OPTIONS.map(option => (
            <div key={option.value} className="flex items-center space-x-2">
              <RadioGroupItem
                value={option.value}
                id={`detail-${option.value}`}
                disabled={disabled}
              />
              <Label htmlFor={`detail-${option.value}`} className="font-normal cursor-pointer">
                <span className="font-medium">{option.label}</span>
                <span className="text-sm text-muted-foreground ml-2">({option.description})</span>
              </Label>
            </div>
          ))}
        </RadioGroup>
      </div>

      {/* Personal Notes Textarea */}
      <div className="space-y-2">
        <Label htmlFor="instructions" className="text-base font-semibold">
          📝 Note Personali
        </Label>
        <Textarea
          id="instructions"
          placeholder="Es: Spiega sempre le regole come se fossi principiante"
          value={value.personalNotes}
          onChange={e => onChange({ personalNotes: e.target.value })}
          maxLength={1000}
          rows={4}
          disabled={disabled}
        />
        <div className="text-xs text-right text-muted-foreground">
          {1000 - value.personalNotes.length} caratteri rimanenti
        </div>
      </div>
    </div>
  );
}

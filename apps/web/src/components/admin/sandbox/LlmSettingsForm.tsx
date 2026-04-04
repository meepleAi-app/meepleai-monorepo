'use client';

import { useState } from 'react';

import { ChevronDown } from 'lucide-react';

import { useSandboxSession } from '@/components/admin/sandbox/contexts/SandboxSessionContext';
import {
  Collapsible,
  CollapsibleContent,
  CollapsibleTrigger,
} from '@/components/ui/data-display/collapsible';
import {
  Select,
  SelectContent,
  SelectGroup,
  SelectItem,
  SelectLabel,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { Textarea } from '@/components/ui/primitives/textarea';
import { Slider } from '@/components/ui/slider';

const MODEL_GROUPS = [
  {
    label: 'Free',
    models: [
      { value: 'meta-llama/llama-3-8b', label: 'Llama 3 8B' },
      { value: 'mistralai/mistral-7b', label: 'Mistral 7B' },
    ],
  },
  {
    label: 'Paid',
    models: [
      { value: 'anthropic/claude-3', label: 'Claude 3' },
      { value: 'openai/gpt-4o', label: 'GPT-4o' },
    ],
  },
] as const;

export function LlmSettingsForm() {
  const { agentConfig, updateConfig } = useSandboxSession();
  const [promptOpen, setPromptOpen] = useState(false);

  return (
    <div className="space-y-5">
      {/* Model */}
      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <Label htmlFor="model">Modello</Label>
          <span className="text-xs text-muted-foreground">
            {agentConfig.model || 'Non selezionato'}
          </span>
        </div>
        <Select value={agentConfig.model} onValueChange={value => updateConfig({ model: value })}>
          <SelectTrigger id="model">
            <SelectValue placeholder="Seleziona modello" />
          </SelectTrigger>
          <SelectContent>
            {MODEL_GROUPS.map(group => (
              <SelectGroup key={group.label}>
                <SelectLabel>{group.label}</SelectLabel>
                {group.models.map(m => (
                  <SelectItem key={m.value} value={m.value}>
                    {m.label}
                  </SelectItem>
                ))}
              </SelectGroup>
            ))}
          </SelectContent>
        </Select>
      </div>

      {/* Temperature */}
      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <Label>Temperatura</Label>
          <span className="text-xs font-mono text-muted-foreground">
            {agentConfig.temperature.toFixed(1)}
          </span>
        </div>
        <Slider
          min={0}
          max={2}
          step={0.1}
          value={[agentConfig.temperature]}
          onValueChange={([value]) => updateConfig({ temperature: value })}
          aria-label="Temperature"
        />
      </div>

      {/* Max Tokens */}
      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <Label htmlFor="maxTokens">Max tokens</Label>
          <span className="text-xs font-mono text-muted-foreground">{agentConfig.maxTokens}</span>
        </div>
        <Input
          id="maxTokens"
          type="number"
          min={256}
          max={4096}
          value={agentConfig.maxTokens}
          onChange={e => {
            const val = Number(e.target.value);
            if (val >= 256 && val <= 4096) {
              updateConfig({ maxTokens: val });
            }
          }}
        />
      </div>

      {/* System Prompt Override (collapsible) */}
      <Collapsible open={promptOpen} onOpenChange={setPromptOpen}>
        <CollapsibleTrigger className="flex w-full items-center justify-between rounded-md px-2 py-1.5 text-sm font-medium hover:bg-accent hover:text-accent-foreground transition-colors">
          <span>Prompt di sistema personalizzato</span>
          <ChevronDown
            className={`h-4 w-4 text-muted-foreground transition-transform duration-200 ${
              promptOpen ? 'rotate-180' : ''
            }`}
          />
        </CollapsibleTrigger>
        <CollapsibleContent className="pt-2">
          <Textarea
            placeholder="Override del prompt di sistema (lascia vuoto per usare il default)..."
            rows={4}
            value={agentConfig.systemPromptOverride ?? ''}
            onChange={e =>
              updateConfig({
                systemPromptOverride: e.target.value || null,
              })
            }
          />
        </CollapsibleContent>
      </Collapsible>
    </div>
  );
}

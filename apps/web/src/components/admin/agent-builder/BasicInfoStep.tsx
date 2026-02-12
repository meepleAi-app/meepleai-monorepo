/**
 * ISSUE-3709: Agent Builder - Basic Info Step
 * Step 1: Agent name, description, type, model, and basic configuration
 */

'use client';

import type { AgentForm } from '@/lib/schemas/agent-definition-schema';
import { AVAILABLE_MODELS } from '@/lib/schemas/agent-definition-schema';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { Textarea } from '@/components/ui/primitives/textarea';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Slider } from '@/components/ui/slider';

interface BasicInfoStepProps {
  agent: AgentForm;
  onChange: (agent: AgentForm) => void;
}

export function BasicInfoStep({ agent, onChange }: BasicInfoStepProps) {
  const handleChange = (field: keyof AgentForm, value: unknown) => {
    onChange({ ...agent, [field]: value });
  };

  return (
    <div className="space-y-6">
      {/* Name */}
      <div className="space-y-2">
        <Label htmlFor="agent-name">
          Name <span className="text-destructive">*</span>
        </Label>
        <Input
          id="agent-name"
          value={agent.name}
          onChange={(e: React.ChangeEvent<HTMLInputElement>) => handleChange('name', e.target.value)}
          placeholder="e.g., Chess Tutor Pro"
          maxLength={100}
          required
        />
        <p className="text-xs text-muted-foreground">
          {agent.name.length}/100 characters
        </p>
      </div>

      {/* Description */}
      <div className="space-y-2">
        <Label htmlFor="agent-description">Description</Label>
        <Textarea
          id="agent-description"
          value={agent.description || ''}
          onChange={(e: React.ChangeEvent<HTMLTextAreaElement>) => handleChange('description', e.target.value)}
          placeholder="Describe your agent's purpose and capabilities..."
          maxLength={1000}
          rows={4}
        />
        <p className="text-xs text-muted-foreground">
          {(agent.description || '').length}/1000 characters
        </p>
      </div>

      {/* Type */}
      <div className="space-y-2">
        <Label htmlFor="agent-type">
          Agent Type <span className="text-destructive">*</span>
        </Label>
        <Select
          value={agent.type}
          onValueChange={(value) => handleChange('type', value)}
        >
          <SelectTrigger id="agent-type">
            <SelectValue placeholder="Select agent type" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="RAG">RAG - Retrieval Augmented Generation</SelectItem>
            <SelectItem value="Citation">Citation - Source attribution</SelectItem>
            <SelectItem value="Confidence">Confidence - Confidence scoring</SelectItem>
            <SelectItem value="RulesInterpreter">Rules Interpreter - Game rules</SelectItem>
            <SelectItem value="Conversation">Conversation - Chat-based</SelectItem>
            <SelectItem value="Custom">Custom - Generic agent</SelectItem>
          </SelectContent>
        </Select>
      </div>

      {/* Model */}
      <div className="space-y-2">
        <Label htmlFor="agent-model">
          Model <span className="text-destructive">*</span>
        </Label>
        <Select
          value={agent.model}
          onValueChange={(value) => handleChange('model', value)}
        >
          <SelectTrigger id="agent-model">
            <SelectValue placeholder="Select LLM model" />
          </SelectTrigger>
          <SelectContent>
            {AVAILABLE_MODELS.map((model) => (
              <SelectItem key={model.value} value={model.value}>
                {model.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {/* Temperature */}
      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <Label htmlFor="agent-temperature">Temperature</Label>
          <span className="text-sm text-muted-foreground">{agent.temperature.toFixed(1)}</span>
        </div>
        <Slider
          id="agent-temperature"
          min={0}
          max={2}
          step={0.1}
          value={[agent.temperature]}
          onValueChange={([value]) => handleChange('temperature', value)}
          className="w-full"
        />
        <div className="flex justify-between text-xs text-muted-foreground">
          <span>Focused (0.0)</span>
          <span>Balanced (1.0)</span>
          <span>Creative (2.0)</span>
        </div>
      </div>

      {/* Max Tokens */}
      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <Label htmlFor="agent-max-tokens">Max Tokens</Label>
          <span className="text-sm text-muted-foreground">{agent.maxTokens.toLocaleString()}</span>
        </div>
        <Slider
          id="agent-max-tokens"
          min={100}
          max={32000}
          step={100}
          value={[agent.maxTokens]}
          onValueChange={([value]) => handleChange('maxTokens', value)}
          className="w-full"
        />
        <div className="flex justify-between text-xs text-muted-foreground">
          <span>100</span>
          <span>16k</span>
          <span>32k</span>
        </div>
      </div>
    </div>
  );
}

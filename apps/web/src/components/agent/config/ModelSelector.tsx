/**
 * Model Selector - AI model selection with tier filtering
 * Issue #3239 (FRONT-003)
 */

'use client';

import { Sparkles } from 'lucide-react';

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { useAgentStore } from '@/stores/agentStore';

interface Model {
  id: string;
  name: string;
  tier: 'free' | 'premium';
  costPerQuery: string;
}

export function ModelSelector() {
  const { selectedModelId, setSelectedModel } = useAgentStore();

  // Mock data - replace with API call filtering by user tier
  const models: Model[] = [
    { id: 'gpt-4o-mini', name: 'GPT-4o Mini', tier: 'free', costPerQuery: '$0.0005' },
    { id: 'llama-3.3', name: 'Llama 3.3', tier: 'free', costPerQuery: '$0.0003' },
    { id: 'gpt-4', name: 'GPT-4', tier: 'premium', costPerQuery: '$0.003' },
    { id: 'claude-3', name: 'Claude 3 Opus', tier: 'premium', costPerQuery: '$0.015' },
  ];

  return (
    <div className="space-y-3">
      <label className="block text-sm font-medium text-slate-200">
        AI Model
        <span className="ml-1 text-red-400">*</span>
      </label>

      <Select value={selectedModelId || ''} onValueChange={setSelectedModel}>
        <SelectTrigger className="w-full bg-slate-900 border-slate-700">
          <SelectValue placeholder="Choose AI model..." />
        </SelectTrigger>
        <SelectContent>
          {models.map(model => (
            <SelectItem key={model.id} value={model.id}>
              <div className="flex items-center justify-between gap-4 w-full">
                <div className="flex items-center gap-2">
                  <Sparkles className="h-4 w-4 text-purple-400" />
                  <span>{model.name}</span>
                </div>
                <div className="flex items-center gap-2">
                  {model.tier === 'premium' && (
                    <span className="text-xs px-2 py-0.5 rounded-full bg-purple-500/20 text-purple-300">
                      Premium
                    </span>
                  )}
                  <span className="text-xs text-slate-400">{model.costPerQuery}/query</span>
                </div>
              </div>
            </SelectItem>
          ))}
        </SelectContent>
      </Select>

      <p className="text-xs text-slate-500">
        Model determines response quality and cost. Premium models provide better reasoning.
      </p>
    </div>
  );
}

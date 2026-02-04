/**
 * Model Tier Selector - Tier-based model selection
 * Issue #3376
 *
 * Displays model tiers (Free, Normal, Premium, Custom) with access control
 * based on user subscription tier.
 */

'use client';

import { Lock, Sparkles, Crown, Settings } from 'lucide-react';

import {
  Select,
  SelectContent,
  SelectGroup,
  SelectItem,
  SelectLabel,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { cn } from '@/lib/utils';
import { useAgentStore } from '@/stores/agentStore';

export type ModelTier = 'free' | 'normal' | 'premium' | 'custom';

interface Model {
  id: string;
  name: string;
  tier: ModelTier;
  costPerQuery: string;
  description: string;
}

interface TierConfig {
  id: ModelTier;
  name: string;
  icon: React.ReactNode;
  color: string;
}

const tiers: TierConfig[] = [
  { id: 'free', name: 'Free', icon: <Sparkles className="h-4 w-4" />, color: 'text-green-400' },
  { id: 'normal', name: 'Normal', icon: <Sparkles className="h-4 w-4" />, color: 'text-blue-400' },
  { id: 'premium', name: 'Premium', icon: <Crown className="h-4 w-4" />, color: 'text-purple-400' },
  { id: 'custom', name: 'Custom', icon: <Settings className="h-4 w-4" />, color: 'text-orange-400' },
];

// Mock data - replace with API call
const models: Model[] = [
  {
    id: 'gpt-4o-mini',
    name: 'GPT-4o Mini',
    tier: 'free',
    costPerQuery: '$0.0005',
    description: 'Fast, efficient for simple queries',
  },
  {
    id: 'llama-3.3',
    name: 'Llama 3.3',
    tier: 'free',
    costPerQuery: '$0.0003',
    description: 'Open source, good performance',
  },
  {
    id: 'claude-3-haiku',
    name: 'Claude 3 Haiku',
    tier: 'normal',
    costPerQuery: '$0.001',
    description: 'Fast and intelligent',
  },
  {
    id: 'gpt-4-turbo',
    name: 'GPT-4 Turbo',
    tier: 'normal',
    costPerQuery: '$0.002',
    description: 'High capability, reasonable cost',
  },
  {
    id: 'claude-3-sonnet',
    name: 'Claude 3 Sonnet',
    tier: 'premium',
    costPerQuery: '$0.005',
    description: 'Balanced performance',
  },
  {
    id: 'gpt-4',
    name: 'GPT-4',
    tier: 'premium',
    costPerQuery: '$0.003',
    description: 'High quality reasoning',
  },
  {
    id: 'claude-3-opus',
    name: 'Claude 3 Opus',
    tier: 'premium',
    costPerQuery: '$0.015',
    description: 'Best quality, complex reasoning',
  },
];

interface ModelTierSelectorProps {
  userTier?: ModelTier;
  isAdmin?: boolean;
}

export function ModelTierSelector({ userTier = 'free', isAdmin = false }: ModelTierSelectorProps) {
  const { selectedModelId, selectedTierId, setSelectedModel, setSelectedTier } = useAgentStore();

  // Filter available tiers based on user tier
  const tierOrder: ModelTier[] = ['free', 'normal', 'premium', 'custom'];
  const userTierIndex = tierOrder.indexOf(userTier);

  const isModelAccessible = (modelTier: ModelTier) => {
    if (isAdmin) return true;
    const modelTierIndex = tierOrder.indexOf(modelTier);
    return modelTierIndex <= userTierIndex;
  };

  const isTierAccessible = (tier: ModelTier) => {
    if (isAdmin) return true;
    if (tier === 'custom') return isAdmin;
    const idx = tierOrder.indexOf(tier);
    return idx <= userTierIndex;
  };

  // Filter models by selected tier
  const filteredModels = selectedTierId
    ? models.filter(m => m.tier === selectedTierId)
    : models.filter(m => isModelAccessible(m.tier));

  const availableTiers = tiers.filter(t => t.id !== 'custom' || isAdmin);

  return (
    <div className="space-y-4">
      {/* Tier Selection */}
      <div className="space-y-2">
        <label className="block text-sm font-medium text-slate-200">Model Tier</label>
        <div className="flex gap-2">
          {availableTiers.map(tier => {
            const accessible = isTierAccessible(tier.id);
            return (
              <button
                key={tier.id}
                onClick={() => accessible && setSelectedTier(tier.id)}
                disabled={!accessible}
                className={cn(
                  'relative flex items-center gap-2 px-3 py-2 rounded-lg border-2 transition-all',
                  selectedTierId === tier.id
                    ? 'border-cyan-400 bg-cyan-500/10'
                    : accessible
                      ? 'border-slate-700 bg-slate-900 hover:border-slate-600'
                      : 'border-slate-800 bg-slate-900/50 opacity-50 cursor-not-allowed'
                )}
              >
                <span className={tier.color}>{tier.icon}</span>
                <span className="text-sm font-medium">{tier.name}</span>
                {!accessible && <Lock className="h-3 w-3 text-slate-500" />}
              </button>
            );
          })}
        </div>
      </div>

      {/* Model Selection */}
      <div className="space-y-2">
        <label className="block text-sm font-medium text-slate-200">
          AI Model
          <span className="ml-1 text-red-400">*</span>
        </label>

        <Select value={selectedModelId || ''} onValueChange={setSelectedModel}>
          <SelectTrigger className="w-full bg-slate-900 border-slate-700">
            <SelectValue placeholder="Choose AI model..." />
          </SelectTrigger>
          <SelectContent>
            {tierOrder
              .filter(tier => tier !== 'custom' || isAdmin)
              .map(tier => {
                const tierModels = filteredModels.filter(m => m.tier === tier);
                if (tierModels.length === 0) return null;

                const tierConfig = tiers.find(t => t.id === tier);

                return (
                  <SelectGroup key={tier}>
                    <SelectLabel className="flex items-center gap-2">
                      <span className={tierConfig?.color}>{tierConfig?.icon}</span>
                      {tierConfig?.name} Models
                    </SelectLabel>
                    {tierModels.map(model => {
                      const accessible = isModelAccessible(model.tier);
                      return (
                        <SelectItem
                          key={model.id}
                          value={model.id}
                          disabled={!accessible}
                          className={!accessible ? 'opacity-50' : ''}
                        >
                          <div className="flex items-center justify-between gap-4 w-full">
                            <div className="flex flex-col">
                              <span className="flex items-center gap-2">
                                {model.name}
                                {!accessible && <Lock className="h-3 w-3 text-slate-500" />}
                              </span>
                              <span className="text-xs text-slate-500">{model.description}</span>
                            </div>
                            <span className="text-xs text-slate-400">{model.costPerQuery}</span>
                          </div>
                        </SelectItem>
                      );
                    })}
                  </SelectGroup>
                );
              })}
          </SelectContent>
        </Select>

        <p className="text-xs text-slate-500">
          Higher tier models provide better reasoning at increased cost.
          {userTier !== 'premium' && ' Upgrade to access premium models.'}
        </p>
      </div>
    </div>
  );
}

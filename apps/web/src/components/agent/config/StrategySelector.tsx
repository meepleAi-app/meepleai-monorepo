/**
 * Strategy Selector - RAG strategy selection component
 * Issue #3376
 *
 * Displays available strategies with descriptions and metrics
 * Backend strategies: FAST, BALANCED, PRECISE, EXPERT, CONSENSUS, CUSTOM
 */

'use client';

import { Zap, Scale, Target, Sparkles, Users, Settings } from 'lucide-react';

import { cn } from '@/lib/utils';
import { useAgentStore } from '@/stores/agentStore';

export type RagStrategy = 'FAST' | 'BALANCED' | 'PRECISE' | 'EXPERT' | 'CONSENSUS' | 'CUSTOM';

interface StrategyConfig {
  id: RagStrategy;
  name: string;
  icon: React.ReactNode;
  description: string;
  latency: string;
  accuracy: string;
  costEstimate: string;
  adminOnly?: boolean;
}

const strategies: StrategyConfig[] = [
  {
    id: 'FAST',
    name: 'Fast',
    icon: <Zap className="h-5 w-5" />,
    description: 'Quick lookups, simple Q&A',
    latency: '~1s',
    accuracy: '75%',
    costEstimate: '$0.001',
  },
  {
    id: 'BALANCED',
    name: 'Balanced',
    icon: <Scale className="h-5 w-5" />,
    description: 'Standard gameplay questions',
    latency: '~2s',
    accuracy: '85%',
    costEstimate: '$0.002',
  },
  {
    id: 'PRECISE',
    name: 'Precise',
    icon: <Target className="h-5 w-5" />,
    description: 'Complex rules interpretation',
    latency: '~4s',
    accuracy: '95%',
    costEstimate: '$0.005',
  },
  {
    id: 'EXPERT',
    name: 'Expert',
    icon: <Sparkles className="h-5 w-5" />,
    description: 'Research, obscure rules',
    latency: '~8s',
    accuracy: '97%',
    costEstimate: '$0.01',
  },
  {
    id: 'CONSENSUS',
    name: 'Consensus',
    icon: <Users className="h-5 w-5" />,
    description: 'Multi-model voting for disputes',
    latency: '~12s',
    accuracy: '99%',
    costEstimate: '$0.03',
  },
  {
    id: 'CUSTOM',
    name: 'Custom',
    icon: <Settings className="h-5 w-5" />,
    description: 'Admin-defined strategy',
    latency: 'Variable',
    accuracy: 'Variable',
    costEstimate: 'Variable',
    adminOnly: true,
  },
];

interface StrategySelectorProps {
  isAdmin?: boolean;
}

export function StrategySelector({ isAdmin = false }: StrategySelectorProps) {
  const { selectedStrategyId, setSelectedStrategy } = useAgentStore();

  const availableStrategies = strategies.filter(s => !s.adminOnly || isAdmin);

  return (
    <div className="space-y-3">
      <label className="block text-sm font-medium text-slate-200">
        Strategy
        <span className="ml-1 text-red-400">*</span>
      </label>

      <div className="grid grid-cols-2 sm:grid-cols-3 gap-2">
        {availableStrategies.map(strategy => (
          <button
            key={strategy.id}
            onClick={() => setSelectedStrategy(strategy.id)}
            className={cn(
              'relative flex flex-col items-center p-3 rounded-lg border-2 transition-all text-center',
              selectedStrategyId === strategy.id
                ? 'border-cyan-400 bg-cyan-500/10 shadow-[0_0_20px_rgba(0,255,255,0.3)]'
                : 'border-slate-700 bg-slate-900 hover:border-slate-600'
            )}
          >
            <div
              className={cn(
                'mb-2',
                selectedStrategyId === strategy.id ? 'text-cyan-400' : 'text-slate-400'
              )}
            >
              {strategy.icon}
            </div>
            <div className="text-sm font-medium text-white">{strategy.name}</div>
            <div className="text-xs text-slate-500 mt-1">{strategy.description}</div>
          </button>
        ))}
      </div>

      {/* Strategy details when selected */}
      {selectedStrategyId && (
        <div className="mt-3 p-3 bg-slate-800/50 rounded-lg border border-slate-700">
          {(() => {
            const selected = strategies.find(s => s.id === selectedStrategyId);
            if (!selected) return null;
            return (
              <div className="flex justify-between text-xs text-slate-400">
                <span>
                  <span className="text-yellow-400">&#9889;</span> {selected.latency}
                </span>
                <span>
                  <span className="text-green-400">&#10004;</span> {selected.accuracy}
                </span>
                <span>
                  <span className="text-purple-400">$</span> {selected.costEstimate}/query
                </span>
              </div>
            );
          })()}
        </div>
      )}

      <p className="text-xs text-slate-500">
        Strategy determines accuracy vs speed trade-off. BALANCED recommended for most users.
      </p>
    </div>
  );
}

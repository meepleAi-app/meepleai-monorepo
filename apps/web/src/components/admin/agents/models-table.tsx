'use client';

import { useState } from 'react';

import { Badge } from '@/components/ui/badge';

interface Model {
  id: string;
  provider: string;
  name: string;
  enabled: boolean;
  costPer1k: number;
  avgLatency: number;
  usage: number;
}

const MOCK_MODELS: Model[] = [
  {
    id: '1',
    provider: 'OpenAI',
    name: 'GPT-4 Turbo',
    enabled: true,
    costPer1k: 0.01,
    avgLatency: 1.2,
    usage: 8420,
  },
  {
    id: '2',
    provider: 'Anthropic',
    name: 'Claude 3.5 Sonnet',
    enabled: true,
    costPer1k: 0.003,
    avgLatency: 1.5,
    usage: 5230,
  },
  {
    id: '3',
    provider: 'OpenAI',
    name: 'GPT-3.5 Turbo',
    enabled: true,
    costPer1k: 0.001,
    avgLatency: 0.8,
    usage: 3150,
  },
  {
    id: '4',
    provider: 'Google',
    name: 'Gemini Pro',
    enabled: false,
    costPer1k: 0.00025,
    avgLatency: 1.1,
    usage: 420,
  },
  {
    id: '5',
    provider: 'Anthropic',
    name: 'Claude 3 Haiku',
    enabled: true,
    costPer1k: 0.00025,
    avgLatency: 0.6,
    usage: 2840,
  },
];

export function ModelsTable() {
  const [models, setModels] = useState(MOCK_MODELS);

  const handleToggle = (id: string) => {
    setModels((prev) =>
      prev.map((m) => (m.id === id ? { ...m, enabled: !m.enabled } : m))
    );
  };

  return (
    <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl border border-amber-200/50 dark:border-zinc-700/50 overflow-hidden">
      <div className="p-6 border-b border-slate-200 dark:border-zinc-700">
        <h2 className="font-quicksand text-xl font-bold text-slate-900 dark:text-zinc-100">
          AI Models
        </h2>
      </div>
      <div className="overflow-x-auto">
        <table className="w-full">
          <thead className="bg-amber-100/50 dark:bg-zinc-900/50 border-b border-amber-200/50 dark:border-zinc-700/50">
            <tr>
              <th className="text-left py-3 px-4 text-sm font-bold text-amber-900 dark:text-amber-400 uppercase">
                Provider
              </th>
              <th className="text-left py-3 px-4 text-sm font-bold text-amber-900 dark:text-amber-400 uppercase">
                Model
              </th>
              <th className="text-center py-3 px-4 text-sm font-bold text-amber-900 dark:text-amber-400 uppercase">
                Status
              </th>
              <th className="text-right py-3 px-4 text-sm font-bold text-amber-900 dark:text-amber-400 uppercase">
                Cost/1k
              </th>
              <th className="text-right py-3 px-4 text-sm font-bold text-amber-900 dark:text-amber-400 uppercase">
                Latency
              </th>
              <th className="text-right py-3 px-4 text-sm font-bold text-amber-900 dark:text-amber-400 uppercase">
                Usage
              </th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-200 dark:divide-zinc-700">
            {models.map((model) => (
              <tr key={model.id} className="hover:bg-slate-50/50 dark:hover:bg-zinc-900/50">
                <td className="py-3 px-4">
                  <Badge variant="outline" className="bg-gray-100 text-gray-900 dark:bg-gray-900/30 dark:text-gray-300">
                    {model.provider}
                  </Badge>
                </td>
                <td className="py-3 px-4 font-medium text-slate-900 dark:text-zinc-100">
                  {model.name}
                </td>
                <td className="py-3 px-4 text-center">
                  <button
                    onClick={() => handleToggle(model.id)}
                    className={`relative inline-flex h-6 w-11 items-center rounded-full transition-colors ${
                      model.enabled
                        ? 'bg-green-500 dark:bg-green-600'
                        : 'bg-gray-200 dark:bg-zinc-700'
                    }`}
                  >
                    <span
                      className={`inline-block h-4 w-4 transform rounded-full bg-white transition-transform ${
                        model.enabled ? 'translate-x-6' : 'translate-x-1'
                      }`}
                    />
                    <span className="sr-only">Toggle {model.name}</span>
                  </button>
                </td>
                <td className="py-3 px-4 text-right font-mono text-sm text-slate-600 dark:text-zinc-400">
                  ${model.costPer1k.toFixed(4)}
                </td>
                <td className="py-3 px-4 text-right font-mono text-sm text-slate-600 dark:text-zinc-400">
                  {model.avgLatency}s
                </td>
                <td className="py-3 px-4 text-right">
                  <Badge variant="outline" className="bg-blue-100 text-blue-900 dark:bg-blue-900/30 dark:text-blue-300">
                    {model.usage.toLocaleString()}
                  </Badge>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

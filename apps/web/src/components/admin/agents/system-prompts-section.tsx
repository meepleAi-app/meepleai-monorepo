'use client';

import { EyeIcon, PencilIcon } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';

interface SystemPrompt {
  id: string;
  title: string;
  description: string;
  tokenCount: number;
  lastUpdated: string;
}

const MOCK_PROMPTS: SystemPrompt[] = [
  {
    id: '1',
    title: 'Rules Expert',
    description: 'Specialized in explaining game rules and resolving rule disputes',
    tokenCount: 2847,
    lastUpdated: '2026-02-15',
  },
  {
    id: '2',
    title: 'Strategy Advisor',
    description: 'Provides tactical advice and strategic recommendations for competitive play',
    tokenCount: 3102,
    lastUpdated: '2026-02-12',
  },
  {
    id: '3',
    title: 'Game Recommender',
    description: 'Suggests games based on player preferences, group size, and complexity',
    tokenCount: 2654,
    lastUpdated: '2026-02-10',
  },
];

export function SystemPromptsSection() {
  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="font-quicksand text-xl font-bold text-slate-900 dark:text-zinc-100">
          System Prompts
        </h2>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        {MOCK_PROMPTS.map((prompt) => (
          <div
            key={prompt.id}
            className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl p-6 border border-slate-200/50 dark:border-zinc-700/50"
          >
            <div className="flex items-start justify-between mb-3">
              <h3 className="font-quicksand text-lg font-bold text-slate-900 dark:text-zinc-100">
                {prompt.title}
              </h3>
              <Badge variant="outline" className="bg-amber-100 text-amber-900 dark:bg-amber-900/30 dark:text-amber-300">
                {prompt.tokenCount} tokens
              </Badge>
            </div>
            <p className="text-sm text-slate-600 dark:text-zinc-400 mb-4">
              {prompt.description}
            </p>
            <div className="flex items-center justify-between">
              <span className="text-xs text-slate-500 dark:text-zinc-500">
                Updated {prompt.lastUpdated}
              </span>
              <div className="flex gap-2">
                <Button variant="ghost" size="sm" className="h-8 w-8 p-0">
                  <EyeIcon className="h-4 w-4" />
                  <span className="sr-only">View prompt</span>
                </Button>
                <Button variant="ghost" size="sm" className="h-8 w-8 p-0">
                  <PencilIcon className="h-4 w-4" />
                  <span className="sr-only">Edit prompt</span>
                </Button>
              </div>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

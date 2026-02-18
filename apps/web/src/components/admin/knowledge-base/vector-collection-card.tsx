'use client';

import { DatabaseIcon } from 'lucide-react';

import { Badge } from '@/components/ui/badge';

interface VectorCollectionCardProps {
  name: string;
  vectorCount: number;
  dimensions: number;
  storage: string;
  health: number;
}

export function VectorCollectionCard({
  name,
  vectorCount,
  dimensions,
  storage,
  health,
}: VectorCollectionCardProps) {
  const healthColor =
    health >= 90
      ? 'bg-green-500'
      : health >= 70
        ? 'bg-amber-500'
        : 'bg-red-500';

  return (
    <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl p-6 border border-slate-200/50 dark:border-zinc-700/50">
      <div className="flex items-start justify-between mb-4">
        <div className="flex items-center gap-3">
          <div className="w-12 h-12 bg-blue-100 dark:bg-blue-900/30 rounded-lg flex items-center justify-center">
            <DatabaseIcon className="w-6 h-6 text-blue-700 dark:text-blue-400" />
          </div>
          <div>
            <h3 className="font-quicksand text-lg font-bold text-slate-900 dark:text-zinc-100">
              {name}
            </h3>
            <p className="text-xs text-slate-500 dark:text-zinc-400">{dimensions}d vectors</p>
          </div>
        </div>
        <Badge variant="outline" className={health >= 90 ? 'bg-green-100 text-green-900 dark:bg-green-900/30 dark:text-green-300' : 'bg-amber-100 text-amber-900 dark:bg-amber-900/30 dark:text-amber-300'}>
          {health}% healthy
        </Badge>
      </div>

      <div className="space-y-3">
        <div className="flex justify-between text-sm">
          <span className="text-slate-600 dark:text-zinc-400">Vectors</span>
          <span className="font-semibold text-slate-900 dark:text-zinc-100">
            {vectorCount.toLocaleString()}
          </span>
        </div>
        <div className="flex justify-between text-sm">
          <span className="text-slate-600 dark:text-zinc-400">Storage</span>
          <span className="font-semibold text-slate-900 dark:text-zinc-100">{storage}</span>
        </div>
      </div>

      {/* Health Bar */}
      <div className="mt-4">
        <div className="h-2 bg-gray-200 dark:bg-zinc-700 rounded-full overflow-hidden">
          <div
            className={`h-full ${healthColor} transition-all duration-300`}
            style={{ width: `${health}%` }}
          />
        </div>
      </div>
    </div>
  );
}

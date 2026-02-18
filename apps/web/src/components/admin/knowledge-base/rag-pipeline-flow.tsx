'use client';

import { ArrowRightIcon } from 'lucide-react';

const PIPELINE_STAGES = [
  { id: 'ingest', label: 'Ingest', health: 'healthy' },
  { id: 'chunk', label: 'Chunk', health: 'healthy' },
  { id: 'embed', label: 'Embed', health: 'healthy' },
  { id: 'store', label: 'Store', health: 'healthy' },
  { id: 'retrieve', label: 'Retrieve', health: 'warning' },
  { id: 'rerank', label: 'Rerank', health: 'healthy' },
  { id: 'generate', label: 'Generate', health: 'healthy' },
];

export function RAGPipelineFlow() {
  return (
    <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl p-8 border border-amber-200/50 dark:border-zinc-700/50">
      <h2 className="font-quicksand text-xl font-bold text-slate-900 dark:text-zinc-100 mb-8">
        RAG Pipeline Flow
      </h2>

      {/* Pipeline Visualization */}
      <div className="flex items-center justify-between gap-4 mb-8 overflow-x-auto pb-4">
        {PIPELINE_STAGES.map((stage, index) => (
          <div key={stage.id} className="flex items-center gap-4">
            {/* Stage Card */}
            <div className="flex flex-col items-center">
              <div className="bg-slate-50 dark:bg-zinc-900 rounded-lg p-4 border-2 border-amber-400/40 dark:border-amber-600/40 min-w-[100px]">
                <div className="text-center">
                  <div className="font-semibold text-slate-900 dark:text-zinc-100 text-sm mb-2">
                    {stage.label}
                  </div>
                  <div
                    className={`w-3 h-3 rounded-full mx-auto ${
                      stage.health === 'healthy'
                        ? 'bg-green-500 shadow-[0_0_8px_rgba(16,185,129,0.5)]'
                        : 'bg-amber-500 shadow-[0_0_8px_rgba(245,158,11,0.5)]'
                    } animate-pulse`}
                  />
                </div>
              </div>
            </div>

            {/* Arrow */}
            {index < PIPELINE_STAGES.length - 1 && (
              <ArrowRightIcon className="w-6 h-6 text-amber-500 dark:text-amber-400 flex-shrink-0" />
            )}
          </div>
        ))}
      </div>

      {/* Health Summary */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <div className="bg-green-50 dark:bg-green-900/20 rounded-lg p-4 border border-green-200 dark:border-green-800">
          <div className="text-sm font-medium text-green-900 dark:text-green-300">Healthy Stages</div>
          <div className="text-2xl font-bold text-green-700 dark:text-green-400">6/7</div>
        </div>
        <div className="bg-amber-50 dark:bg-amber-900/20 rounded-lg p-4 border border-amber-200 dark:border-amber-800">
          <div className="text-sm font-medium text-amber-900 dark:text-amber-300">Warnings</div>
          <div className="text-2xl font-bold text-amber-700 dark:text-amber-400">1</div>
        </div>
        <div className="bg-slate-50 dark:bg-zinc-900 rounded-lg p-4 border border-slate-200 dark:border-zinc-700">
          <div className="text-sm font-medium text-slate-900 dark:text-zinc-100">Uptime</div>
          <div className="text-2xl font-bold text-slate-700 dark:text-zinc-300">99.2%</div>
        </div>
      </div>
    </div>
  );
}

import { Suspense } from 'react';

import { type Metadata } from 'next';

import { RAGPipelineFlow } from '@/components/admin/knowledge-base/rag-pipeline-flow';

export const metadata: Metadata = {
  title: 'RAG Pipeline',
  description: 'Monitor the RAG pipeline stages and health',
};

function CardSkeleton({ height = 'h-[400px]' }: { height?: string }) {
  return (
    <div className={`${height} bg-white/40 dark:bg-zinc-800/40 backdrop-blur-sm rounded-2xl border border-slate-200/60 dark:border-zinc-700/40 animate-pulse`} />
  );
}

export default function PipelineOverviewPage() {
  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
          RAG Pipeline Overview
        </h1>
        <p className="text-sm text-muted-foreground mt-1">
          Monitor the RAG pipeline stages and health
        </p>
      </div>

      {/* Pipeline Flow */}
      <Suspense fallback={<CardSkeleton height="h-[500px]" />}>
        <RAGPipelineFlow />
      </Suspense>

      {/* Recent Activity */}
      <Suspense fallback={<CardSkeleton height="h-[300px]" />}>
        <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl p-6 border border-slate-200/50 dark:border-zinc-700/50">
          <h2 className="font-quicksand text-xl font-bold text-slate-900 dark:text-zinc-100 mb-4">
            Recent Activity
          </h2>
          <p className="text-sm text-slate-600 dark:text-zinc-400">
            Recent pipeline activity will be displayed here
          </p>
        </div>
      </Suspense>
    </div>
  );
}

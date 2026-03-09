import { Suspense } from 'react';

import { PlusIcon } from 'lucide-react';
import { type Metadata } from 'next';

import { ModelsTable } from '@/components/admin/agents/models-table';
import { SystemPromptsSection } from '@/components/admin/agents/system-prompts-section';
import { Button } from '@/components/ui/button';

export const metadata: Metadata = {
  title: 'Models & Prompts',
  description: 'Configure AI models and system prompts',
};

function CardSkeleton({ height = 'h-[400px]' }: { height?: string }) {
  return (
    <div
      className={`${height} bg-white/40 dark:bg-zinc-800/40 backdrop-blur-sm rounded-2xl border border-slate-200/60 dark:border-zinc-700/40 animate-pulse`}
    />
  );
}

export default function ModelsPromptsPage() {
  return (
    <div className="space-y-8">
      {/* Page Header */}
      <div className="flex items-center justify-between flex-wrap gap-4">
        <div>
          <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
            Models & Prompts
          </h1>
          <p className="text-sm text-muted-foreground mt-1">
            Configure AI models and system prompts
          </p>
        </div>
        <Button className="bg-amber-500 hover:bg-amber-600 text-white">
          <PlusIcon className="h-4 w-4 mr-2" />
          Add Model
        </Button>
      </div>

      {/* Models Table */}
      <Suspense fallback={<CardSkeleton />}>
        <ModelsTable />
      </Suspense>

      {/* System Prompts */}
      <Suspense fallback={<CardSkeleton height="h-[300px]" />}>
        <SystemPromptsSection />
      </Suspense>
    </div>
  );
}

import { Suspense } from 'react';

import { type Metadata } from 'next';

import { KBSettings } from '@/components/admin/knowledge-base/kb-settings';

export const metadata: Metadata = {
  title: 'Settings',
  description: 'Configure knowledge base and RAG pipeline settings',
};

function CardSkeleton({ height = 'h-[600px]' }: { height?: string }) {
  return (
    <div className={`${height} bg-white/40 dark:bg-zinc-800/40 backdrop-blur-sm rounded-2xl border border-slate-200/60 dark:border-zinc-700/40 animate-pulse`} />
  );
}

export default function KBSettingsPage() {
  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
          Knowledge Base Settings
        </h1>
        <p className="text-sm text-muted-foreground mt-1">
          Configure knowledge base and RAG pipeline settings
        </p>
      </div>

      {/* Settings */}
      <Suspense fallback={<CardSkeleton />}>
        <KBSettings />
      </Suspense>
    </div>
  );
}

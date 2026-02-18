import { Suspense } from 'react';

import { type Metadata } from 'next';

import { UploadZone } from '@/components/admin/knowledge-base/upload-zone';
import { UploadSettings } from '@/components/admin/knowledge-base/upload-settings';
import { ProcessingQueue } from '@/components/admin/knowledge-base/processing-queue';

export const metadata: Metadata = {
  title: 'Upload & Process',
  description: 'Upload and process documents for the knowledge base',
};

function CardSkeleton({ height = 'h-[300px]' }: { height?: string }) {
  return (
    <div className={`${height} bg-white/40 dark:bg-zinc-800/40 backdrop-blur-sm rounded-2xl border border-slate-200/60 dark:border-zinc-700/40 animate-pulse`} />
  );
}

export default function UploadProcessPage() {
  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
          Upload & Process
        </h1>
        <p className="text-sm text-muted-foreground mt-1">
          Upload and process documents for the knowledge base
        </p>
      </div>

      {/* Upload Zone */}
      <Suspense fallback={<CardSkeleton height="h-[200px]" />}>
        <UploadZone />
      </Suspense>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Settings */}
        <Suspense fallback={<CardSkeleton />}>
          <UploadSettings />
        </Suspense>

        {/* Processing Queue */}
        <Suspense fallback={<CardSkeleton />}>
          <ProcessingQueue />
        </Suspense>
      </div>
    </div>
  );
}

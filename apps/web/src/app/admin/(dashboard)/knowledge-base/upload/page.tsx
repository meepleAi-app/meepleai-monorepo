/* eslint-disable local/no-hardcoded-color-utility -- text-white / button color on style-prop colored bg or admin-decorative inline gradient; DS-13a admin scope, mockup .e-bg pattern. Future: extract --admin-* token family (deferred to DS-15 audit). */
import { Suspense } from 'react';

import { type Metadata } from 'next';

import { KbIdempotencyGuard } from '@/components/admin/knowledge-base/kb-idempotency-guard';
import { ProcessingQueue } from '@/components/admin/knowledge-base/processing-queue';
import { UploadSettings } from '@/components/admin/knowledge-base/upload-settings';
import { UploadZone } from '@/components/admin/knowledge-base/upload-zone';

export const metadata: Metadata = {
  title: 'Upload & Process',
  description: 'Upload and process documents for the knowledge base',
};

function CardSkeleton({ height = 'h-[300px]' }: { height?: string }) {
  return (
    <div
      className={`${height} bg-card/40 dark:bg-zinc-800/40 backdrop-blur-sm rounded-2xl border border-border/60 dark:border-zinc-700/40 animate-pulse`}
    />
  );
}

export default async function UploadProcessPage({
  searchParams,
}: {
  searchParams: Promise<{ gameId?: string }>;
}) {
  const { gameId } = await searchParams;

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

      {/* Idempotency guard — avvisa se il gioco ha già KB/snapshot */}
      {gameId && <KbIdempotencyGuard gameId={gameId} />}

      {/* Upload Zone */}
      <Suspense fallback={<CardSkeleton height="h-[200px]" />}>
        <UploadZone initialGameId={gameId} />
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

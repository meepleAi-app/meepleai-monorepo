/**
 * RAG Setup Page - Admin Dashboard
 *
 * Route: /admin/shared-games/[id]/rag-setup
 * Single-page dashboard for RAG configuration:
 * upload PDFs, monitor embedding, create agent, test chat.
 */

import { Suspense } from 'react';

import { type Metadata } from 'next';

import { Skeleton } from '@/components/ui/feedback/skeleton';

import { RagSetupClient } from './client';

interface RagSetupPageProps {
  params: Promise<{ id: string }>;
}

export const metadata: Metadata = {
  title: 'RAG Setup',
  description: 'Configura RAG per un gioco condiviso',
};

function LoadingSkeleton() {
  return (
    <div className="space-y-6">
      <div className="flex items-center gap-4">
        <Skeleton className="h-9 w-9 rounded-md" />
        <div className="space-y-2">
          <Skeleton className="h-8 w-64" />
          <Skeleton className="h-4 w-48" />
        </div>
      </div>
      <Skeleton className="h-16 w-full rounded-xl" />
      <div className="grid gap-6 lg:grid-cols-2">
        <div className="space-y-6">
          <Skeleton className="h-64 w-full rounded-xl" />
          <Skeleton className="h-48 w-full rounded-xl" />
        </div>
        <div className="space-y-6">
          <Skeleton className="h-80 w-full rounded-xl" />
          <Skeleton className="h-[480px] w-full rounded-xl" />
        </div>
      </div>
    </div>
  );
}

export default function RagSetupPage({ params }: RagSetupPageProps) {
  return (
    <Suspense fallback={<LoadingSkeleton />}>
      <RagSetupClient params={params} />
    </Suspense>
  );
}

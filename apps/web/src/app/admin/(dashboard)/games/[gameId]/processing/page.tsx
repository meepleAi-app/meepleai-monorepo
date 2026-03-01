'use client';

import { use } from 'react';

import { ProcessingMonitor } from '@/components/admin/games/processing/ProcessingMonitor';

interface ProcessingPageProps {
  params: Promise<{ gameId: string }>;
  searchParams: Promise<{ title?: string }>;
}

export default function ProcessingPage({ params, searchParams }: ProcessingPageProps) {
  const { gameId } = use(params);
  const { title } = use(searchParams);

  return (
    <div className="mx-auto max-w-2xl py-8">
      <ProcessingMonitor gameId={gameId} gameTitle={title} />
    </div>
  );
}

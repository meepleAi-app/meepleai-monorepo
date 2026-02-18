'use client';

import { use } from 'react';

import { ProcessingMonitor } from '@/components/admin/games/processing/ProcessingMonitor';

interface ProcessingPageProps {
  params: Promise<{ id: string }>;
  searchParams: Promise<{ title?: string }>;
}

export default function ProcessingPage({ params, searchParams }: ProcessingPageProps) {
  const { id } = use(params);
  const { title } = use(searchParams);

  return (
    <div className="mx-auto max-w-2xl py-8">
      <ProcessingMonitor gameId={id} gameTitle={title} />
    </div>
  );
}

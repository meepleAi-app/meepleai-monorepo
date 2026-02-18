'use client';

import { use } from 'react';

import { AgentTestingPage } from '@/components/admin/games/agent-test/AgentTestingPage';

interface AgentTestPageProps {
  params: Promise<{ id: string }>;
  searchParams: Promise<{ title?: string }>;
}

export default function AgentTestPage({ params, searchParams }: AgentTestPageProps) {
  const { id } = use(params);
  const { title } = use(searchParams);

  return (
    <div className="mx-auto max-w-4xl py-8">
      <AgentTestingPage gameId={id} gameTitle={title} />
    </div>
  );
}

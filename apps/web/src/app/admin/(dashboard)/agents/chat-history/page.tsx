import { Suspense } from 'react';

import { type Metadata } from 'next';

import { ChatHistoryFilters } from '@/components/admin/agents/chat-history-filters';
import { ChatHistoryTable } from '@/components/admin/agents/chat-history-table';

export const metadata: Metadata = {
  title: 'Chat History',
  description: 'Review agent conversations and user satisfaction',
};

function CardSkeleton({ height = 'h-[600px]' }: { height?: string }) {
  return (
    <div
      className={`${height} bg-white/40 dark:bg-zinc-800/40 backdrop-blur-sm rounded-2xl border border-slate-200/60 dark:border-zinc-700/40 animate-pulse`}
    />
  );
}

export default function ChatHistoryPage() {
  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div>
        <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
          Chat History
        </h1>
        <p className="text-sm text-muted-foreground mt-1">
          Review agent conversations and user satisfaction
        </p>
      </div>

      {/* Filters */}
      <Suspense fallback={<CardSkeleton height="h-[120px]" />}>
        <ChatHistoryFilters />
      </Suspense>

      {/* Chat Table */}
      <Suspense fallback={<CardSkeleton />}>
        <ChatHistoryTable />
      </Suspense>
    </div>
  );
}

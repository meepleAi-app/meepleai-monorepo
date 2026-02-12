'use client';

import { MessageSquare, Clock } from 'lucide-react';
import type { AgentMetadata } from '@/types/agent';

export function AgentStatsDisplay({ metadata }: { metadata: AgentMetadata }) {
  const formatCount = (n: number) => n < 1000 ? n.toString() : `${(n / 1000).toFixed(1)}K`;

  return (
    <div className="flex gap-3 text-xs text-muted-foreground">
      <div className="flex items-center gap-1">
        <MessageSquare className="w-3.5 h-3.5" />
        <span className="font-medium">{formatCount(metadata.invocationCount)}</span>
      </div>
      {metadata.lastExecuted && (
        <div className="flex items-center gap-1">
          <Clock className="w-3.5 h-3.5" />
          <span>{new Date(metadata.lastExecuted).toLocaleString()}</span>
        </div>
      )}
    </div>
  );
}

'use client';

import { MessageSquare, Clock, Brain, Eye, Code2 } from 'lucide-react';
import type { AgentMetadata } from '@/types/agent';
import { AgentStatusBadge } from './AgentStatusBadge';
import { cn } from '@/lib/utils';

const CAPABILITY_ICONS = {
  RAG: Brain,
  Vision: Eye,
  Code: Code2,
  Functions: MessageSquare,
  MultiTurn: MessageSquare
};

const CAPABILITY_COLORS = {
  RAG: 'hsl(38 92% 50%)',      // Amber
  Vision: 'hsl(262 83% 58%)',  // Purple
  Code: 'hsl(210 40% 55%)',    // Slate
  Functions: 'hsl(221 83% 53%)', // Blue
  MultiTurn: 'hsl(142 76% 36%)'  // Green
};

export function AgentStatsDisplay({ metadata }: { metadata: AgentMetadata }) {
  const formatCount = (n: number) => n < 1000 ? n.toString() : `${(n / 1000).toFixed(1)}K`;

  return (
    <div className="flex flex-col gap-2">
      {/* Status Badge */}
      <AgentStatusBadge status={metadata.status} />

      {/* Stats Row */}
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
        {metadata.avgResponseTime !== undefined && (
          <div className="flex items-center gap-1">
            <Clock className="w-3.5 h-3.5" />
            <span>{metadata.avgResponseTime}ms avg</span>
          </div>
        )}
      </div>

      {/* Capabilities Tags (Epic #4068 - Issue #4184) */}
      {metadata.capabilities && metadata.capabilities.length > 0 && (
        <div className="flex flex-wrap gap-1.5">
          {metadata.capabilities.map((cap) => {
            const Icon = CAPABILITY_ICONS[cap];
            const color = CAPABILITY_COLORS[cap];

            return (
              <div
                key={cap}
                className={cn(
                  'inline-flex items-center gap-1 px-2 py-0.5 rounded-md',
                  'text-[10px] font-semibold border backdrop-blur-sm'
                )}
                style={{
                  color,
                  backgroundColor: `${color.replace(')', ' / 0.1)')}`,
                  borderColor: color
                }}
                role="status"
                aria-label={`Capability: ${cap}`}
              >
                <Icon className="w-3 h-3" />
                <span>{cap}</span>
              </div>
            );
          })}
        </div>
      )}

      {/* Model Information (Epic #4068 - Issue #4184) */}
      {metadata.model && (
        <div className="text-xs text-muted-foreground">
          <span className="font-medium">{metadata.model.name}</span>
          {metadata.model.temperature !== undefined && (
            <span className="ml-2">• temp: {metadata.model.temperature}</span>
          )}
        </div>
      )}
    </div>
  );
}

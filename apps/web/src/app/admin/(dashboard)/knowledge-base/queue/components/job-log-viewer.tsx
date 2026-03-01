'use client';

import { ScrollArea } from '@/components/ui/primitives/scroll-area';

import type { StepLogEntryDto } from '../lib/queue-api';

interface JobLogViewerProps {
  logs: StepLogEntryDto[];
}

function getLevelColor(level: string): string {
  switch (level) {
    case 'Error':
      return 'text-red-500';
    case 'Warning':
      return 'text-amber-500';
    default:
      return 'text-muted-foreground';
  }
}

function formatTimestamp(ts: string): string {
  const d = new Date(ts);
  return d.toLocaleTimeString('en-US', {
    hour12: false,
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  });
}

export function JobLogViewer({ logs }: JobLogViewerProps) {
  if (logs.length === 0) {
    return (
      <p className="text-sm text-muted-foreground py-4">No log entries.</p>
    );
  }

  return (
    <ScrollArea className="h-[200px]">
      <div className="font-mono text-xs space-y-0.5 p-2 bg-slate-50/50 dark:bg-zinc-900/50 rounded-lg">
        {logs.map((entry) => (
          <div key={entry.id} className="flex gap-2">
            <span className="text-muted-foreground shrink-0">
              {formatTimestamp(entry.timestamp)}
            </span>
            <span className={`shrink-0 w-12 ${getLevelColor(entry.level)}`}>
              [{entry.level}]
            </span>
            <span className="text-foreground break-all">{entry.message}</span>
          </div>
        ))}
      </div>
    </ScrollArea>
  );
}

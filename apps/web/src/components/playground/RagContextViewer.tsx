'use client';

import { FileText } from 'lucide-react';

import { Badge } from '@/components/ui';
import { ScrollArea } from '@/components/ui';
import { cn } from '@/lib/utils';
import { usePlaygroundStore } from '@/stores/playground-store';

function scoreColor(score: number): string {
  if (score >= 0.9) return 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400';
  if (score >= 0.7) return 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900/30 dark:text-yellow-400';
  return 'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400';
}

export function RagContextViewer() {
  const { citations } = usePlaygroundStore();

  if (citations.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center h-full text-muted-foreground py-8">
        <FileText className="h-8 w-8 mb-2 opacity-50" />
        <p className="text-sm">No RAG context yet</p>
        <p className="text-xs mt-1">Citations appear here during agent responses</p>
      </div>
    );
  }

  return (
    <ScrollArea className="h-full">
      <div className="space-y-3 p-1">
        <div className="text-xs text-muted-foreground font-medium">
          {citations.length} citation{citations.length !== 1 ? 's' : ''} retrieved
        </div>

        {citations.map((citation, index) => (
          <div
            key={index}
            className="border rounded-lg p-3 text-sm space-y-2 bg-background"
          >
            {/* Header: source + score */}
            <div className="flex items-center justify-between gap-2">
              <div className="flex items-center gap-1.5 min-w-0">
                <FileText className="h-3.5 w-3.5 shrink-0 text-muted-foreground" />
                <span className="font-medium truncate" title={citation.source}>
                  {citation.source}
                </span>
              </div>
              <Badge
                variant="secondary"
                className={cn('shrink-0 text-xs font-mono', scoreColor(citation.score))}
              >
                {(citation.score * 100).toFixed(0)}%
              </Badge>
            </div>

            {/* Location */}
            <div className="text-xs text-muted-foreground">
              Page {citation.page}, Line {citation.line}
            </div>

            {/* Snippet text */}
            <div className="text-xs bg-muted/50 rounded p-2 leading-relaxed whitespace-pre-wrap">
              {citation.text}
            </div>
          </div>
        ))}
      </div>
    </ScrollArea>
  );
}

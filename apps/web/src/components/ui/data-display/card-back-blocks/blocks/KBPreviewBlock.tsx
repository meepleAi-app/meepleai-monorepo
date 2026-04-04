'use client';

import { memo } from 'react';

import { cn } from '@/lib/utils';

interface KBPreviewBlockProps {
  title: string;
  entityColor: string;
  data: {
    type: 'kbPreview';
    docsCount: number;
    chunksCount: number;
    lastQuery?: string;
    indexStatus: string;
  };
}

function getStatusVariant(status: string): string {
  switch (status.toLowerCase()) {
    case 'indexed':
      return 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400';
    case 'indexing':
      return 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900/30 dark:text-yellow-400';
    case 'error':
      return 'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400';
    default:
      return 'bg-muted text-muted-foreground';
  }
}

export const KBPreviewBlock = memo(function KBPreviewBlock({
  title,
  entityColor,
  data,
}: KBPreviewBlockProps) {
  const { docsCount, chunksCount, lastQuery, indexStatus } = data;

  return (
    <div className="flex flex-col gap-2">
      {title && (
        <>
          <h4
            className="text-xs font-semibold uppercase tracking-wide"
            style={{ color: `hsl(${entityColor})` }}
          >
            {title}
          </h4>
          <div className="h-px w-full" style={{ backgroundColor: `hsl(${entityColor} / 0.2)` }} />
        </>
      )}

      <div className="flex flex-col gap-1.5 text-xs">
        <div className="flex items-center justify-between">
          <span className="text-muted-foreground">Documenti</span>
          <span className="font-semibold tabular-nums">{docsCount}</span>
        </div>
        <div className="flex items-center justify-between">
          <span className="text-muted-foreground">Chunks</span>
          <span className="font-semibold tabular-nums">{chunksCount}</span>
        </div>
        <div className="flex items-center justify-between">
          <span className="text-muted-foreground">Stato</span>
          <span
            className={cn(
              'rounded-full px-2 py-0.5 text-[10px] font-medium capitalize',
              getStatusVariant(indexStatus)
            )}
          >
            {indexStatus}
          </span>
        </div>
        {lastQuery && (
          <div className="mt-1 border-t pt-1" style={{ borderColor: `hsl(${entityColor} / 0.15)` }}>
            <p className="text-[10px] text-muted-foreground">Ultima query</p>
            <p className="mt-0.5 line-clamp-2 text-foreground">{lastQuery}</p>
          </div>
        )}
      </div>
    </div>
  );
});

KBPreviewBlock.displayName = 'KBPreviewBlock';

'use client';

import { memo } from 'react';

interface HistoryEntry {
  timestamp: string;
  message: string;
  sender?: string;
}

interface HistoryBlockProps {
  title: string;
  entityColor: string;
  data: {
    type: 'history';
    entries: HistoryEntry[];
  };
}

export const HistoryBlock = memo(function HistoryBlock({
  title,
  entityColor,
  data,
}: HistoryBlockProps) {
  const { entries } = data;

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

      {entries.length === 0 ? (
        <p className="text-xs text-muted-foreground italic">No data yet</p>
      ) : (
        <ul className="flex flex-col gap-1.5">
          {entries.map((entry, idx) => (
            <li key={idx} className="flex flex-col gap-0.5 text-xs">
              <div className="flex items-baseline gap-1.5">
                <span className="shrink-0 font-mono text-[10px] tabular-nums text-muted-foreground">
                  {entry.timestamp}
                </span>
                {entry.sender && (
                  <span className="shrink-0 font-semibold text-foreground">{entry.sender}</span>
                )}
              </div>
              <p className="text-foreground/80">{entry.message}</p>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
});

HistoryBlock.displayName = 'HistoryBlock';

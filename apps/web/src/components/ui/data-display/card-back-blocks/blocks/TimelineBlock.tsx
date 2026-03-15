'use client';

import { memo } from 'react';

import { cn } from '@/lib/utils';

interface TimelineEvent {
  time: string;
  label: string;
  icon?: string;
}

interface TimelineBlockProps {
  title: string;
  entityColor: string;
  data: {
    type: 'timeline';
    events: TimelineEvent[];
  };
}

export const TimelineBlock = memo(function TimelineBlock({
  title,
  entityColor,
  data,
}: TimelineBlockProps) {
  const { events } = data;

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

      {events.length === 0 ? (
        <p className="text-xs text-muted-foreground italic">No data yet</p>
      ) : (
        <ol className="flex flex-col gap-1.5">
          {events.map((event, idx) => (
            <li key={idx} className="flex items-start gap-2 text-xs">
              <span
                className={cn(
                  'shrink-0 w-12 text-right font-mono tabular-nums',
                  'text-muted-foreground'
                )}
              >
                {event.time}
              </span>
              {event.icon && (
                <span className="shrink-0" aria-hidden="true">
                  {event.icon}
                </span>
              )}
              <span className="text-foreground">{event.label}</span>
            </li>
          ))}
        </ol>
      )}
    </div>
  );
});

TimelineBlock.displayName = 'TimelineBlock';

'use client';

import { memo } from 'react';

interface Milestone {
  at: number;
  label: string;
}

interface ProgressBlockProps {
  title: string;
  entityColor: string;
  data: {
    type: 'progress';
    current: number;
    target: number;
    label: string;
    milestones?: Milestone[];
  };
}

export const ProgressBlock = memo(function ProgressBlock({
  title,
  entityColor,
  data,
}: ProgressBlockProps) {
  const { current, target, label, milestones } = data;

  const percentage = target > 0 ? Math.min(100, Math.round((current / target) * 100)) : 0;

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
          <span className="font-medium text-foreground">{label}</span>
          <span className="tabular-nums text-muted-foreground">{percentage}%</span>
        </div>

        {/* Progress bar */}
        <div className="relative h-2 w-full overflow-hidden rounded-full bg-muted">
          <div
            className="h-full rounded-full transition-all duration-300"
            style={{
              width: `${percentage}%`,
              backgroundColor: `hsl(${entityColor})`,
            }}
            role="progressbar"
            aria-valuenow={current}
            aria-valuemin={0}
            aria-valuemax={target}
          />
          {/* Milestone markers */}
          {milestones?.map(milestone => {
            const pos = target > 0 ? Math.min(100, (milestone.at / target) * 100) : 0;
            return (
              <span
                key={milestone.at}
                className="absolute top-0 h-full w-0.5 bg-background/60"
                style={{ left: `${pos}%` }}
                title={milestone.label}
              />
            );
          })}
        </div>

        {/* Milestones legend */}
        {milestones && milestones.length > 0 && (
          <ul className="flex flex-wrap gap-x-2 gap-y-0.5 text-[10px] text-muted-foreground">
            {milestones.map(milestone => (
              <li key={milestone.at}>
                {milestone.at}: {milestone.label}
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
});

ProgressBlock.displayName = 'ProgressBlock';

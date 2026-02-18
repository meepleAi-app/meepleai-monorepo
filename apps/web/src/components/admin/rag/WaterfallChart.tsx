'use client';

import React from 'react';

export type WaterfallCallType = 'embedding' | 'search' | 'rerank' | 'llm' | 'cache';

export interface WaterfallCall {
  label: string;
  durationMs: number;
  type: WaterfallCallType;
  startOffsetMs: number;
}

export interface WaterfallChartProps {
  calls: WaterfallCall[];
}

const TYPE_COLORS: Record<WaterfallCallType, string> = {
  embedding: 'from-purple-500 to-purple-400',
  search: 'from-blue-500 to-blue-400',
  rerank: 'from-green-500 to-green-400',
  llm: 'from-amber-500 to-amber-400',
  cache: 'from-cyan-500 to-cyan-400',
};

export function WaterfallChart({ calls }: WaterfallChartProps): React.JSX.Element {
  if (!calls || calls.length === 0) {
    return (
      <div className="flex items-center justify-center py-8 text-sm text-muted-foreground">
        No timing data available
      </div>
    );
  }

  // Calculate total duration for percentage calculations
  const maxEndTime = Math.max(
    ...calls.map((call) => call.startOffsetMs + call.durationMs)
  );

  return (
    <div className="flex flex-col gap-1.5">
      {calls.map((call, index) => {
        const widthPercent = (call.durationMs / maxEndTime) * 100;
        const leftPercent = (call.startOffsetMs / maxEndTime) * 100;

        return (
          <div key={index} className="flex items-center gap-2.5">
            {/* Label */}
            <span className="w-[120px] flex-shrink-0 text-xs text-muted-foreground truncate">
              {call.label}
            </span>

            {/* Bar container */}
            <div className="relative flex-1 h-5 bg-border/40 rounded overflow-hidden">
              {/* Bar */}
              <div
                className={`absolute top-0 h-full bg-gradient-to-r ${TYPE_COLORS[call.type]} rounded flex items-center px-1.5 transition-all duration-500 ease-out`}
                style={{
                  width: `${widthPercent}%`,
                  left: `${leftPercent}%`,
                }}
              >
                <span className="text-[10px] font-mono font-semibold text-white">
                  {call.durationMs}ms
                </span>
              </div>
            </div>

            {/* Duration text */}
            <span className="w-[50px] flex-shrink-0 text-right text-[11px] font-mono text-muted-foreground/60">
              {call.durationMs}ms
            </span>
          </div>
        );
      })}
    </div>
  );
}

'use client';

/**
 * DebugCostBadge - Compact token/cost display
 *
 * Updates from DebugCostUpdate events to show running token/cost info.
 */

interface DebugCostBadgeProps {
  totalTokens: number;
  costUsd?: number | null;
  modelId?: string | null;
}

export function DebugCostBadge({ totalTokens, costUsd, modelId }: DebugCostBadgeProps) {
  return (
    <div className="inline-flex items-center gap-1.5 rounded-full bg-muted/50 px-2.5 py-0.5 text-xs text-muted-foreground">
      <span className="tabular-nums font-medium text-foreground">
        {totalTokens.toLocaleString()}
      </span>
      <span>tokens</span>
      {costUsd !== null && costUsd !== undefined && costUsd > 0 && (
        <>
          <span className="text-border">|</span>
          <span className="tabular-nums">${costUsd.toFixed(4)}</span>
        </>
      )}
      {modelId && (
        <>
          <span className="text-border">|</span>
          <span className="truncate max-w-[100px]">{modelId}</span>
        </>
      )}
    </div>
  );
}

'use client';
import { type JSX } from 'react';

export interface DrawerStreamingLabels {
  statusLabel: string;
  stopLabel: string;
}

export function DrawerStreaming({
  partialText,
  totalTokens,
  elapsedMs,
  onStop,
  labels,
}: {
  partialText: string;
  totalTokens: number;
  elapsedMs: number;
  onStop: () => void;
  labels: DrawerStreamingLabels;
}): JSX.Element {
  return (
    <div data-testid="drawer-state-streaming" className="flex-1 flex flex-col">
      <div className="flex-1 p-4 overflow-y-auto">
        <div className="bg-muted border border-border rounded-lg p-3 text-sm text-foreground leading-relaxed">
          {partialText}
          <span className="inline-flex gap-1 ml-2 align-middle">
            <span className="w-1.5 h-1.5 rounded-full bg-entity-kb animate-pulse" />
            <span className="w-1.5 h-1.5 rounded-full bg-entity-kb/50 animate-pulse" />
            <span className="w-1.5 h-1.5 rounded-full bg-entity-kb/30 animate-pulse" />
          </span>
        </div>
        <div className="mt-2 font-mono text-[10px] text-muted-foreground flex gap-2">
          <span className="text-entity-kb font-bold">● {labels.statusLabel}</span>
          <span>·</span>
          <span>{totalTokens} tokens</span>
          <span>·</span>
          <span>{(elapsedMs / 1000).toFixed(1)}s</span>
        </div>
      </div>
      <div className="p-3 border-t border-border bg-card">
        <button
          type="button"
          onClick={onStop}
          className="w-full py-2 rounded-md bg-muted border border-border text-sm text-foreground hover:bg-border"
        >
          ◼ {labels.stopLabel}
        </button>
      </div>
    </div>
  );
}

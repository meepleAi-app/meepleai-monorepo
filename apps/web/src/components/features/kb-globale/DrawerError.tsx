'use client';
import { type JSX } from 'react';

import type { KbAskError } from '../../../hooks/useKbAskStream';

export interface DrawerErrorLabelsForKind {
  title: string;
  body: string;
  action: string;
  alt?: string;
}
export interface DrawerErrorLabels {
  connection: DrawerErrorLabelsForKind;
  timeout: DrawerErrorLabelsForKind;
  partial: DrawerErrorLabelsForKind;
  server: DrawerErrorLabelsForKind;
}

export function DrawerError({
  error,
  partialText,
  onRetry,
  onCancel,
  labels,
}: {
  error: KbAskError;
  partialText: string;
  onRetry: () => void;
  onCancel?: () => void;
  labels: DrawerErrorLabels;
}): JSX.Element {
  const cfg = labels[error.kind];
  const icons = { connection: '📡', timeout: '⏱️', partial: '⚠', server: '⚙' } as const;
  return (
    <div
      data-testid={`drawer-state-error-${error.kind}`}
      className="flex-1 p-4 flex flex-col gap-3"
    >
      {error.kind === 'partial' && partialText && (
        <div className="bg-muted border border-destructive/30 rounded-lg p-3 text-sm text-muted-foreground italic">
          {partialText}
          <span className="not-italic font-bold text-destructive"> [stream interrotto]</span>
        </div>
      )}
      <div className="rounded-md bg-destructive/10 border border-destructive/30 border-l-4 border-l-destructive p-3 flex flex-col gap-2">
        <div className="flex items-center gap-2">
          <span className="text-2xl">{icons[error.kind]}</span>
          <div>
            <div className="font-display font-bold text-sm text-destructive">{cfg.title}</div>
            <div className="text-xs text-muted-foreground">{cfg.body}</div>
          </div>
        </div>
        {error.code && (
          <div className="font-mono text-[10px] text-muted-foreground">{error.code}</div>
        )}
        <div className="flex gap-2">
          <button
            type="button"
            onClick={onRetry}
            className="flex-1 px-3 py-1.5 rounded-md bg-destructive text-white text-xs font-medium"
          >
            ↻ {cfg.action}
          </button>
          {cfg.alt && onCancel && (
            <button
              type="button"
              onClick={onCancel}
              className="px-3 py-1.5 rounded-md bg-muted text-xs text-foreground"
            >
              {cfg.alt}
            </button>
          )}
        </div>
      </div>
    </div>
  );
}

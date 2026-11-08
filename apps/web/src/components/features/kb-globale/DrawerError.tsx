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
  /** #4510: the answer stopped before its end. */
  interrupted: DrawerErrorLabelsForKind;
  /** #4388: two answers are already open for the user; the third was refused. */
  busy: DrawerErrorLabelsForKind;
  server: DrawerErrorLabelsForKind;
}

/** #4510: the words that arrived before the end, kept but marked as not a complete answer. */
const INCOMPLETE_MARK = 'Risposta interrotta: non è completa.';

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
  const icons = {
    connection: '📡',
    timeout: '⏱️',
    interrupted: '⚠',
    busy: '⏳',
    server: '⚙',
  } as const;
  // #4510: the words that arrived stay when the answer was interrupted. (The `partial` kind is gone:
  // it was tied to `LLM_STREAMING_FAILED`, a code the backend no longer sends.)
  const keepsPartialText = error.kind === 'interrupted';
  return (
    <div
      data-testid={`drawer-state-error-${error.kind}`}
      className="flex-1 p-4 flex flex-col gap-3"
    >
      {/* #4510: small text on bg-muted is `text-foreground` — `text-muted-foreground` there is
          4.43:1 in the dark theme, below AA. */}
      {keepsPartialText && partialText && (
        <div
          data-testid="drawer-error-partial-text"
          className="bg-muted border border-destructive/30 rounded-lg p-3 text-sm text-foreground italic"
        >
          {partialText}
          <span className="block mt-2 not-italic text-xs font-bold text-foreground">
            {INCOMPLETE_MARK}
          </span>
        </div>
      )}
      {/* #4510: the reason is announced (role="alert"), and every text in the box is
          `text-foreground`: `text-destructive` on `bg-destructive/10` is 3.30:1 (light) and 2.68:1
          (dark), and white on `bg-destructive` 3.76:1 (light). */}
      <div
        role="alert"
        className="rounded-md bg-destructive/10 border border-destructive/30 border-l-4 border-l-destructive p-3 flex flex-col gap-2"
      >
        <div className="flex items-center gap-2">
          <span className="text-2xl" aria-hidden="true">
            {icons[error.kind]}
          </span>
          <div>
            <div className="font-display font-bold text-sm text-foreground">{cfg.title}</div>
            <div className="text-xs text-foreground">{cfg.body}</div>
          </div>
        </div>
        {error.code && <div className="font-mono text-[10px] text-foreground">{error.code}</div>}
        <div className="flex gap-2">
          <button
            type="button"
            onClick={onRetry}
            className="flex-1 px-3 py-1.5 rounded-md bg-primary text-primary-foreground text-xs font-medium"
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

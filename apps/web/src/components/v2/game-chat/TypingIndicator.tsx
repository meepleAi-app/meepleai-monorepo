/**
 * TypingIndicator — 3 dot animati + meta latency opzionale.
 *
 * Renderizzato durante stato 'submitting' della FSM (Q6: spinner atomico,
 * non streaming token-by-token). Budget default 10000ms (Q6 brainstorm).
 * Spec: §3.2 §4.1
 */
import type { ReactElement } from 'react';
import clsx from 'clsx';

export interface TypingIndicatorProps {
  readonly elapsedMs?: number;
  readonly budgetMs?: number;
  readonly hint?: string;
  readonly className?: string;
}

const DEFAULT_BUDGET_MS = 10_000;

function fmtSec(ms: number): string {
  return `${(ms / 1000).toFixed(1)}s`;
}

export function TypingIndicator({
  elapsedMs,
  budgetMs = DEFAULT_BUDGET_MS,
  hint,
  className,
}: TypingIndicatorProps): ReactElement {
  const showMeta = elapsedMs !== undefined || hint;
  return (
    <div data-slot="typing-indicator" className={clsx('flex flex-col gap-1', className)}>
      <div className="inline-flex gap-1 px-3 py-2">
        {[0, 1, 2].map(i => (
          <span
            key={i}
            data-testid="typing-dot"
            className="h-2 w-2 rounded-full bg-[hsl(var(--c-agent))] motion-safe:animate-pulse"
            style={{ animationDelay: `${i * 150}ms` }}
            aria-hidden="true"
          />
        ))}
      </div>
      {showMeta && (
        <div className="font-mono text-xs text-muted-foreground">
          ⏱️
          {hint && ` ${hint} ·`}
          {elapsedMs !== undefined && ` ${fmtSec(elapsedMs)} / ${Math.round(budgetMs / 1000)}s`}
        </div>
      )}
    </div>
  );
}

/**
 * SuggestedPrompts — chip categorizzati A/B/C/E/F (Q4 brainstorm categorie domanda).
 * Pure component. Categoria mostrata come tag mono.
 * Spec: §3.2
 */
import type { ReactElement } from 'react';
import clsx from 'clsx';

export type PromptCategory = 'A' | 'B' | 'C' | 'E' | 'F';

export interface SuggestedPrompt {
  readonly category: PromptCategory;
  readonly text: string;
  readonly onClick: () => void;
}

export interface SuggestedPromptsProps {
  readonly prompts: readonly SuggestedPrompt[];
  readonly groupLabel?: string;
  readonly className?: string;
}

export function SuggestedPrompts({
  prompts,
  groupLabel,
  className,
}: SuggestedPromptsProps): ReactElement | null {
  if (prompts.length === 0) return null;
  return (
    <div
      data-slot="suggested-prompts"
      className={clsx('flex flex-wrap gap-2 px-4 pb-3', className)}
    >
      {groupLabel && (
        <span className="w-full font-mono text-[10px] uppercase tracking-wider text-muted-foreground">
          {groupLabel}
        </span>
      )}
      {prompts.map((p, i) => (
        <button
          key={i}
          type="button"
          onClick={p.onClick}
          data-category={p.category}
          className={clsx(
            'inline-flex items-center gap-1 rounded-full px-3 py-2',
            'bg-[hsl(var(--c-chat)/0.08)] text-[hsl(var(--c-chat))]',
            'border border-[hsl(var(--c-chat)/0.2)]',
            'font-semibold text-xs',
            'hover:bg-[hsl(var(--c-chat)/0.15)]',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[hsl(var(--c-chat))]'
          )}
        >
          <span className="font-mono text-[9px] opacity-70">{p.category}</span>
          <span>{p.text}</span>
        </button>
      ))}
    </div>
  );
}

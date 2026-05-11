/**
 * OutOfContextActions — 3 action pill verticali per stato "decline + propose switch".
 * Pure component. Renderizzato solo quando response.outOfContext === true.
 * Spec: §3.2
 */
import type { ReactElement } from 'react';

import clsx from 'clsx';

export type OutOfContextActionKind = 'switch-game' | 'find-agent' | 'stay';

export interface OutOfContextAction {
  readonly kind: OutOfContextActionKind;
  readonly label: string;
  readonly onClick: () => void;
}

export interface OutOfContextActionsProps {
  readonly actions: readonly OutOfContextAction[];
  readonly className?: string;
}

const KIND_ICON: Record<OutOfContextActionKind, string> = {
  'switch-game': '🎲',
  'find-agent': '🤖',
  stay: '↩️',
};

export function OutOfContextActions({
  actions,
  className,
}: OutOfContextActionsProps): ReactElement | null {
  if (actions.length === 0) return null;
  return (
    <div
      data-slot="out-of-context-actions"
      className={clsx('mt-3 flex flex-col gap-2', className)}
    >
      {actions.map((a, i) => (
        <button
          key={i}
          type="button"
          onClick={a.onClick}
          data-action-kind={a.kind}
          className={clsx(
            'rounded-full border bg-card text-left',
            'border-border px-3 py-2 text-sm font-semibold',
            'flex items-center gap-2 transition-colors',
            'hover:bg-muted hover:border-[hsl(var(--c-chat)/0.4)]',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[hsl(var(--c-chat))]'
          )}
        >
          <span aria-hidden="true">{KIND_ICON[a.kind]}</span>
          <span>{a.label}</span>
        </button>
      ))}
    </div>
  );
}

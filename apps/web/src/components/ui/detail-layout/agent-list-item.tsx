/**
 * AgentListItem — published agent row for /shared-games/[id] V2.
 *
 * Wave A.4 (Issue #603). Mirrors mockup `sp3-shared-game-detail.jsx`
 * AgentPublicListItem (lines 499-574).
 *
 * Visual contract:
 *  - 44px icon agent-tinted (orange) bg + 🤖 emoji
 *  - Title (display 600) inline
 *  - Mono meta line: invocations count + last-updated date
 *  - Description (line-clamp 2)
 *  - Footer right: filled "Prova →" button agent-color with shadow-md
 *
 * `InvocationCount` is the only popularity metric backed by AgentDefinition today
 * (sourced from runtime telemetry). Mockup's expertise tag pills + rating are
 * deferred — domain entity has no expertise tags column today.
 */

import type { JSX } from 'react';

import clsx from 'clsx';

import { entityHsl } from '@/components/ui/data-display/meeple-card/tokens';

export interface AgentListItemLabels {
  /** "Last updated" prefix for date. */
  readonly updatedPrefix: string;
  /** Aria/visible suffix for invocation count, e.g. (n) => `${n} convs`. */
  readonly invocationsLabel: (count: number) => string;
  /** Filled CTA label, e.g. "Prova →". */
  readonly tryLabel: string;
  /** Aria label fragment for try button. */
  readonly tryAriaLabel: (name: string) => string;
}

export interface AgentListItemProps {
  readonly id: string;
  readonly name: string;
  readonly invocationCount: number;
  readonly lastUpdatedAt: string;
  readonly tryHref?: string;
  readonly description?: string;
  readonly labels: AgentListItemLabels;
  readonly className?: string;
}

export function AgentListItem({
  id,
  name,
  invocationCount,
  lastUpdatedAt,
  tryHref,
  description,
  labels,
  className,
}: AgentListItemProps): JSX.Element {
  const tryable = Boolean(tryHref);
  return (
    <article
      data-slot="shared-game-detail-agent-item"
      data-agent-id={id}
      className={clsx(
        'flex items-start gap-3 rounded-lg border border-border bg-card p-3.5',
        'transition-[transform,box-shadow,border-color] duration-150',
        'hover:-translate-y-0.5 hover:shadow-md',
        className
      )}
      style={{
        borderColor: entityHsl('agent', 0.18),
      }}
    >
      {/* Icon */}
      <span
        aria-hidden="true"
        className="flex h-11 w-11 shrink-0 items-center justify-center rounded-lg text-[22px]"
        style={{
          backgroundColor: entityHsl('agent', 0.12),
          color: entityHsl('agent'),
        }}
      >
        🤖
      </span>

      {/* Body */}
      <div className="flex min-w-0 flex-1 flex-col gap-1">
        <h3 className="m-0 font-display text-[15px] font-semibold leading-tight text-foreground">
          {name}
        </h3>

        <p className="m-0 font-mono text-[10px] uppercase tracking-[0.06em] text-[hsl(var(--text-muted))]">
          <span data-dynamic="number">{labels.invocationsLabel(invocationCount)}</span> ·{' '}
          {labels.updatedPrefix}{' '}
          <time dateTime={lastUpdatedAt} data-dynamic="datetime">
            {new Date(lastUpdatedAt).toLocaleDateString()}
          </time>
        </p>

        {description ? (
          <p className="m-0 line-clamp-2 text-sm text-foreground/80">{description}</p>
        ) : null}
      </div>

      {/* CTA */}
      <div className="flex shrink-0 items-center self-center">
        {tryable ? (
          <a
            href={tryHref}
            aria-label={labels.tryAriaLabel(name)}
            className={clsx(
              'inline-flex items-center gap-1 rounded-full px-3.5 py-1.5 font-mono text-[11px] font-semibold uppercase tracking-[0.06em] text-white no-underline shadow-md',
              'transition-[transform,box-shadow] duration-150',
              'hover:scale-[1.03] hover:shadow-lg',
              'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-offset-2'
            )}
            style={{
              backgroundColor: entityHsl('agent'),
            }}
          >
            {labels.tryLabel}
          </a>
        ) : (
          <span
            aria-disabled="true"
            className="inline-flex cursor-not-allowed items-center gap-1 rounded-full px-3.5 py-1.5 font-mono text-[11px] font-semibold uppercase tracking-[0.06em] text-white opacity-50"
            style={{ backgroundColor: entityHsl('agent') }}
          >
            {labels.tryLabel}
          </span>
        )}
      </div>
    </article>
  );
}

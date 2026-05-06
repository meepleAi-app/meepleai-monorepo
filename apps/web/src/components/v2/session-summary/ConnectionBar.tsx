/**
 * ConnectionBar — Wave D.3 v2 component (Issue #756).
 *
 * Horizontal pip strip rendering the session's content density at a glance —
 * one pip per major content category (gioco, giocatori, agente, chat, foto,
 * serata). Empty categories render as dashed-border ghosts at reduced opacity.
 *
 * Mockup mapping:
 *   - admin-mockups/design_files/sp4-session-summary-parts.jsx (ConnectionBar)
 *
 * Contract reference: docs/frontend/contracts/sessions-id-summary-hooks.md §5.3.
 *
 * MeepleCard divergence (Gate C): inline pip strip — neither a card nor a
 * MeepleCard list variant. DIVERGE.
 *
 * A11y:
 *   - `role="status"` (sealed completion info, no live updates).
 *   - Each pip is a button so clicks scroll-anchor to the matching section.
 *   - Empty pip ghosts get a `title` attribute explaining the empty state.
 *
 * Pure component: orchestrator resolves all label strings + counts via labels.
 */

import type { ReactElement } from 'react';

import clsx from 'clsx';

export type ConnectionBarEntity =
  | 'game'
  | 'player'
  | 'agent'
  | 'chat'
  | 'kb'
  | 'event'
  | 'session'
  | 'toolkit';

const ENTITY_FULL: Record<ConnectionBarEntity, string> = {
  game: 'hsl(25, 95%, 39%)',
  player: 'hsl(262, 83%, 58%)',
  agent: 'hsl(38, 92%, 33%)',
  chat: 'hsl(220, 80%, 55%)',
  kb: 'hsl(210, 40%, 48%)',
  event: 'hsl(350, 89%, 48%)',
  session: 'hsl(240, 60%, 45%)',
  toolkit: 'hsl(142, 70%, 31%)',
};

const ENTITY_BG_ALPHA: Record<ConnectionBarEntity, string> = {
  game: 'hsla(25, 95%, 39%, 0.1)',
  player: 'hsla(262, 83%, 58%, 0.1)',
  agent: 'hsla(38, 92%, 33%, 0.1)',
  chat: 'hsla(220, 80%, 55%, 0.1)',
  kb: 'hsla(210, 40%, 48%, 0.1)',
  event: 'hsla(350, 89%, 48%, 0.1)',
  session: 'hsla(240, 60%, 45%, 0.1)',
  toolkit: 'hsla(142, 70%, 31%, 0.1)',
};

const ENTITY_RING_ALPHA: Record<ConnectionBarEntity, string> = {
  game: 'hsla(25, 95%, 39%, 0.3)',
  player: 'hsla(262, 83%, 58%, 0.3)',
  agent: 'hsla(38, 92%, 33%, 0.3)',
  chat: 'hsla(220, 80%, 55%, 0.3)',
  kb: 'hsla(210, 40%, 48%, 0.3)',
  event: 'hsla(350, 89%, 48%, 0.3)',
  session: 'hsla(240, 60%, 45%, 0.3)',
  toolkit: 'hsla(142, 70%, 31%, 0.3)',
};

export interface ConnectionBarPip {
  readonly entity: ConnectionBarEntity;
  readonly emoji: string;
  readonly label: string;
  readonly count: number;
  /** Optional anchor href (e.g. "#section-game") for scroll navigation. */
  readonly href?: string;
}

export interface ConnectionBarLabels {
  /** A11y label for the strip (e.g. "Andamento partita"). */
  readonly title: string;
  /** Title attribute for empty pips ("Nessun evento registrato"). */
  readonly emptyEvent: string;
}

export interface ConnectionBarProps {
  /** Pips to render — orchestrator computes from session/diary counts. */
  readonly pips: readonly ConnectionBarPip[];
  readonly labels: ConnectionBarLabels;
  readonly className?: string;
}

export function ConnectionBar({ pips, labels, className }: ConnectionBarProps): ReactElement {
  return (
    <div
      data-slot="connection-bar"
      role="status"
      aria-label={labels.title}
      className={clsx(
        'flex gap-1.5 overflow-x-auto px-4 py-2.5 sm:px-8',
        'border-b border-border bg-card/80 backdrop-blur-sm',
        '[scrollbar-width:none] [&::-webkit-scrollbar]:hidden',
        className
      )}
    >
      {pips.map(p => {
        const isEmpty = p.count === 0;
        const baseStyle = isEmpty
          ? {
              background: 'transparent',
              border: `1px dashed ${ENTITY_RING_ALPHA[p.entity]}`,
              opacity: 0.6,
            }
          : {
              background: ENTITY_BG_ALPHA[p.entity],
              border: `1px solid ${ENTITY_RING_ALPHA[p.entity]}`,
            };
        const Tag = p.href ? 'a' : 'span';
        return (
          <Tag
            key={p.entity}
            {...(p.href ? { href: p.href } : {})}
            data-slot="connection-bar-pip"
            data-entity={p.entity}
            data-empty={isEmpty || undefined}
            title={isEmpty ? labels.emptyEvent : undefined}
            className={clsx(
              'inline-flex shrink-0 items-center gap-1 whitespace-nowrap rounded-full px-2.5 py-1',
              'font-mono text-[10px] font-extrabold uppercase tracking-wide',
              'no-underline transition-colors',
              p.href && 'cursor-pointer'
            )}
            style={{
              ...baseStyle,
              color: ENTITY_FULL[p.entity],
            }}
          >
            <span aria-hidden="true">{p.emoji}</span>
            {!isEmpty && (
              <span
                className="rounded-full px-1.5 py-0 font-extrabold"
                style={{ background: ENTITY_RING_ALPHA[p.entity] }}
              >
                {p.count}
              </span>
            )}
            <span>{p.label}</span>
          </Tag>
        );
      })}
    </div>
  );
}

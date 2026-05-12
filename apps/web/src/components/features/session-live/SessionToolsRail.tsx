/* eslint-disable local/no-hardcoded-color-utility -- text-white / button color on style-prop colored bg or entity-colored CTA; mockup .e-bg pattern. DS-12 will introduce primitives encoding bg via className. */
'use client';

/**
 * SessionToolsRail — Wave D.2 Interactions sub-PR (Issue #750).
 *
 * Tool grid for Player+Host role variants.
 * Spectator: rail hidden entirely (return null).
 * Player+Host: grid of clickable tool cards (dice, timer, card icons).
 *
 * Gate C: DIVERGES from MeepleCard — interactive tool grid, not a card
 *   pattern. Tool cards are action triggers, not entity displays.
 *
 * Layout: compact → 2-col; default → 3-col (desktop).
 *
 * data-slot="session-tools-rail" — required by unit tests.
 * data-viewer-role={viewerRole} — role variant assertion in unit tests.
 */

import type { ReactElement } from 'react';

import { Dices, Timer, CreditCard } from 'lucide-react';

import type { ParticipantRole } from '@/lib/session-live/participant-role';

// ─── Labels ───────────────────────────────────────────────────────────────────

export interface SessionToolsRailLabels {
  /** Section heading: "Strumenti" */
  readonly title: string;
  readonly toolDiceLabel: string;
  readonly toolTimerLabel: string;
  readonly toolCardLabel: string;
  /** Template: "Esegui {toolName}" — resolved by orchestrator for aria-label */
  readonly executeAriaTemplate: string;
  readonly disabledSpectatorTooltip: string;
}

// ─── Props ────────────────────────────────────────────────────────────────────

export interface SessionToolsRailProps {
  readonly tools: ReadonlyArray<{ id: string; name: string; icon: 'dice' | 'timer' | 'card' }>;
  readonly viewerRole: ParticipantRole;
  readonly onToolExecute: (toolId: string) => void;
  readonly compact?: boolean;
  readonly labels: SessionToolsRailLabels;
  readonly className?: string;
}

// ─── Icon map ─────────────────────────────────────────────────────────────────

const ICON_MAP = {
  dice: Dices,
  timer: Timer,
  card: CreditCard,
} as const;

// ─── Component ────────────────────────────────────────────────────────────────

export function SessionToolsRail({
  tools,
  viewerRole,
  onToolExecute,
  compact = false,
  labels,
  className,
}: SessionToolsRailProps): ReactElement | null {
  // Spectator: rail hidden entirely (§4.4 Cell R1)
  if (viewerRole === 'Spectator') {
    return null;
  }

  const gridCols = compact ? 'grid-cols-2' : 'grid-cols-3';

  return (
    <section
      data-slot="session-tools-rail"
      data-viewer-role={viewerRole}
      aria-label={labels.title}
      className={className}
    >
      <h3 className="mb-3 text-xs font-semibold uppercase tracking-wider text-muted-foreground">
        {labels.title}
      </h3>

      <div className={`grid gap-2 ${gridCols}`}>
        {tools.map(tool => {
          const IconComponent = ICON_MAP[tool.icon] ?? Dices;
          const ariaLabel = labels.executeAriaTemplate.replace('{toolName}', tool.name);

          return (
            <button
              key={tool.id}
              type="button"
              aria-label={ariaLabel}
              onClick={() => onToolExecute(tool.id)}
              className="flex flex-col items-center gap-2 rounded-lg border border-border/60
                bg-card p-3 text-slate-200 transition-colors
                hover:border-border hover:bg-card
                focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-slate-400
                active:scale-95"
            >
              <IconComponent className="h-5 w-5 text-slate-300" aria-hidden="true" />
              <span className="text-xs font-medium text-slate-300">{tool.name}</span>
            </button>
          );
        })}
      </div>
    </section>
  );
}

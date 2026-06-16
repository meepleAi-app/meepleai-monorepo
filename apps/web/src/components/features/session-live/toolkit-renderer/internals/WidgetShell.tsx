'use client';

/**
 * WidgetShell — Issue #2376 G5c internal helper.
 *
 * Accordion-style shell with icon + title + type pill + body.
 * Body unmounts when collapsed=true (matches ChatAgentPanel §5 contract from #2375 G3).
 *
 * @see docs/superpowers/specs/2026-06-16-issue-2376-g5c-toolkit-renderer-design.md §3
 */

import type { ReactElement, ReactNode } from 'react';

import { ChevronDown } from 'lucide-react';

export interface WidgetShellProps {
  readonly icon: string;
  readonly title: string;
  readonly typeLabel: string;
  readonly children: ReactNode;
  readonly collapsed: boolean;
  readonly onHeaderClick: () => void;
  readonly headerAriaLabel: string;
  readonly entityAccent?: 'tool' | 'player' | 'session' | 'kb' | 'chat';
}

export function WidgetShell({
  icon,
  title,
  typeLabel,
  children,
  collapsed,
  onHeaderClick,
  headerAriaLabel,
  entityAccent = 'tool',
}: WidgetShellProps): ReactElement {
  const accentMap: Record<string, string> = {
    tool: 'border-entity-tool/30 bg-entity-tool/5 text-entity-tool',
    player: 'border-entity-player/30 bg-entity-player/5 text-entity-player',
    session: 'border-entity-session/30 bg-entity-session/5 text-entity-session',
    kb: 'border-entity-kb/30 bg-entity-kb/5 text-entity-kb',
    chat: 'border-entity-chat/30 bg-entity-chat/5 text-entity-chat',
  };
  const accent = accentMap[entityAccent] ?? accentMap.tool;

  return (
    <section
      data-slot="widget-shell"
      data-collapsed={collapsed ? 'true' : undefined}
      className={`flex flex-col overflow-hidden rounded-lg border bg-card ${
        collapsed ? 'border-border' : accent.split(' ')[0]
      }`}
    >
      <button
        type="button"
        onClick={onHeaderClick}
        aria-expanded={!collapsed}
        aria-label={headerAriaLabel}
        className={`flex w-full items-center gap-2 border-b px-3 py-2 text-left transition-colors hover:bg-muted/50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring ${
          collapsed ? 'border-transparent' : 'border-border/60'
        }`}
      >
        <span
          aria-hidden="true"
          className="flex h-6 w-6 shrink-0 items-center justify-center rounded text-sm"
        >
          {icon}
        </span>
        <span className="flex-1 truncate text-sm font-bold text-foreground">{title}</span>
        <span
          className={`shrink-0 rounded-full border px-2 py-0.5 text-xs font-semibold uppercase tracking-wider ${accent}`}
        >
          {typeLabel}
        </span>
        <ChevronDown
          aria-hidden="true"
          className={`h-4 w-4 shrink-0 transition-transform ${
            collapsed ? '-rotate-90' : ''
          } ${accent.split(' ').pop()}`}
        />
      </button>
      {!collapsed && <div className="p-3">{children}</div>}
    </section>
  );
}

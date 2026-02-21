'use client';

/**
 * SessionToolLayout — full-screen layout: sticky header + ToolRail + content area
 * Epic #4968: Game Session Toolkit v2
 */

import React from 'react';

import type { ToolItem } from './ToolRail';
import { ToolRail } from './ToolRail';

// ── Component ─────────────────────────────────────────────────────────────────

interface SessionToolLayoutProps {
  /** Header slot — renders SessionHeader + TurnIndicatorBar */
  header: React.ReactNode;
  /** Currently active tool ID */
  activeTool: string;
  /** Called when the user clicks a rail item */
  onToolSelect: (id: string) => void;
  /** Custom tools rendered below the divider in the ToolRail */
  customTools?: ToolItem[];
  /** Active tool panel content */
  children: React.ReactNode;
}

export function SessionToolLayout({
  header,
  activeTool,
  onToolSelect,
  customTools,
  children,
}: SessionToolLayoutProps) {
  return (
    <div className="flex flex-col h-screen bg-slate-50 dark:bg-slate-950">
      {/* Sticky header */}
      <div className="shrink-0">{header}</div>

      {/* Body: ToolRail + scrollable content */}
      <div className="flex flex-1 overflow-hidden">
        <ToolRail
          activeTool={activeTool}
          onToolSelect={onToolSelect}
          customTools={customTools}
        />

        <main className="flex-1 overflow-auto p-4 md:p-6">
          {children}
        </main>
      </div>
    </div>
  );
}

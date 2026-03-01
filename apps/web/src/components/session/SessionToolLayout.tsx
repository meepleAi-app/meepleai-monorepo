'use client';

import React, { useState } from 'react';

import { cn } from '@/lib/utils';

import { BASE_TOOLS, ToolRail, type ToolItem } from './ToolRail';

export interface SessionToolLayoutProps {
  /** The session header component (SessionHeader + sub-line). */
  header: React.ReactNode;
  /** Currently active tool ID. */
  activeTool: string;
  /** Called when user selects a different tool. */
  onToolSelect: (toolId: string) => void;
  /** Custom tools from GameToolkit (override flags applied in parent). */
  customTools?: ToolItem[];
  /** The content area rendered for the active tool. */
  children: React.ReactNode;
  className?: string;
}

/**
 * SessionToolLayout — top-level layout wrapper for the active session.
 *
 * Desktop (md+): [Header][Side Rail | Tool Area]
 * Mobile (<md):  [Header][Tool Area][Bottom Nav]
 *
 * Issue #4973.
 */
export function SessionToolLayout({
  header,
  activeTool,
  onToolSelect,
  customTools = [],
  children,
  className,
}: SessionToolLayoutProps) {
  const [isRailCollapsed, setIsRailCollapsed] = useState(false);

  const tools: ToolItem[] = [...BASE_TOOLS, ...customTools];

  return (
    <div
      className={cn(
        'flex flex-col min-h-screen bg-stone-100 dark:bg-stone-950 session-toolkit-layout',
        className,
      )}
    >
      {/* Header (full width) */}
      <div className="flex-shrink-0 z-20 sticky top-0">{header}</div>

      {/* Body: side rail + tool area (desktop) / tool area + bottom nav (mobile) */}
      <div className="flex flex-1 min-h-0 overflow-hidden">
        {/* Tool Rail (desktop side rail + mobile bottom nav) */}
        <ToolRail
          tools={tools}
          activeTool={activeTool}
          onToolChange={onToolSelect}
          isCollapsed={isRailCollapsed}
          onToggleCollapse={() => setIsRailCollapsed(c => !c)}
        />

        {/* Active tool area */}
        <main
          className={cn(
            'flex-1 overflow-y-auto',
            // Bottom padding on mobile to avoid overlap with bottom nav
            'pb-20 md:pb-6',
            'px-4 py-4 md:px-6',
          )}
          aria-live="polite"
          aria-atomic="false"
          id="session-tool-area"
        >
          {/* Animated wrapper — fade on tool change */}
          <div className="session-tool-area-content animate-tool-fade">{children}</div>
        </main>
      </div>
    </div>
  );
}

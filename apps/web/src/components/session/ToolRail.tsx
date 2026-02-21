'use client';

import React, { useState } from 'react';

import { ChevronLeft, ChevronRight, Dices, LayoutGrid, PenLine, RotateCcw } from 'lucide-react';

import { cn } from '@/lib/utils';

export interface ToolItem {
  id: string;
  label: string;
  shortLabel: string;
  icon: React.ReactNode;
  type: 'base' | 'custom';
}

/**
 * Base tools always present in every session (Issue #4968, #4973).
 * Connected to tool components in Issue #4974.
 */
export const BASE_TOOLS: ToolItem[] = [
  {
    id: 'scoreboard',
    type: 'base',
    label: 'Punteggi',
    shortLabel: 'Score',
    icon: <LayoutGrid className="w-5 h-5" aria-hidden="true" />,
  },
  {
    id: 'turn-order',
    type: 'base',
    label: 'Ordine turno',
    shortLabel: 'Turno',
    icon: <RotateCcw className="w-5 h-5" aria-hidden="true" />,
  },
  {
    id: 'dice',
    type: 'base',
    label: 'Dadi',
    shortLabel: 'Dadi',
    icon: <Dices className="w-5 h-5" aria-hidden="true" />,
  },
  {
    id: 'whiteboard',
    type: 'base',
    label: 'Lavagna',
    shortLabel: 'Lav.',
    icon: <PenLine className="w-5 h-5" aria-hidden="true" />,
  },
];

export interface ToolRailProps {
  tools: ToolItem[];
  activeTool: string;
  onToolChange: (toolId: string) => void;
  isCollapsed?: boolean;
  onToggleCollapse?: () => void;
  className?: string;
}

/** Single tool button used in both desktop and mobile variants. */
function ToolButton({
  tool,
  isActive,
  showLabel,
  onClick,
}: {
  tool: ToolItem;
  isActive: boolean;
  showLabel: boolean;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      role="tab"
      aria-selected={isActive}
      aria-label={tool.label}
      onClick={onClick}
      className={cn(
        'group relative flex items-center gap-3 w-full px-3 py-2.5 rounded-lg',
        'transition-all duration-150',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-amber-500 focus-visible:ring-offset-1',
        isActive
          ? 'bg-amber-100 dark:bg-amber-900/40 border border-amber-400 dark:border-amber-600 text-amber-800 dark:text-amber-200 shadow-sm'
          : 'border border-transparent text-stone-600 dark:text-stone-400 hover:bg-stone-100 dark:hover:bg-stone-800',
      )}
    >
      <span
        className={cn(
          'flex-shrink-0',
          isActive ? 'text-amber-700 dark:text-amber-300' : 'text-stone-500 dark:text-stone-400',
        )}
      >
        {tool.icon}
      </span>
      {showLabel && (
        <span className="text-sm font-medium truncate leading-none">{tool.label}</span>
      )}
      {isActive && (
        <span
          aria-hidden="true"
          className="absolute right-2 top-1/2 -translate-y-1/2 w-1.5 h-1.5 rounded-full bg-amber-500 dark:bg-amber-400"
        />
      )}
    </button>
  );
}

/**
 * ToolRail — desktop side rail + mobile bottom navigation for the session.
 *
 * Desktop (md+): collapsible vertical list with icons + labels.
 * Mobile (<md):  sticky bottom bar with icons + short labels.
 *
 * WCAG 2.1 AA: role="tablist", aria-selected, aria-label, keyboard navigation.
 * Issue #4973.
 */
export function ToolRail({
  tools,
  activeTool,
  onToolChange,
  isCollapsed: isCollapsedProp,
  onToggleCollapse,
  className,
}: ToolRailProps) {
  const [internalCollapsed, setInternalCollapsed] = useState(false);

  const isCollapsed = isCollapsedProp !== undefined ? isCollapsedProp : internalCollapsed;
  const handleToggle = onToggleCollapse ?? (() => setInternalCollapsed(c => !c));

  const baseTools = tools.filter(t => t.type === 'base');
  const customTools = tools.filter(t => t.type === 'custom');

  // Keyboard navigation: cycle through all tools using activeTool as anchor
  const handleKeyDown = (e: React.KeyboardEvent<HTMLElement>) => {
    const currentIndex = tools.findIndex(t => t.id === activeTool);
    if (currentIndex === -1) return;

    if (e.key === 'ArrowDown' || e.key === 'ArrowRight') {
      e.preventDefault();
      onToolChange(tools[(currentIndex + 1) % tools.length].id);
    } else if (e.key === 'ArrowUp' || e.key === 'ArrowLeft') {
      e.preventDefault();
      onToolChange(tools[(currentIndex - 1 + tools.length) % tools.length].id);
    }
  };

  return (
    <>
      {/* ── Desktop: Side Rail ───────────────────────────────────────────── */}
      <aside
        className={cn(
          'hidden md:flex flex-col session-tool-rail',
          isCollapsed ? 'w-14' : 'w-48',
          'transition-[width] duration-200 ease-in-out',
          'bg-stone-50 dark:bg-stone-900 border-r border-stone-200 dark:border-stone-700',
          'h-full min-h-0 overflow-y-auto overflow-x-hidden',
          className,
        )}
        aria-label="Strumenti sessione"
      >
        {/* Collapse toggle */}
        <button
          type="button"
          onClick={handleToggle}
          aria-label={isCollapsed ? 'Espandi rail' : 'Comprimi rail'}
          className={cn(
            'flex items-center justify-center p-2 mx-1 mt-2 mb-3 rounded-md',
            'text-stone-500 dark:text-stone-400 hover:bg-stone-100 dark:hover:bg-stone-800',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-amber-500',
            'transition-colors duration-150',
            isCollapsed ? 'self-center' : 'self-end',
          )}
        >
          {isCollapsed ? (
            <ChevronRight className="w-4 h-4" aria-hidden="true" />
          ) : (
            <ChevronLeft className="w-4 h-4" aria-hidden="true" />
          )}
        </button>

        {/* Base tools tablist */}
        <div
          role="tablist"
          aria-label="Strumenti base"
          aria-orientation="vertical"
          className="flex flex-col gap-1 px-1"
          onKeyDown={handleKeyDown}
        >
          {baseTools.map(tool => (
            <ToolButton
              key={tool.id}
              tool={tool}
              isActive={activeTool === tool.id}
              showLabel={!isCollapsed}
              onClick={() => onToolChange(tool.id)}
            />
          ))}
        </div>

        {/* Separator + custom tools */}
        {customTools.length > 0 && (
          <>
            <div
              className="flex items-center px-2 my-3"
              title="Tool custom"
              aria-hidden="true"
            >
              <div className="flex-1 h-px bg-stone-200 dark:bg-stone-700" />
              {!isCollapsed && (
                <span className="px-2 text-xs text-stone-400 dark:text-stone-500 font-medium uppercase tracking-wider">
                  Custom
                </span>
              )}
              <div className="flex-1 h-px bg-stone-200 dark:bg-stone-700" />
            </div>

            <div
              role="tablist"
              aria-label="Strumenti custom"
              aria-orientation="vertical"
              className="flex flex-col gap-1 px-1"
              onKeyDown={handleKeyDown}
            >
              {customTools.map(tool => (
                <ToolButton
                  key={tool.id}
                  tool={tool}
                  isActive={activeTool === tool.id}
                  showLabel={!isCollapsed}
                  onClick={() => onToolChange(tool.id)}
                />
              ))}
            </div>
          </>
        )}
      </aside>

      {/* ── Mobile: Bottom Navigation ────────────────────────────────────── */}
      <nav
        className={cn(
          'md:hidden fixed bottom-0 left-0 right-0 z-30',
          'bg-stone-50 dark:bg-stone-900 border-t border-stone-200 dark:border-stone-700',
          'shadow-[0_-2px_8px_rgba(0,0,0,0.08)]',
        )}
        aria-label="Strumenti sessione"
      >
        <div
          role="tablist"
          aria-label="Strumenti sessione"
          aria-orientation="horizontal"
          className="flex items-center justify-around px-2 py-1.5 overflow-x-auto"
          onKeyDown={handleKeyDown}
        >
          {tools.slice(0, 5).map(tool => (
            <button
              key={tool.id}
              type="button"
              role="tab"
              aria-selected={activeTool === tool.id}
              aria-label={tool.label}
              onClick={() => onToolChange(tool.id)}
              className={cn(
                'flex flex-col items-center gap-0.5 px-3 py-1.5 rounded-lg min-w-[56px]',
                'transition-colors duration-150',
                'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-amber-500',
                activeTool === tool.id
                  ? 'text-amber-700 dark:text-amber-300'
                  : 'text-stone-500 dark:text-stone-400',
              )}
            >
              <span
                className={cn(
                  'relative flex items-center justify-center w-8 h-8 rounded-md transition-colors duration-150',
                  activeTool === tool.id
                    ? 'bg-amber-100 dark:bg-amber-900/40'
                    : 'bg-transparent',
                )}
              >
                {tool.icon}
              </span>
              <span className="text-[10px] font-medium leading-none truncate max-w-[52px]">
                {tool.shortLabel}
              </span>
            </button>
          ))}
        </div>
      </nav>
    </>
  );
}

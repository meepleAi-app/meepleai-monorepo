'use client';

import type { ReactNode } from 'react';

import { PanelLeftClose, PanelRightClose, PanelLeftOpen, PanelRightOpen } from 'lucide-react';

import { cn } from '@/lib/utils';

interface CollapsiblePanelProps {
  side: 'left' | 'right';
  isCollapsed: boolean;
  onToggle: () => void;
  children?: ReactNode;
  width?: string;
  className?: string;
}

export function CollapsiblePanel({
  side,
  isCollapsed,
  onToggle,
  children,
  width = '280px',
  className,
}: CollapsiblePanelProps) {
  const CollapseIcon = side === 'left' ? PanelLeftClose : PanelRightClose;
  const ExpandIcon = side === 'left' ? PanelLeftOpen : PanelRightOpen;

  return (
    <div
      data-testid="collapsible-panel"
      className={cn(
        'flex flex-col border-border bg-card transition-[width] duration-200',
        side === 'left' ? 'border-r' : 'border-l',
        isCollapsed && 'w-[44px]',
        className
      )}
      style={isCollapsed ? { width: '44px' } : { width }}
    >
      {isCollapsed ? (
        <button
          onClick={onToggle}
          aria-label="Espandi pannello"
          className="p-2.5 text-muted-foreground hover:text-foreground transition-colors"
        >
          <ExpandIcon className="h-4 w-4" />
        </button>
      ) : (
        <>
          <div className="flex items-center justify-end p-1 border-b border-border">
            <button
              onClick={onToggle}
              aria-label="Comprimi pannello"
              className="p-1.5 rounded-md text-muted-foreground hover:text-foreground hover:bg-muted transition-colors"
            >
              <CollapseIcon className="h-4 w-4" />
            </button>
          </div>
          <div className="flex-1 overflow-y-auto">{children}</div>
        </>
      )}
    </div>
  );
}

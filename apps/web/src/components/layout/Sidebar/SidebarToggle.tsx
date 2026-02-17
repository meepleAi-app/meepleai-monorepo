/**
 * SidebarToggle Component
 * Collapse/expand toggle button for the sidebar.
 */

'use client';

import { ChevronsLeft, ChevronsRight } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

export interface SidebarToggleProps {
  isCollapsed: boolean;
  onToggle: () => void;
}

export function SidebarToggle({ isCollapsed, onToggle }: SidebarToggleProps) {
  return (
    <Button
      variant="ghost"
      size="icon"
      onClick={onToggle}
      className={cn(
        'absolute -right-3 top-6 z-50',
        'h-6 w-6 rounded-full',
        'border border-sidebar-border bg-sidebar',
        'hover:bg-sidebar-accent',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sidebar-ring'
      )}
      aria-label={isCollapsed ? 'Expand sidebar' : 'Collapse sidebar'}
      data-testid="sidebar-toggle"
    >
      {isCollapsed ? (
        <ChevronsRight className="h-3.5 w-3.5" aria-hidden="true" />
      ) : (
        <ChevronsLeft className="h-3.5 w-3.5" aria-hidden="true" />
      )}
    </Button>
  );
}

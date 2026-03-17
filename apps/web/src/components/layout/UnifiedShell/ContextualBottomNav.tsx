'use client';

import { useBottomNavActions } from '@/hooks/use-bottom-nav-actions';
import { cn } from '@/lib/utils';

import { ContextualBottomNavItem } from './ContextualBottomNavItem';

export function ContextualBottomNav() {
  const actions = useBottomNavActions();

  return (
    <nav
      className={cn(
        'sticky bottom-0 z-30',
        'flex items-center justify-around px-2 py-1',
        'bg-background/95 backdrop-blur-xl',
        'border-t border-border/40',
        'pb-[env(safe-area-inset-bottom)]'
      )}
      role="toolbar"
      aria-label="Page actions"
      data-testid="contextual-bottom-nav"
    >
      {actions.map(action => (
        <ContextualBottomNavItem
          key={action.id}
          id={action.id}
          label={action.label}
          icon={action.icon}
          variant={action.variant}
          onClick={action.onClick}
        />
      ))}
    </nav>
  );
}

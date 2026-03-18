'use client';

import { Shield, Gamepad2 } from 'lucide-react';

import { cn } from '@/lib/utils';
import { useCardHand } from '@/stores/use-card-hand';

export function AdminToggle() {
  const { context, toggleContext } = useCardHand();
  const isAdmin = context === 'admin';

  return (
    <button
      type="button"
      onClick={toggleContext}
      className={cn(
        'flex items-center gap-1.5 px-2.5 py-1.5 rounded-lg text-xs font-medium',
        'transition-all duration-200 border',
        isAdmin
          ? 'bg-destructive/10 text-destructive border-destructive/30'
          : 'bg-muted/50 text-muted-foreground border-border/30 hover:bg-muted'
      )}
      role="switch"
      aria-checked={isAdmin}
      aria-label={isAdmin ? 'Switch to user mode' : 'Switch to admin mode'}
      data-testid="admin-toggle"
    >
      {isAdmin ? (
        <>
          <Shield className="w-3.5 h-3.5" />
          <span className="hidden sm:inline">Admin</span>
        </>
      ) : (
        <>
          <Gamepad2 className="w-3.5 h-3.5" />
          <span className="hidden sm:inline">User</span>
        </>
      )}
    </button>
  );
}

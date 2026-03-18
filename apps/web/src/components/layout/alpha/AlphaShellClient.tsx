'use client';

/**
 * AlphaShellClient — Client component providing the alpha layout structure.
 *
 * Mobile-first layout with:
 * - Top nav bar (placeholder — Task 1.2 will replace)
 * - Scrollable main content area
 * - Bottom tab bar (placeholder — Task 1.3 will replace)
 *
 * Uses h-dvh (dynamic viewport height) for mobile-safe full height.
 */

import { type ReactNode } from 'react';

interface AlphaShellClientProps {
  children: ReactNode;
  isAdmin: boolean;
}

export function AlphaShellClient({ children }: AlphaShellClientProps) {
  return (
    <div className="flex flex-col h-dvh bg-background">
      {/* AlphaTopNav placeholder — Task 1.2 will replace */}
      <div className="h-14 border-b border-border/40 flex items-center px-4">
        <span className="font-quicksand font-bold">MeepleAI</span>
      </div>

      <main className="flex-1 overflow-y-auto pb-16">{children}</main>

      {/* AlphaTabBar placeholder — Task 1.3 will replace */}
      <div className="fixed bottom-0 left-0 right-0 h-14 border-t border-border/40 bg-card/90 backdrop-blur-md flex items-center justify-around z-40">
        <span className="text-xs text-muted-foreground">Home</span>
        <span className="text-xs text-muted-foreground">Libreria</span>
        <span className="text-xs text-muted-foreground">Play</span>
        <span className="text-xs text-muted-foreground">Chat</span>
      </div>
    </div>
  );
}

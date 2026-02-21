'use client';

/**
 * ShowcaseLayout — Three-panel shell
 *
 * ┌──────────────┬─────────────────────────┬─────────────────┐
 * │  Sidebar     │  Canvas (preview)        │  Controls       │
 * │  (280px)     │  (flex-1)                │  (320px)        │
 * └──────────────┴─────────────────────────┴─────────────────┘
 */

import type { ReactNode } from 'react';

import { cn } from '@/lib/utils';

interface ShowcaseLayoutProps {
  sidebar: ReactNode;
  canvas: ReactNode;
  controls?: ReactNode;
  className?: string;
}

export function ShowcaseLayout({ sidebar, canvas, controls, className }: ShowcaseLayoutProps) {
  return (
    <div className={cn('flex h-screen w-full overflow-hidden bg-background', className)}>
      {/* Left: Sidebar */}
      <aside className="flex h-full w-[280px] flex-shrink-0 flex-col border-r border-border/60 bg-card/50 backdrop-blur-sm">
        {sidebar}
      </aside>

      {/* Center: Canvas */}
      <main className="flex flex-1 flex-col overflow-hidden">{canvas}</main>

      {/* Right: Controls */}
      {controls && (
        <aside className="flex h-full w-[300px] flex-shrink-0 flex-col border-l border-border/60 bg-card/50 backdrop-blur-sm">
          {controls}
        </aside>
      )}
    </div>
  );
}

'use client';

import type { ReactNode } from 'react';
import { useCallback, useEffect } from 'react';

import { CollapsiblePanel } from '@/components/layout/CollapsiblePanel';
import { useLocalStorage } from '@/hooks/useLocalStorage';

interface LiveSessionLayoutProps {
  leftPanel: ReactNode;
  centerContent: ReactNode;
  rightPanel: ReactNode;
}

export function LiveSessionLayout({
  leftPanel,
  centerContent,
  rightPanel,
}: LiveSessionLayoutProps) {
  const [leftCollapsed, setLeftCollapsed] = useLocalStorage('session-left-collapsed', false);
  const [rightCollapsed, setRightCollapsed] = useLocalStorage('session-right-collapsed', false);
  const toggleLeft = useCallback(() => setLeftCollapsed((v: boolean) => !v), [setLeftCollapsed]);
  const toggleRight = useCallback(() => setRightCollapsed((v: boolean) => !v), [setRightCollapsed]);

  useEffect(() => {
    function handleKeyDown(e: KeyboardEvent) {
      // Skip if user is typing in an input/textarea
      const tag = (e.target as HTMLElement)?.tagName;
      if (tag === 'INPUT' || tag === 'TEXTAREA' || (e.target as HTMLElement)?.isContentEditable) {
        return;
      }

      // Ctrl+[ → toggle left panel
      if (e.ctrlKey && e.key === '[') {
        e.preventDefault();
        toggleLeft();
      }
      // Ctrl+] → toggle right panel
      if (e.ctrlKey && e.key === ']') {
        e.preventDefault();
        toggleRight();
      }
    }

    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [toggleLeft, toggleRight]);

  return (
    <div
      data-testid="live-session-layout"
      className="flex h-[calc(100vh-var(--top-bar-height,48px))] overflow-hidden"
    >
      {/* Left panel — scoreboard, game card */}
      <div className="hidden lg:flex">
        <CollapsiblePanel
          side="left"
          isCollapsed={leftCollapsed}
          onToggle={toggleLeft}
          width="280px"
        >
          {leftPanel}
        </CollapsiblePanel>
      </div>

      {/* Center — activity feed */}
      <div className="flex-1 flex flex-col overflow-hidden">{centerContent}</div>

      {/* Right panel — AI chat, rules, FAQ */}
      <div className="hidden lg:flex">
        <CollapsiblePanel
          side="right"
          isCollapsed={rightCollapsed}
          onToggle={toggleRight}
          width="280px"
        >
          {rightPanel}
        </CollapsiblePanel>
      </div>
    </div>
  );
}

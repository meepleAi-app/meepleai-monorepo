/**
 * Split View Layout Component (Issue #3152)
 *
 * Resizable two-panel layout for Agent Chat + PDF Viewer.
 * Supports drag-to-resize with localStorage persistence.
 */

'use client';

import { useState, useRef, useEffect, useCallback, type ReactNode } from 'react';

import { cn } from '@/lib/utils';

export interface SplitViewLayoutProps {
  /** Left panel content (usually Agent Chat) */
  leftPanel: ReactNode;
  /** Right panel content (usually PDF Viewer) */
  rightPanel: ReactNode;
  /** Initial split ratio (0-1, default 0.5 = 50/50) */
  initialRatio?: number;
  /** Minimum left panel width percentage (default 0.3 = 30%) */
  minLeftRatio?: number;
  /** Maximum left panel width percentage (default 0.7 = 70%) */
  maxLeftRatio?: number;
  /** localStorage key for persisting ratio */
  storageKey?: string;
  /** Additional CSS classes */
  className?: string;
}

export function SplitViewLayout({
  leftPanel,
  rightPanel,
  initialRatio = 0.5,
  minLeftRatio = 0.3,
  maxLeftRatio = 0.7,
  storageKey = 'game-split-ratio',
  className = '',
}: SplitViewLayoutProps) {
  const [leftRatio, setLeftRatio] = useState<number>(initialRatio);
  const [isResizing, setIsResizing] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  // Load persisted ratio from localStorage
  useEffect(() => {
    if (typeof window !== 'undefined' && storageKey) {
      const stored = localStorage.getItem(storageKey);
      if (stored) {
        const ratio = parseFloat(stored);
        if (!isNaN(ratio) && ratio >= minLeftRatio && ratio <= maxLeftRatio) {
          setLeftRatio(ratio);
        }
      }
    }
  }, [storageKey, minLeftRatio, maxLeftRatio]);

  // Persist ratio to localStorage
  useEffect(() => {
    if (typeof window !== 'undefined' && storageKey) {
      localStorage.setItem(storageKey, leftRatio.toString());
    }
  }, [leftRatio, storageKey]);

  // Handle mouse down on divider
  const handleMouseDown = useCallback(() => {
    setIsResizing(true);
    document.body.style.cursor = 'col-resize';
    document.body.style.userSelect = 'none';
  }, []);

  // Handle mouse move during resize
  const handleMouseMove = useCallback(
    (e: MouseEvent) => {
      if (!isResizing || !containerRef.current) return;

      const containerRect = containerRef.current.getBoundingClientRect();
      const newRatio = (e.clientX - containerRect.left) / containerRect.width;

      // Clamp ratio within min/max bounds
      const clampedRatio = Math.max(minLeftRatio, Math.min(maxLeftRatio, newRatio));
      setLeftRatio(clampedRatio);
    },
    [isResizing, minLeftRatio, maxLeftRatio]
  );

  // Handle mouse up (end resize)
  const handleMouseUp = useCallback(() => {
    if (isResizing) {
      setIsResizing(false);
      document.body.style.cursor = '';
      document.body.style.userSelect = '';
    }
  }, [isResizing]);

  // Attach mouse event listeners
  useEffect(() => {
    if (isResizing) {
      document.addEventListener('mousemove', handleMouseMove);
      document.addEventListener('mouseup', handleMouseUp);
      return () => {
        document.removeEventListener('mousemove', handleMouseMove);
        document.removeEventListener('mouseup', handleMouseUp);
      };
    }
  }, [isResizing, handleMouseMove, handleMouseUp]);

  const leftWidthPercentage = leftRatio * 100;
  const rightWidthPercentage = (1 - leftRatio) * 100;

  return (
    <div
      ref={containerRef}
      className={cn('flex h-full overflow-hidden', className)}
    >
      {/* Left Panel */}
      <div
        style={{ width: `${leftWidthPercentage}%` }}
        className="flex-shrink-0 overflow-hidden"
      >
        {leftPanel}
      </div>

      {/* Resizable Divider */}
      <div
        onMouseDown={handleMouseDown}
        className={cn(
          'w-1 bg-gray-200 hover:bg-blue-500 cursor-col-resize transition-all flex-shrink-0',
          isResizing && 'bg-blue-600 w-1.5'
        )}
        title="Trascina per ridimensionare"
      />

      {/* Right Panel */}
      <div
        style={{ width: `${rightWidthPercentage}%` }}
        className="flex-shrink-0 overflow-hidden"
      >
        {rightPanel}
      </div>
    </div>
  );
}

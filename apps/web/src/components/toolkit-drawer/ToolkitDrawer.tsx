'use client';

/**
 * ToolkitDrawer — Slide-down drawer for the Default Game Toolkit
 *
 * Follows the BottomSheet + ExtraMeepleCardDrawer animation patterns:
 * - AnimatePresence + motion.div from framer-motion
 * - Overlay with click-to-close
 * - Escape key closes
 * - Body scroll lock
 * - Glassmorphism panel
 * - Tab bar with toolkit green active color
 *
 * Tab content is placeholder until individual tab components are implemented.
 */

import React, { useCallback, useEffect } from 'react';

import { AnimatePresence, motion } from 'framer-motion';

import { lockScroll, unlockScroll } from '@/lib/scroll-lock';
import { cn } from '@/lib/utils';

import { PlayerBar } from './shared/PlayerBar';
import { DiceRollerTab } from './tabs/DiceRollerTab';
import { EventDiaryTab } from './tabs/EventDiaryTab';
import { NotesTab } from './tabs/NotesTab';
import { ScoreboardTab } from './tabs/ScoreboardTab';
import { ToolkitDrawerProvider, useToolkitDrawer } from './ToolkitDrawerProvider';

import type { ToolkitDrawerProps, ToolkitTab } from './types';

// ============================================================================
// Tab Configuration
// ============================================================================

interface TabConfig {
  key: ToolkitTab;
  label: string;
  icon: string;
  testId: string;
}

const TABS: TabConfig[] = [
  { key: 'dice', label: 'Dadi', icon: '\uD83C\uDFB2', testId: 'toolkit-tab-dice' },
  { key: 'notes', label: 'Note', icon: '\uD83D\uDCDD', testId: 'toolkit-tab-notes' },
  { key: 'diary', label: 'Diario', icon: '\uD83D\uDCD6', testId: 'toolkit-tab-diary' },
  { key: 'scores', label: 'Punti', icon: '\uD83C\uDFC6', testId: 'toolkit-tab-scores' },
];

// ============================================================================
// ToolkitDrawer (public entry point)
// ============================================================================

export function ToolkitDrawer({ gameId, sessionId, onClose, defaultTab }: ToolkitDrawerProps) {
  return (
    <ToolkitDrawerProvider gameId={gameId} sessionId={sessionId} defaultTab={defaultTab}>
      <ToolkitDrawerInner onClose={onClose} />
    </ToolkitDrawerProvider>
  );
}

// ============================================================================
// Inner Component (consumes context)
// ============================================================================

function ToolkitDrawerInner({ onClose }: { onClose: () => void }) {
  const { activeTab, setActiveTab } = useToolkitDrawer();

  // Escape key handler
  const handleKeyDown = useCallback(
    (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    },
    [onClose]
  );

  // Escape key + scroll lock lifecycle
  useEffect(() => {
    document.addEventListener('keydown', handleKeyDown);
    lockScroll();

    return () => {
      document.removeEventListener('keydown', handleKeyDown);
      unlockScroll();
    };
  }, [handleKeyDown]);

  return (
    <AnimatePresence>
      {/* Overlay */}
      <motion.div
        data-testid="toolkit-drawer-overlay"
        className="fixed inset-0 z-50 bg-black/30"
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
        exit={{ opacity: 0 }}
        transition={{ duration: 0.2 }}
        onClick={onClose}
      />

      {/* Panel — slides from top */}
      <motion.div
        data-testid="toolkit-drawer-panel"
        role="dialog"
        aria-modal="true"
        aria-label="Game Toolkit"
        className={cn(
          'fixed inset-x-0 top-0 z-50 flex flex-col',
          'max-h-[85vh]',
          'bg-white/95 backdrop-blur-xl',
          'rounded-b-2xl shadow-2xl'
        )}
        initial={{ y: '-100%' }}
        animate={{ y: 0 }}
        exit={{ y: '-100%' }}
        transition={{ type: 'spring', damping: 30, stiffness: 300 }}
      >
        {/* Drag handle */}
        <div className="flex justify-center pb-1 pt-3" data-testid="toolkit-drag-handle">
          <div className="h-1 w-10 rounded-full bg-gray-300" />
        </div>

        {/* Tab bar */}
        <div
          className="flex shrink-0 border-b border-gray-200 px-2"
          role="tablist"
          aria-label="Toolkit tabs"
        >
          {TABS.map(tab => {
            const isActive = activeTab === tab.key;
            return (
              <button
                key={tab.key}
                role="tab"
                aria-selected={isActive}
                data-testid={tab.testId}
                onClick={() => setActiveTab(tab.key)}
                className={cn(
                  'flex flex-1 flex-col items-center gap-0.5 py-2.5 text-xs font-medium transition-colors',
                  isActive
                    ? 'border-b-2 text-[hsl(142,70%,45%)]'
                    : 'text-gray-500 hover:text-gray-700'
                )}
                style={isActive ? { borderBottomColor: 'hsl(142, 70%, 45%)' } : undefined}
              >
                <span className="text-base">{tab.icon}</span>
                <span>{tab.label}</span>
              </button>
            );
          })}
        </div>

        {/* Tab content */}
        <div className="flex-1 overflow-y-auto px-4 pb-4 pt-4">
          <TabContent tab={activeTab} />
        </div>

        {/* Player bar — always visible at bottom */}
        <PlayerBar />
      </motion.div>
    </AnimatePresence>
  );
}

// ============================================================================
// Tab Content Placeholder
// ============================================================================

function TabContent({ tab }: { tab: ToolkitTab }) {
  switch (tab) {
    case 'dice':
      return <DiceRollerTab />;
    case 'notes':
      return <NotesTab />;
    case 'diary':
      return <EventDiaryTab />;
    case 'scores':
      return <ScoreboardTab />;
    default:
      return null;
  }
}

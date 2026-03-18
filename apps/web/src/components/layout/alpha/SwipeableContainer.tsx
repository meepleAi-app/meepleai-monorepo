'use client';

/**
 * SwipeableContainer — Swipeable panel container for alpha layout tabs.
 *
 * Features:
 * - Opacity-based cross-fade animation between tab panels (250ms)
 * - Horizontal swipe detection to switch adjacent tabs
 * - Each panel has its own scroll container
 * - Uses framer-motion AnimatePresence for enter/exit animations
 */

import { Children, type ReactNode, useCallback } from 'react';

import { AnimatePresence, motion, type PanInfo } from 'framer-motion';

import { useAlphaNav, type AlphaTab } from '@/hooks/useAlphaNav';

const TAB_ORDER: AlphaTab[] = ['home', 'library', 'play', 'chat'];
const SWIPE_THRESHOLD = 50;

interface SwipeableContainerProps {
  children: ReactNode[];
}

export function SwipeableContainer({ children }: SwipeableContainerProps) {
  const { activeTab, setActiveTab, setSectionTitle } = useAlphaNav();
  const activeIndex = TAB_ORDER.indexOf(activeTab);

  const SECTION_TITLES: Record<AlphaTab, string> = {
    home: 'Home',
    library: 'Libreria',
    play: 'Gioca',
    chat: 'Chat',
  };

  const navigateToIndex = useCallback(
    (index: number) => {
      if (index < 0 || index >= TAB_ORDER.length) return;
      const tab = TAB_ORDER[index];
      setActiveTab(tab);
      setSectionTitle(SECTION_TITLES[tab]);
    },
    [setActiveTab, setSectionTitle]
  );

  const handlePanEnd = useCallback(
    (_event: MouseEvent | TouchEvent | PointerEvent, info: PanInfo) => {
      const { offset } = info;
      // Only trigger if horizontal movement exceeds threshold
      // and is more horizontal than vertical
      if (Math.abs(offset.x) > SWIPE_THRESHOLD && Math.abs(offset.x) > Math.abs(offset.y)) {
        if (offset.x < 0) {
          // Swipe left → next tab
          navigateToIndex(activeIndex + 1);
        } else {
          // Swipe right → previous tab
          navigateToIndex(activeIndex - 1);
        }
      }
    },
    [activeIndex, navigateToIndex]
  );

  const childArray = Children.toArray(children);
  const activeChild = childArray[activeIndex] ?? null;

  return (
    <motion.div className="flex-1 relative overflow-hidden" onPanEnd={handlePanEnd}>
      <AnimatePresence mode="wait">
        <motion.div
          key={activeTab}
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0 }}
          transition={{ duration: 0.25, ease: 'easeOut' }}
          className="absolute inset-0 overflow-y-auto h-full pb-16 lg:pb-0"
        >
          {activeChild}
        </motion.div>
      </AnimatePresence>
    </motion.div>
  );
}

'use client';

import React, { useCallback, useEffect } from 'react';

import { AnimatePresence, motion } from 'framer-motion';

import { cn } from '@/lib/utils';

export interface BottomSheetProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title?: string;
  height?: 'auto' | 'half' | 'full';
  children: React.ReactNode;
  className?: string;
}

const heightClasses = {
  auto: 'max-h-[85vh]',
  half: 'h-[50vh]',
  full: 'h-[90vh]',
} as const;

export function BottomSheet({
  open,
  onOpenChange,
  title,
  height = 'auto',
  children,
  className,
}: BottomSheetProps) {
  const handleKeyDown = useCallback(
    (e: KeyboardEvent) => {
      if (e.key === 'Escape') onOpenChange(false);
    },
    [onOpenChange]
  );

  useEffect(() => {
    if (open) {
      document.addEventListener('keydown', handleKeyDown);
      document.body.style.overflow = 'hidden';
    }
    return () => {
      document.removeEventListener('keydown', handleKeyDown);
      if (open) {
        document.body.style.overflow = '';
      }
    };
  }, [open, handleKeyDown]);

  return (
    <AnimatePresence>
      {open && (
        <>
          <motion.div
            data-testid="bottom-sheet-overlay"
            className="fixed inset-0 z-50 bg-black/60"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            transition={{ duration: 0.2 }}
            onClick={() => onOpenChange(false)}
          />
          <motion.div
            data-testid="bottom-sheet-content"
            role="dialog"
            aria-modal="true"
            aria-labelledby={title ? 'bottom-sheet-title' : undefined}
            aria-label={title ? undefined : 'Sheet'}
            className={cn(
              'sheet-surface fixed inset-x-0 bottom-0 z-50 flex flex-col overflow-hidden',
              heightClasses[height],
              className
            )}
            initial={{ y: '100%' }}
            animate={{ y: 0 }}
            exit={{ y: '100%' }}
            transition={{ type: 'spring', damping: 30, stiffness: 300 }}
          >
            <div className="flex justify-center py-3" data-testid="drag-handle">
              <div className="h-1 w-10 rounded-full bg-white/20" />
            </div>
            {title && (
              <div className="px-4 pb-3">
                <h2
                  id="bottom-sheet-title"
                  className="text-lg font-semibold text-[var(--gaming-text-primary)]"
                >
                  {title}
                </h2>
              </div>
            )}
            <div className="flex-1 overflow-y-auto px-4 pb-8">{children}</div>
          </motion.div>
        </>
      )}
    </AnimatePresence>
  );
}

'use client';

/**
 * Service Worker Update Prompt (Issue #3346)
 *
 * Shows a notification when a new version of the app is available.
 */

import { motion, AnimatePresence } from 'framer-motion';
import { RefreshCw, Sparkles } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { usePWA } from '@/lib/domain-hooks/usePWA';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export interface UpdatePromptProps {
  /** Custom class name */
  className?: string;
}

// ============================================================================
// Component
// ============================================================================

export function UpdatePrompt({ className }: UpdatePromptProps) {
  const { updateAvailable, actions } = usePWA();

  if (!updateAvailable) return null;

  return (
    <AnimatePresence>
      <motion.div
        initial={{ opacity: 0, y: -50 }}
        animate={{ opacity: 1, y: 0 }}
        exit={{ opacity: 0, y: -50 }}
        transition={{ type: 'spring', damping: 25 }}
        className={cn('fixed top-4 left-4 right-4 z-50 mx-auto max-w-md', className)}
      >
        <div className="relative overflow-hidden rounded-xl bg-gradient-to-r from-blue-600/90 to-blue-500/90 p-4 text-white shadow-xl backdrop-blur-sm">
          <div className="flex items-start gap-4">
            {/* Icon */}
            <div className="flex-shrink-0 rounded-lg bg-white/20 p-2">
              <Sparkles className="h-6 w-6" />
            </div>

            {/* Content */}
            <div className="flex-1 min-w-0">
              <h3 className="font-semibold text-base">Update Available</h3>
              <p className="mt-1 text-sm opacity-90">
                A new version of MeepleAI is ready. Refresh to get the latest features.
              </p>

              {/* Update button */}
              <Button
                onClick={actions.applyUpdate}
                variant="secondary"
                size="sm"
                className="mt-3 gap-2"
              >
                <RefreshCw className="h-4 w-4" />
                Update Now
              </Button>
            </div>
          </div>

          {/* Decorative elements */}
          <div className="absolute -right-4 -top-4 h-24 w-24 rounded-full bg-white/10" />
          <div className="absolute -bottom-2 -left-2 h-16 w-16 rounded-full bg-white/5" />
        </div>
      </motion.div>
    </AnimatePresence>
  );
}

export default UpdatePrompt;

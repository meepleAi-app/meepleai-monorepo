'use client';

/**
 * PWA Install Prompt Component (Issue #3346)
 *
 * Shows a prompt to install the app on supported devices.
 */

import { useState, useEffect } from 'react';

import { motion, AnimatePresence } from 'framer-motion';
import { Download, X, Smartphone, Monitor, Sparkles } from 'lucide-react';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { usePWA } from '@/lib/domain-hooks/usePWA';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export interface InstallPromptProps {
  /** Delay before showing the prompt (ms) */
  showDelay?: number;

  /** Whether to show as a banner instead of dialog */
  variant?: 'dialog' | 'banner' | 'button';

  /** Custom class name */
  className?: string;

  /** Callback after install attempt */
  onInstallResult?: (installed: boolean) => void;
}

// ============================================================================
// Constants
// ============================================================================

const DISMISSED_KEY = 'meepleai-install-dismissed';
const DISMISSED_DURATION = 7 * 24 * 60 * 60 * 1000; // 7 days

// ============================================================================
// Component
// ============================================================================

export function InstallPrompt({
  showDelay = 30000, // 30 seconds
  variant = 'banner',
  className,
  onInstallResult,
}: InstallPromptProps) {
  const { canInstall, isStandalone, actions } = usePWA();
  const [showPrompt, setShowPrompt] = useState(false);
  const [installing, setInstalling] = useState(false);

  // Check if already dismissed
  useEffect(() => {
    if (!canInstall || isStandalone) return;

    const dismissedAt = localStorage.getItem(DISMISSED_KEY);
    if (dismissedAt) {
      const elapsed = Date.now() - parseInt(dismissedAt, 10);
      if (elapsed < DISMISSED_DURATION) {
        return; // Still within dismiss period
      }
    }

    // Show prompt after delay
    const timeout = setTimeout(() => {
      setShowPrompt(true);
    }, showDelay);

    return () => clearTimeout(timeout);
  }, [canInstall, isStandalone, showDelay]);

  const handleInstall = async () => {
    setInstalling(true);
    const installed = await actions.install();
    setInstalling(false);
    setShowPrompt(false);
    onInstallResult?.(installed);
  };

  const handleDismiss = () => {
    localStorage.setItem(DISMISSED_KEY, Date.now().toString());
    setShowPrompt(false);
  };

  // Don't render if already installed or can't install
  if (isStandalone || (!canInstall && variant !== 'button')) {
    return null;
  }

  // Button variant - always visible if can install
  if (variant === 'button') {
    if (!canInstall) return null;

    return (
      <Button onClick={handleInstall} disabled={installing} className={cn('gap-2', className)}>
        <Download className={cn('h-4 w-4', installing && 'animate-bounce')} />
        {installing ? 'Installing...' : 'Install App'}
      </Button>
    );
  }

  // Banner variant
  if (variant === 'banner') {
    return (
      <AnimatePresence>
        {showPrompt && (
          <motion.div
            initial={{ opacity: 0, y: 100 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: 100 }}
            transition={{ type: 'spring', damping: 25 }}
            className={cn('fixed bottom-4 left-4 right-4 z-50 mx-auto max-w-md', className)}
          >
            <div className="relative overflow-hidden rounded-xl bg-gradient-to-r from-primary/90 to-primary/80 p-4 text-primary-foreground shadow-xl backdrop-blur-sm">
              {/* Close button */}
              <button
                onClick={handleDismiss}
                className="absolute right-2 top-2 rounded-full p-1 hover:bg-white/20 transition-colors"
                aria-label="Dismiss"
              >
                <X className="h-4 w-4" />
              </button>

              <div className="flex items-start gap-4">
                {/* Icon */}
                <div className="flex-shrink-0 rounded-lg bg-white/20 p-2">
                  <Sparkles className="h-6 w-6" />
                </div>

                {/* Content */}
                <div className="flex-1 min-w-0">
                  <h3 className="font-semibold text-base">Install MeepleAI</h3>
                  <p className="mt-1 text-sm opacity-90">
                    Add to home screen for quick access and offline support
                  </p>

                  {/* Install button */}
                  <Button
                    onClick={handleInstall}
                    disabled={installing}
                    variant="secondary"
                    size="sm"
                    className="mt-3 gap-2"
                  >
                    <Download className={cn('h-4 w-4', installing && 'animate-bounce')} />
                    {installing ? 'Installing...' : 'Install'}
                  </Button>
                </div>
              </div>

              {/* Decorative elements */}
              <div className="absolute -right-4 -top-4 h-24 w-24 rounded-full bg-white/10" />
              <div className="absolute -bottom-2 -left-2 h-16 w-16 rounded-full bg-white/5" />
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    );
  }

  // Dialog variant
  return (
    <Dialog open={showPrompt} onOpenChange={open => !open && handleDismiss()}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <Sparkles className="h-5 w-5 text-primary" />
            Install MeepleAI
          </DialogTitle>
          <DialogDescription>Get the full app experience with offline support</DialogDescription>
        </DialogHeader>

        <div className="space-y-4 py-4">
          {/* Features list */}
          <div className="space-y-3">
            <Feature
              icon={<Smartphone className="h-4 w-4" />}
              title="Quick Access"
              description="Launch from home screen like a native app"
            />
            <Feature
              icon={<Monitor className="h-4 w-4" />}
              title="Offline Mode"
              description="Access sessions and games without internet"
            />
            <Feature
              icon={<Download className="h-4 w-4" />}
              title="Sync Automatically"
              description="Changes sync when you're back online"
            />
          </div>
        </div>

        {/* Actions */}
        <div className="flex gap-2 justify-end">
          <Button variant="ghost" onClick={handleDismiss}>
            Not Now
          </Button>
          <Button onClick={handleInstall} disabled={installing} className="gap-2">
            <Download className={cn('h-4 w-4', installing && 'animate-bounce')} />
            {installing ? 'Installing...' : 'Install App'}
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}

// ============================================================================
// Sub-components
// ============================================================================

function Feature({
  icon,
  title,
  description,
}: {
  icon: React.ReactNode;
  title: string;
  description: string;
}) {
  return (
    <div className="flex items-start gap-3">
      <div className="flex-shrink-0 rounded-full bg-primary/10 p-2 text-primary">{icon}</div>
      <div>
        <h4 className="text-sm font-medium">{title}</h4>
        <p className="text-xs text-muted-foreground">{description}</p>
      </div>
    </div>
  );
}

export default InstallPrompt;

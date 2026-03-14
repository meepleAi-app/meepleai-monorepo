'use client';

import React, { useState } from 'react';

import { Info, X } from 'lucide-react';

import { useToolboxStore } from '@/lib/stores/toolboxStore';
import { cn } from '@/lib/utils';

interface OfflineBannerProps {
  className?: string;
  'data-testid'?: string;
}

/**
 * Offline indicator banner for the Game Toolbox.
 * Shows when the toolbox is in offline-only mode.
 * Epic #412 — Game Toolbox.
 */
export function OfflineBanner({ className = '', 'data-testid': testId }: OfflineBannerProps) {
  const isOffline = useToolboxStore(s => s.isOffline);
  const [isDismissed, setIsDismissed] = useState(false);

  if (!isOffline || isDismissed) return null;

  return (
    <div
      className={cn(
        'flex items-center gap-2 rounded-md bg-amber-100 px-4 py-2 text-sm text-amber-800 dark:bg-amber-900/30 dark:text-amber-200',
        className
      )}
      role="status"
      data-testid={testId ?? 'offline-banner'}
    >
      <Info className="h-4 w-4 shrink-0" />
      <span className="flex-1">Modalit&agrave; offline &mdash; solo per te</span>
      <button
        type="button"
        onClick={() => setIsDismissed(true)}
        className="shrink-0 rounded p-0.5 hover:bg-amber-200 dark:hover:bg-amber-800/50"
        aria-label="Dismiss offline banner"
        data-testid="offline-banner-dismiss"
      >
        <X className="h-3.5 w-3.5" />
      </button>
    </div>
  );
}

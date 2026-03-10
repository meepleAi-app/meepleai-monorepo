/**
 * OfflineFaqBadge Component (Issue #5586)
 *
 * Displays a badge indicating the response source:
 * - cached: "From offline FAQ" with clipboard icon
 * - offline: "No cached answer" with alert icon
 *
 * Used in chat messages when the LLM is unavailable
 * and the system falls back to cached FAQ data.
 */

'use client';

import React from 'react';

import { ClipboardList, AlertTriangle, Wifi } from 'lucide-react';

import type { ResponseSource } from '@/hooks/useGracefulDegradation';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

interface OfflineFaqBadgeProps {
  /** The source of the response */
  source: ResponseSource;
  /** Confidence score (0-1), shown for cached responses */
  confidence?: number;
  /** Additional CSS classes */
  className?: string;
}

// ============================================================================
// Config
// ============================================================================

const SOURCE_CONFIG: Record<
  Exclude<ResponseSource, 'live'>,
  { label: string; icon: React.ElementType; color: string; bgColor: string }
> = {
  cached: {
    label: 'From offline FAQ',
    icon: ClipboardList,
    color: 'hsl(38 92% 50%)', // Amber
    bgColor: 'hsl(38 92% 95%)',
  },
  offline: {
    label: 'No cached answer',
    icon: AlertTriangle,
    color: 'hsl(0 72% 51%)', // Red
    bgColor: 'hsl(0 72% 95%)',
  },
};

// ============================================================================
// Component
// ============================================================================

export function OfflineFaqBadge({ source, confidence, className }: OfflineFaqBadgeProps) {
  // Don't render for live responses
  if (source === 'live') return null;

  const config = SOURCE_CONFIG[source];
  const Icon = config.icon;

  return (
    <div
      className={cn(
        'inline-flex items-center gap-1.5 px-2 py-0.5 rounded-md text-xs font-medium',
        'border',
        className
      )}
      style={{
        color: config.color,
        backgroundColor: config.bgColor,
        borderColor: config.color,
      }}
      role="status"
      aria-label={config.label}
    >
      <Icon className="w-3 h-3 flex-shrink-0" aria-hidden="true" />
      <span>{config.label}</span>
      {source === 'cached' && confidence !== undefined && confidence > 0 && (
        <span className="opacity-70">({Math.round(confidence * 100)}%)</span>
      )}
    </div>
  );
}

/**
 * Compact inline version for use within chat message text.
 */
export function OfflineFaqIndicator({ source }: { source: ResponseSource }) {
  if (source === 'live') return null;

  if (source === 'cached') {
    return (
      <span
        className="inline-flex items-center gap-1 text-amber-600 dark:text-amber-400 text-xs"
        role="status"
      >
        <ClipboardList className="w-3 h-3 inline" aria-hidden="true" />
        From offline FAQ
      </span>
    );
  }

  return (
    <span
      className="inline-flex items-center gap-1 text-red-500 dark:text-red-400 text-xs"
      role="status"
    >
      <Wifi className="w-3 h-3 inline" aria-hidden="true" />
      Offline
    </span>
  );
}

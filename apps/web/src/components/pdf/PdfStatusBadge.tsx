/**
 * PdfStatusBadge Component (Issue #4217)
 *
 * Reusable badge displaying PDF processing state with glassmorphic design.
 * Supports 7 state variants with icons and semantic colors.
 *
 * Features:
 * - Glassmorphic design with backdrop-blur
 * - State-specific icons from lucide-react
 * - Two size variants: default and compact
 * - Optional icon display
 * - WCAG 2.1 AA compliant colors
 */

'use client';

import {
  Upload,
  FileText,
  Scissors,
  Cpu,
  Database,
  CheckCircle2,
  XCircle,
  Clock,
} from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { cn } from '@/lib/utils';
import type { PdfState } from '@/types/pdf';

// ============================================================================
// Types
// ============================================================================

export interface PdfStatusBadgeProps {
  /** Current PDF processing state */
  state: PdfState;
  /** Size variant */
  variant?: 'default' | 'compact';
  /** Show icon before label */
  showIcon?: boolean;
  /** Additional CSS classes */
  className?: string;
}

interface StateConfig {
  icon: typeof Upload;
  label: string;
  colorClass: string;
}

// ============================================================================
// Constants
// ============================================================================

const STATE_CONFIG: Record<PdfState, StateConfig> = {
  pending: {
    icon: Clock,
    label: 'Pending',
    colorClass: 'bg-gray-100 text-gray-900 dark:bg-gray-800 dark:text-gray-100',
  },
  uploading: {
    icon: Upload,
    label: 'Uploading',
    colorClass: 'bg-blue-100 text-blue-900 dark:bg-blue-900/30 dark:text-blue-100',
  },
  extracting: {
    icon: FileText,
    label: 'Extracting',
    colorClass: 'bg-purple-100 text-purple-900 dark:bg-purple-900/30 dark:text-purple-100',
  },
  chunking: {
    icon: Scissors,
    label: 'Chunking',
    colorClass: 'bg-indigo-100 text-indigo-900 dark:bg-indigo-900/30 dark:text-indigo-100',
  },
  embedding: {
    icon: Cpu,
    label: 'Embedding',
    colorClass: 'bg-violet-100 text-violet-900 dark:bg-violet-900/30 dark:text-violet-100',
  },
  indexing: {
    icon: Database,
    label: 'Indexing',
    colorClass: 'bg-fuchsia-100 text-fuchsia-900 dark:bg-fuchsia-900/30 dark:text-fuchsia-100',
  },
  ready: {
    icon: CheckCircle2,
    label: 'Ready',
    colorClass: 'bg-green-100 text-green-900 dark:bg-green-900/30 dark:text-green-100',
  },
  failed: {
    icon: XCircle,
    label: 'Failed',
    colorClass: 'bg-red-100 text-red-900 dark:bg-red-900/30 dark:text-red-100',
  },
};

// ============================================================================
// Component
// ============================================================================

export function PdfStatusBadge({
  state,
  variant = 'default',
  showIcon = true,
  className,
}: PdfStatusBadgeProps) {
  // eslint-disable-next-line security/detect-object-injection -- state is from typed PdfState union
  const config = STATE_CONFIG[state];
  const Icon = config.icon;

  const isCompact = variant === 'compact';

  return (
    <Badge
      className={cn(
        // Base glassmorphic style
        'backdrop-blur-md border-0 font-medium transition-all duration-200',
        // State-specific colors
        config.colorClass,
        // Size variants
        isCompact ? 'px-2 py-0.5 text-xs' : 'px-3 py-1 text-sm',
        // Custom classes
        className
      )}
      data-testid="pdf-status-badge"
      data-state={state}
      aria-label={`PDF Status: ${config.label}`}
    >
      {showIcon && (
        <Icon
          className={cn(
            'shrink-0',
            isCompact ? 'h-3 w-3 mr-1' : 'h-4 w-4 mr-1.5'
          )}
          aria-hidden="true"
        />
      )}
      <span>{config.label}</span>
    </Badge>
  );
}

export default PdfStatusBadge;

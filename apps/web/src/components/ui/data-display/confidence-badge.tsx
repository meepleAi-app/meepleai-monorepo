/**
 * ConfidenceBadge Component - AI Confidence Level Indicator
 *
 * Visual indicator for AI response confidence with color-coded badges
 * and explanatory tooltips. Part of the Playful Boardroom design system.
 *
 * @see Issue #1832 (UI-005)
 */

import React from 'react';

import { cn } from '@/lib/utils';

import { Badge } from './badge';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '../overlays/tooltip';

// ============================================================================
// Types
// ============================================================================

export interface ConfidenceBadgeProps {
  /** Confidence score (0-100) */
  confidence: number;
  /** Show explanatory tooltip (default: true) */
  showTooltip?: boolean;
  /** Additional CSS classes */
  className?: string;
}

export type ConfidenceLevel = 'high' | 'medium' | 'low';

// ============================================================================
// Constants
// ============================================================================

const CONFIDENCE_LEVELS = {
  high: {
    threshold: 85,
    label: 'Confident',
    description: 'High accuracy expected',
    colorClass: 'bg-green-500 hover:bg-green-600 text-white border-transparent',
    ariaLabel: 'High confidence',
  },
  medium: {
    threshold: 70,
    label: 'Likely Correct',
    description: 'Moderate confidence',
    colorClass: 'bg-yellow-500 hover:bg-yellow-600 text-white border-transparent',
    ariaLabel: 'Medium confidence',
  },
  low: {
    threshold: 0,
    label: 'Uncertain',
    description: 'Low confidence, verify manually',
    colorClass: 'bg-red-500 hover:bg-red-600 text-white border-transparent',
    ariaLabel: 'Low confidence',
  },
} as const;

// ============================================================================
// Helper Functions
// ============================================================================

/**
 * Determines confidence level based on score
 */
function getConfidenceLevel(confidence: number): ConfidenceLevel {
  if (confidence >= CONFIDENCE_LEVELS.high.threshold) return 'high';
  if (confidence >= CONFIDENCE_LEVELS.medium.threshold) return 'medium';
  return 'low';
}

/**
 * Gets configuration for a confidence level
 */
function getConfidenceConfig(level: ConfidenceLevel) {
  return CONFIDENCE_LEVELS[level];
}

/**
 * Validates confidence score is within valid range
 */
function validateConfidence(confidence: number): number {
  if (confidence < 0) return 0;
  if (confidence > 100) return 100;
  return confidence;
}

// ============================================================================
// Main Component
// ============================================================================

/**
 * ConfidenceBadge - Color-coded confidence indicator with tooltip
 *
 * Displays AI confidence levels with semantic colors and explanations:
 * - Green (≥85%): Confident - High accuracy expected
 * - Yellow (70-84%): Likely correct - Moderate confidence
 * - Red (<70%): Uncertain - Low confidence, verify manually
 *
 * @example
 * ```tsx
 * // High confidence
 * <ConfidenceBadge confidence={95} />
 *
 * // Medium confidence without tooltip
 * <ConfidenceBadge confidence={75} showTooltip={false} />
 *
 * // Low confidence
 * <ConfidenceBadge confidence={55} />
 * ```
 */
export const ConfidenceBadge = React.memo<ConfidenceBadgeProps>(
  ({ confidence, showTooltip = true, className }) => {
    const validatedConfidence = validateConfidence(confidence);
    const level = getConfidenceLevel(validatedConfidence);
    const config = getConfidenceConfig(level);

    const badgeContent = (
      <Badge
        className={cn('text-xs font-medium', config.colorClass, className)}
        aria-label={`${config.ariaLabel}: ${validatedConfidence}%`}
      >
        {validatedConfidence}%
      </Badge>
    );

    // Without tooltip - just return badge
    if (!showTooltip) {
      return badgeContent;
    }

    // With tooltip
    return (
      <TooltipProvider>
        <Tooltip>
          <TooltipTrigger asChild>{badgeContent}</TooltipTrigger>
          <TooltipContent>
            <div className="flex flex-col gap-1">
              <p className="font-semibold text-sm">{config.label}</p>
              <p className="text-xs text-muted-foreground">{config.description}</p>
            </div>
          </TooltipContent>
        </Tooltip>
      </TooltipProvider>
    );
  }
);

ConfidenceBadge.displayName = 'ConfidenceBadge';

// ============================================================================
// Exports
// ============================================================================

export { getConfidenceLevel, getConfidenceConfig, validateConfidence };
export { CONFIDENCE_LEVELS };

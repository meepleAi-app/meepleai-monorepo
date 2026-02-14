/**
 * ConfidenceBadge - AI Confidence Score Indicator
 * Issue #4163: Step 2 - Metadata Extraction
 *
 * Displays color-coded confidence score for AI-extracted metadata
 */

'use client';

import { AlertCircle, CheckCircle, AlertTriangle, type LucideIcon } from 'lucide-react';

import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/overlays/tooltip';
import { cn } from '@/lib/utils';

export interface ConfidenceBadgeProps {
  /**
   * Confidence score (0-100)
   */
  confidence: number;

  /**
   * Optional size variant
   * @default 'md'
   */
  size?: 'sm' | 'md';

  /**
   * Optional className for custom styling
   */
  className?: string;
}

type ConfidenceLevel = 'high' | 'medium' | 'low';

interface ConfidenceConfig {
  label: string;
  bgClass: string;
  textClass: string;
  icon: LucideIcon;
  description: string;
}

const confidenceConfig: Record<ConfidenceLevel, ConfidenceConfig> = {
  high: {
    label: 'Alta Confidenza',
    bgClass: 'bg-green-100 dark:bg-green-900',
    textClass: 'text-green-900 dark:text-green-100',
    icon: CheckCircle,
    description: 'Estrazione di alta qualità. Tutti i campi chiave sono stati estratti correttamente.',
  },
  medium: {
    label: 'Media Confidenza',
    bgClass: 'bg-yellow-100 dark:bg-yellow-900',
    textClass: 'text-yellow-900 dark:text-yellow-100',
    icon: AlertTriangle,
    description: 'Estrazione di media qualità. Alcuni campi potrebbero essere mancanti o imprecisi.',
  },
  low: {
    label: 'Bassa Confidenza',
    bgClass: 'bg-red-100 dark:bg-red-900',
    textClass: 'text-red-900 dark:text-red-100',
    icon: AlertCircle,
    description: 'Estrazione di bassa qualità. Revisione manuale fortemente consigliata.',
  },
};

function getConfidenceLevel(confidence: number): ConfidenceLevel {
  if (confidence >= 80) return 'high';
  if (confidence >= 50) return 'medium';
  return 'low';
}

/**
 * Badge component for displaying AI confidence scores with color-coded levels
 *
 * @example
 * ```tsx
 * <ConfidenceBadge confidence={85} size="md" />
 * ```
 */
export function ConfidenceBadge({ confidence, size = 'md', className }: ConfidenceBadgeProps) {
  const level = getConfidenceLevel(confidence);
  const config = confidenceConfig[level];
  const Icon = config.icon;

  return (
    <TooltipProvider>
      <Tooltip>
        <TooltipTrigger asChild>
          <span
            className={cn(
              'inline-flex items-center gap-1.5 px-3 py-1 rounded-md font-semibold',
              size === 'sm' ? 'text-xs' : 'text-sm',
              config.bgClass,
              config.textClass,
              className
            )}
            data-testid={`confidence-badge-${level}`}
          >
            <Icon className={cn('w-4 h-4', size === 'sm' && 'w-3 h-3')} />
            {config.label}: {Math.round(confidence)}%
          </span>
        </TooltipTrigger>
        <TooltipContent>
          <p>{config.description}</p>
        </TooltipContent>
      </Tooltip>
    </TooltipProvider>
  );
}

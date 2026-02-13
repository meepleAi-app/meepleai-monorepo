/**
 * PdfErrorCard Component (Issue #4217)
 * Error display with category-specific icons and retry functionality
 */

'use client';

import { AlertCircle, Wifi, FileWarning, Database, ServerCrash, RefreshCw } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

export interface PdfErrorCardProps {
  error: string;
  category: 'network' | 'parsing' | 'quota' | 'service' | 'unknown';
  canRetry: boolean;
  onRetry?: () => void;
  className?: string;
}

const CATEGORY_CONFIG = {
  network: { icon: Wifi, label: 'Network Error', color: 'text-orange-600' },
  parsing: { icon: FileWarning, label: 'Parsing Error', color: 'text-yellow-600' },
  quota: { icon: Database, label: 'Quota Exceeded', color: 'text-purple-600' },
  service: { icon: ServerCrash, label: 'Service Error', color: 'text-blue-600' },
  unknown: { icon: AlertCircle, label: 'Unknown Error', color: 'text-red-600' },
};

export function PdfErrorCard({ error, category, canRetry, onRetry, className }: PdfErrorCardProps) {
  // eslint-disable-next-line security/detect-object-injection -- category is from typed union
  const config = CATEGORY_CONFIG[category];
  const Icon = config.icon;

  return (
    <div
      className={cn(
        'rounded-lg border border-red-200 bg-red-50 p-4 backdrop-blur-md',
        'dark:border-red-900/50 dark:bg-red-900/20',
        className
      )}
      role="alert"
      data-testid="pdf-error-card"
      data-category={category}
    >
      <div className="flex items-start gap-3">
        <Icon className={cn('h-5 w-5 shrink-0 mt-0.5', config.color)} aria-hidden="true" />
        <div className="flex-1 min-w-0">
          <p className="text-sm font-semibold text-red-900 dark:text-red-100">{config.label}</p>
          <p className="mt-1 text-sm text-red-700 dark:text-red-200">{error}</p>
        </div>
        {canRetry && onRetry && (
          <Button
            variant="outline"
            size="sm"
            onClick={onRetry}
            className="shrink-0"
            data-testid="retry-button"
          >
            <RefreshCw className="mr-1.5 h-3.5 w-3.5" />
            Retry
          </Button>
        )}
      </div>
    </div>
  );
}

export default PdfErrorCard;

import { Badge } from '@/components/ui/data-display/badge';
import { cn } from '@/lib/utils';

export interface ConfidenceBadgeProps {
  /** Confidence score (0-1) */
  confidence: number | null;
  /** Show numeric value */
  showValue?: boolean;
  /** Additional className */
  className?: string;
}

/**
 * Confidence badge with color-coded quality level
 * - High: >= 0.80 (green)
 * - Medium: >= 0.50 (amber)
 * - Low: < 0.50 (red)
 */
export function ConfidenceBadge({
  confidence,
  showValue = true,
  className,
}: ConfidenceBadgeProps) {
  if (confidence === null || confidence === undefined) {
    return (
      <Badge variant="secondary" className={cn('font-mono', className)}>
        N/A
      </Badge>
    );
  }

  const level =
    confidence >= 0.8 ? 'high' : confidence >= 0.5 ? 'medium' : 'low';

  const variants = {
    high: 'default' as const, // green (uses primary color)
    medium: 'secondary' as const, // amber
    low: 'destructive' as const, // red
  };

  const labels = {
    high: 'High',
    medium: 'Medium',
    low: 'Low',
  };

  return (
    <Badge
      variant={variants[level]}
      className={cn(
        'font-mono',
        level === 'high' && 'shadow-[0_0_8px_rgba(34,197,94,0.3)]',
        className
      )}
    >
      {showValue ? confidence.toFixed(2) : labels[level]}
    </Badge>
  );
}

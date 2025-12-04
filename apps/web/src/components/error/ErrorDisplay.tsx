/**
 * ErrorDisplay - Error state component with retry functionality
 *
 * Provides visual feedback when errors occur in the Q&A interface.
 * Supports different error types with appropriate icons and messaging.
 * Includes retry button with optional countdown timer for rate limits.
 *
 * Follows Playful Boardroom design system (Opzione A) and WCAG 2.1 AA guidelines.
 *
 * @see docs/04-frontend/wireframes-playful-boardroom.md
 * @issue BGAI-068
 *
 * @example
 * ```tsx
 * // Basic usage
 * <ErrorDisplay
 *   title="Errore di connessione"
 *   description="Impossibile raggiungere il server"
 *   onRetry={handleRetry}
 * />
 *
 * // Rate limit with countdown
 * <ErrorDisplay
 *   variant="rateLimit"
 *   title="Troppe richieste"
 *   retryAfterSeconds={30}
 *   onRetry={handleRetry}
 * />
 *
 * // Network error
 * <ErrorDisplay
 *   variant="network"
 *   error={networkError}
 *   onRetry={handleRetry}
 * />
 * ```
 */

import { useEffect, useState, useCallback } from 'react';
import { AlertCircle, WifiOff, ServerCrash, Clock, RefreshCw, type LucideIcon } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import { useReducedMotion } from '@/lib/animations';
import { ApiError, NetworkError, ServerError, RateLimitError } from '@/lib/api/core/errors';
import { getErrorMessage } from '@/lib/utils/errorHandler';

export type ErrorVariant = 'generic' | 'network' | 'server' | 'rateLimit';

export interface ErrorDisplayProps {
  /**
   * Main title text (required)
   */
  title: string;

  /**
   * Optional description text providing additional context
   */
  description?: string;

  /**
   * Error variant determining icon and styling
   * @default 'generic'
   */
  variant?: ErrorVariant;

  /**
   * Original error object for extracting details
   */
  error?: Error | ApiError | null;

  /**
   * Callback when retry button is clicked
   */
  onRetry?: () => void;

  /**
   * Label for retry button
   * @default 'Riprova'
   */
  retryLabel?: string;

  /**
   * Seconds until retry is allowed (for rate limit countdown)
   */
  retryAfterSeconds?: number;

  /**
   * Show retry button
   * @default true
   */
  showRetry?: boolean;

  /**
   * Additional CSS classes
   */
  className?: string;

  /**
   * Test ID for testing purposes
   */
  testId?: string;
}

/**
 * Default icons for each variant
 */
const VARIANT_ICONS: Record<ErrorVariant, LucideIcon> = {
  generic: AlertCircle,
  network: WifiOff,
  server: ServerCrash,
  rateLimit: Clock,
};

/**
 * Variant-specific icon colors (Playful Boardroom palette)
 */
const VARIANT_ICON_COLORS: Record<ErrorVariant, string> = {
  generic: 'text-red-500 dark:text-red-400',
  network: 'text-yellow-500 dark:text-yellow-400',
  server: 'text-orange-500 dark:text-orange-400',
  rateLimit: 'text-purple-500 dark:text-purple-400',
};

/**
 * Variant-specific background colors
 */
const VARIANT_BG_COLORS: Record<ErrorVariant, string> = {
  generic: 'bg-red-50 dark:bg-red-950/30',
  network: 'bg-yellow-50 dark:bg-yellow-950/30',
  server: 'bg-orange-50 dark:bg-orange-950/30',
  rateLimit: 'bg-purple-50 dark:bg-purple-950/30',
};

/**
 * Default descriptions for each variant (Italian)
 */
const VARIANT_DESCRIPTIONS: Record<ErrorVariant, string> = {
  generic: 'Si è verificato un errore imprevisto. Riprova più tardi.',
  network: 'Impossibile connettersi al server. Verifica la tua connessione internet.',
  server: 'Il server sta riscontrando problemi. Riprova tra qualche minuto.',
  rateLimit: 'Hai effettuato troppe richieste. Attendi prima di riprovare.',
};

/**
 * Detect error variant from error object
 */
function detectVariant(error: Error | ApiError | null | undefined): ErrorVariant {
  if (!error) return 'generic';
  if (error instanceof RateLimitError) return 'rateLimit';
  if (error instanceof NetworkError) return 'network';
  if (error instanceof ServerError) return 'server';
  if (error instanceof ApiError) {
    const status = error.statusCode;
    if (status === 429) return 'rateLimit';
    if (status && status >= 500) return 'server';
  }
  return 'generic';
}

/**
 * Extract retry after seconds from error
 */
function extractRetryAfter(error: Error | ApiError | null | undefined): number | undefined {
  if (error instanceof RateLimitError) {
    return error.getRetryAfterSeconds();
  }
  return undefined;
}

/**
 * Format countdown time
 */
function formatCountdown(seconds: number): string {
  if (seconds <= 0) return '';
  if (seconds < 60) return `${seconds}s`;
  const minutes = Math.floor(seconds / 60);
  const remainingSeconds = seconds % 60;
  if (remainingSeconds === 0) return `${minutes}m`;
  return `${minutes}m ${remainingSeconds}s`;
}

/**
 * ErrorDisplay component for error states with retry functionality
 */
export function ErrorDisplay({
  title,
  description,
  variant,
  error,
  onRetry,
  retryLabel = 'Riprova',
  retryAfterSeconds,
  showRetry = true,
  className,
  testId = 'error-display',
}: ErrorDisplayProps) {
  const shouldReduceMotion = useReducedMotion();

  // Detect variant from error if not explicitly provided
  const effectiveVariant = variant ?? detectVariant(error);

  // Extract retry after from error if not provided
  const effectiveRetryAfter = retryAfterSeconds ?? extractRetryAfter(error);

  // Countdown state for rate limit
  const [countdown, setCountdown] = useState(effectiveRetryAfter ?? 0);
  const [isRetrying, setIsRetrying] = useState(false);

  // Update countdown when retryAfterSeconds changes
  useEffect(() => {
    if (effectiveRetryAfter && effectiveRetryAfter > 0) {
      setCountdown(effectiveRetryAfter);
    }
  }, [effectiveRetryAfter]);

  // Countdown timer
  useEffect(() => {
    if (countdown <= 0) return;

    const timer = setInterval(() => {
      setCountdown(prev => Math.max(0, prev - 1));
    }, 1000);

    return () => clearInterval(timer);
  }, [countdown]);

  // Handle retry click
  const handleRetry = useCallback(async () => {
    if (!onRetry || countdown > 0) return;

    setIsRetrying(true);
    try {
      await onRetry();
    } finally {
      setIsRetrying(false);
    }
  }, [onRetry, countdown]);

  // Determine effective description
  const effectiveDescription =
    description ??
    // eslint-disable-next-line security/detect-object-injection -- effectiveVariant is typed union from ErrorVariant
    (error ? getErrorMessage(error) : VARIANT_DESCRIPTIONS[effectiveVariant]);

  // Get icon and colors - eslint warnings are false positives (effectiveVariant is typed union from ErrorVariant)
  // eslint-disable-next-line security/detect-object-injection
  const IconComponent = VARIANT_ICONS[effectiveVariant];
  // eslint-disable-next-line security/detect-object-injection
  const iconColorClass = VARIANT_ICON_COLORS[effectiveVariant];
  // eslint-disable-next-line security/detect-object-injection
  const bgColorClass = VARIANT_BG_COLORS[effectiveVariant];

  // Retry button disabled state
  const isRetryDisabled = countdown > 0 || isRetrying;

  return (
    <div
      role="alert"
      aria-live="assertive"
      data-testid={testId}
      data-variant={effectiveVariant}
      className={cn(
        'flex flex-col items-center justify-center',
        'py-8 px-6 text-center',
        'rounded-xl border',
        bgColorClass,
        'border-current/10',
        className
      )}
    >
      {/* Icon with animation */}
      <div
        className={cn(
          'mb-4 p-4 rounded-full',
          'bg-white/80 dark:bg-slate-800/80',
          'shadow-sm',
          !shouldReduceMotion && [
            'transition-transform duration-300',
            effectiveVariant === 'network' && 'animate-pulse',
          ]
        )}
        aria-hidden="true"
      >
        <IconComponent className={cn('w-8 h-8', iconColorClass)} strokeWidth={1.5} />
      </div>

      {/* Title */}
      <h3
        className={cn(
          'text-lg font-semibold',
          'text-slate-900 dark:text-slate-100',
          'font-quicksand',
          'mb-2'
        )}
      >
        {title}
      </h3>

      {/* Description */}
      <p className={cn('text-sm', 'text-slate-600 dark:text-slate-400', 'max-w-sm mb-4')}>
        {effectiveDescription}
      </p>

      {/* Countdown display for rate limit */}
      {effectiveVariant === 'rateLimit' && countdown > 0 && (
        <div
          className={cn(
            'mb-4 px-4 py-2 rounded-lg',
            'bg-purple-100 dark:bg-purple-900/50',
            'text-purple-700 dark:text-purple-300',
            'font-mono text-sm font-medium'
          )}
          aria-live="polite"
          aria-atomic="true"
        >
          Attendi {formatCountdown(countdown)}
        </div>
      )}

      {/* Retry Button */}
      {showRetry && onRetry && (
        <Button
          type="button"
          variant="default"
          size="default"
          onClick={handleRetry}
          disabled={isRetryDisabled}
          className={cn(
            'gap-2',
            'bg-primary hover:bg-primary/90',
            'text-primary-foreground',
            !shouldReduceMotion && 'transition-all duration-200',
            !isRetryDisabled && !shouldReduceMotion && 'hover:shadow-md active:scale-95'
          )}
          aria-label={
            countdown > 0
              ? `Riprova tra ${formatCountdown(countdown)}`
              : isRetrying
                ? 'Nuovo tentativo in corso...'
                : retryLabel
          }
        >
          <RefreshCw className={cn('h-4 w-4', isRetrying && 'animate-spin')} aria-hidden="true" />
          <span>
            {countdown > 0
              ? `Attendi ${formatCountdown(countdown)}`
              : isRetrying
                ? 'Riprovo...'
                : retryLabel}
          </span>
        </Button>
      )}

      {/* Correlation ID for debugging (only if error has it) */}
      {error instanceof ApiError && error.correlationId && (
        <p className={cn('mt-4 text-xs', 'text-slate-400 dark:text-slate-500', 'font-mono')}>
          ID: {error.correlationId}
        </p>
      )}

      {/* Screen reader text */}
      <span className="sr-only">
        Errore: {title}. {effectiveDescription}
        {onRetry && countdown <= 0 && ` Premi il pulsante per riprovare.`}
        {countdown > 0 && ` Attendi ${formatCountdown(countdown)} prima di riprovare.`}
      </span>
    </div>
  );
}

export default ErrorDisplay;

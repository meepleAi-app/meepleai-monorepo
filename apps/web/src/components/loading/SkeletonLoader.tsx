/**
 * SkeletonLoader Component
 *
 * Loading placeholder with variant-specific dimensions matching real content.
 * Uses CSS-based animation (Tailwind animate-pulse) for performance.
 * Respects user's reduced motion preferences.
 *
 * @example
 * ```tsx
 * // Games grid
 * <SkeletonLoader variant="games" count={6} />
 *
 * // Agents list
 * <SkeletonLoader variant="agents" count={3} />
 *
 * // Chat messages
 * <SkeletonLoader variant="message" count={2} />
 *
 * // Chat history sidebar
 * <SkeletonLoader variant="chatHistory" count={10} />
 * ```
 */

import { useReducedMotion } from '@/lib/animations';

export interface SkeletonLoaderProps {
  /**
   * Visual variant matching the content type
   */
  variant: 'games' | 'agents' | 'message' | 'chatHistory';

  /**
   * Number of skeleton placeholders to render
   * @default 1
   */
  count?: number;

  /**
   * Additional CSS classes
   */
  className?: string;

  /**
   * Enable animation (respects prefers-reduced-motion)
   * @default true
   */
  animate?: boolean;

  /**
   * Accessible label for screen readers
   * @default 'Loading...'
   */
  ariaLabel?: string;
}

/**
 * Variant-specific styles matching real content dimensions
 */
const VARIANT_STYLES = {
  games: 'h-64 rounded-lg', // Card layout: image + title + description
  agents: 'h-20 rounded-md', // List item: icon + name + description
  message: 'h-16 rounded-xl', // Chat message: avatar + text
  chatHistory: 'h-12 rounded-md', // Compact list: title + timestamp
} as const;

/**
 * SkeletonLoader component for loading states
 */
export function SkeletonLoader({
  variant,
  count = 1,
  className = '',
  animate = true,
  ariaLabel = 'Loading...',
}: SkeletonLoaderProps) {
  const shouldReduceMotion = useReducedMotion();
  const shouldAnimate = animate && !shouldReduceMotion;

  // Generate array for rendering multiple skeletons
  const skeletons = Array.from({ length: count }, (_, index) => index);

  return (
    <div className="space-y-4">
      {skeletons.map((index) => (
        <div
          key={index}
          role="status"
          aria-live="polite"
          aria-label={ariaLabel}
          className={`
            ${VARIANT_STYLES[variant]}
            ${shouldAnimate ? 'animate-pulse' : ''}
            bg-slate-200 dark:bg-slate-700
            ${className}
          `}
        >
          {/* Screen reader text */}
          <span className="sr-only">{ariaLabel}</span>

          {/* Variant-specific content structure */}
          {variant === 'games' && (
            <div className="h-full flex flex-col p-4 space-y-3">
              {/* Image placeholder */}
              <div className="flex-1 bg-slate-300 dark:bg-slate-600 rounded" />
              {/* Title placeholder */}
              <div className="h-4 bg-slate-300 dark:bg-slate-600 rounded w-3/4" />
              {/* Description placeholder */}
              <div className="h-3 bg-slate-300 dark:bg-slate-600 rounded w-full" />
              <div className="h-3 bg-slate-300 dark:bg-slate-600 rounded w-5/6" />
            </div>
          )}

          {variant === 'agents' && (
            <div className="h-full flex items-center p-3 space-x-3">
              {/* Icon placeholder */}
              <div className="w-12 h-12 bg-slate-300 dark:bg-slate-600 rounded-full flex-shrink-0" />
              {/* Text content */}
              <div className="flex-1 space-y-2">
                <div className="h-4 bg-slate-300 dark:bg-slate-600 rounded w-1/2" />
                <div className="h-3 bg-slate-300 dark:bg-slate-600 rounded w-3/4" />
              </div>
            </div>
          )}

          {variant === 'message' && (
            <div className="h-full flex items-start p-3 space-x-3">
              {/* Avatar placeholder */}
              <div className="w-10 h-10 bg-slate-300 dark:bg-slate-600 rounded-full flex-shrink-0" />
              {/* Message content */}
              <div className="flex-1 space-y-2 pt-1">
                <div className="h-3 bg-slate-300 dark:bg-slate-600 rounded w-full" />
                <div className="h-3 bg-slate-300 dark:bg-slate-600 rounded w-4/5" />
              </div>
            </div>
          )}

          {variant === 'chatHistory' && (
            <div className="h-full flex items-center p-3 space-x-2">
              {/* Icon placeholder */}
              <div className="w-6 h-6 bg-slate-300 dark:bg-slate-600 rounded flex-shrink-0" />
              {/* Title and timestamp */}
              <div className="flex-1 space-y-1">
                <div className="h-3 bg-slate-300 dark:bg-slate-600 rounded w-3/4" />
                <div className="h-2 bg-slate-300 dark:bg-slate-600 rounded w-1/3" />
              </div>
            </div>
          )}
        </div>
      ))}
    </div>
  );
}

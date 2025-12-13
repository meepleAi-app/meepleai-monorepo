/* eslint-disable security/detect-object-injection -- Safe variant config Record access */
/**
 * EmptyState - Reusable empty state component for consistent UI
 *
 * Provides visual feedback when there is no data to display.
 * Supports multiple variants with appropriate icons and styling.
 * Follows WCAG accessibility guidelines with proper ARIA attributes.
 *
 * @example
 * ```tsx
 * // Basic usage
 * <EmptyState title="No items" description="Add your first item to get started" />
 *
 * // With action button
 * <EmptyState
 *   title="No games found"
 *   description="Try adjusting your search"
 *   variant="noResults"
 *   action={{ label: "Clear filters", onClick: handleClear }}
 * />
 *
 * // Custom icon
 * <EmptyState
 *   title="Upload files"
 *   description="Drag and drop files here"
 *   icon={UploadIcon}
 * />
 * ```
 */

import { type LucideIcon, Inbox, Search, Lock, AlertCircle, FileQuestion } from 'lucide-react';
import { cn } from '@/lib/utils';
import { useReducedMotion } from '@/lib/animations';

export interface EmptyStateAction {
  /** Button label text */
  label: string;
  /** Click handler */
  onClick: () => void;
  /** Optional variant for button styling */
  variant?: 'primary' | 'secondary';
}

export interface EmptyStateProps {
  /**
   * Main title text (required)
   */
  title: string;

  /**
   * Optional description text providing additional context
   */
  description?: string;

  /**
   * Custom icon component from lucide-react
   * If not provided, uses variant-specific default icon
   */
  icon?: LucideIcon;

  /**
   * Visual variant determining default icon and styling
   * @default 'default'
   */
  variant?: 'default' | 'noData' | 'noResults' | 'noAccess' | 'error';

  /**
   * Optional action button
   */
  action?: EmptyStateAction;

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
const VARIANT_ICONS: Record<NonNullable<EmptyStateProps['variant']>, LucideIcon> = {
  default: Inbox,
  noData: FileQuestion,
  noResults: Search,
  noAccess: Lock,
  error: AlertCircle,
};

/**
 * Variant-specific icon colors
 */
const VARIANT_ICON_COLORS: Record<NonNullable<EmptyStateProps['variant']>, string> = {
  default: 'text-slate-400 dark:text-slate-500',
  noData: 'text-slate-400 dark:text-slate-500',
  noResults: 'text-blue-400 dark:text-blue-500',
  noAccess: 'text-yellow-400 dark:text-yellow-500',
  error: 'text-red-400 dark:text-red-500',
};

/**
 * EmptyState component for consistent empty state UI
 */
export function EmptyState({
  title,
  description,
  icon,
  variant = 'default',
  action,
  className,
  testId = 'empty-state',
}: EmptyStateProps) {
  const shouldReduceMotion = useReducedMotion();

  // Use custom icon or variant default
  const IconComponent = icon || VARIANT_ICONS[variant];
  const iconColorClass = VARIANT_ICON_COLORS[variant];

  return (
    <div
      role="status"
      aria-live="polite"
      data-testid={testId}
      className={cn(
        'flex flex-col items-center justify-center',
        'py-12 px-6 text-center',
        'rounded-lg',
        className
      )}
    >
      {/* Icon */}
      <div
        className={cn(
          'mb-4 p-4 rounded-full',
          'bg-slate-100 dark:bg-slate-800',
          !shouldReduceMotion && 'transition-transform duration-200 hover:scale-105'
        )}
        aria-hidden="true"
      >
        <IconComponent className={cn('w-8 h-8', iconColorClass)} strokeWidth={1.5} />
      </div>

      {/* Title */}
      <h3 className={cn('text-lg font-semibold', 'text-slate-900 dark:text-slate-100', 'mb-2')}>
        {title}
      </h3>

      {/* Description */}
      {description && (
        <p className={cn('text-sm', 'text-slate-600 dark:text-slate-400', 'max-w-sm mb-4')}>
          {description}
        </p>
      )}

      {/* Action Button */}
      {action && (
        <button
          type="button"
          onClick={action.onClick}
          className={cn(
            'mt-2 px-4 py-2 rounded-md',
            'font-medium text-sm',
            'focus:outline-none focus:ring-2 focus:ring-offset-2',
            !shouldReduceMotion && 'transition-colors duration-200',
            action.variant === 'secondary'
              ? [
                  'bg-slate-100 dark:bg-slate-800',
                  'text-slate-700 dark:text-slate-300',
                  'hover:bg-slate-200 dark:hover:bg-slate-700',
                  'focus:ring-slate-500',
                ]
              : [
                  'bg-blue-600 dark:bg-blue-500',
                  'text-white',
                  'hover:bg-blue-700 dark:hover:bg-blue-600',
                  'focus:ring-blue-500',
                ]
          )}
        >
          {action.label}
        </button>
      )}

      {/* Screen reader text */}
      <span className="sr-only">
        {title}. {description || ''} {action ? `Action available: ${action.label}` : ''}
      </span>
    </div>
  );
}

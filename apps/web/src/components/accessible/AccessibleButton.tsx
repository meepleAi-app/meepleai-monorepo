/**
 * AccessibleButton Component (UI-05)
 *
 * A fully accessible button component following WCAG 2.1 AA standards.
 *
 * Features:
 * - Proper ARIA attributes
 * - Keyboard navigation (Enter, Space)
 * - Focus indicators (2px outline)
 * - Loading states with announcements
 * - Support for icon-only buttons
 * - High contrast support
 * - Multiple variants and sizes
 *
 * @example
 * ```tsx
 * // Primary button
 * <AccessibleButton variant="primary" onClick={handleClick}>
 *   Save Changes
 * </AccessibleButton>
 *
 * // Icon-only button (aria-label required)
 * <AccessibleButton
 *   variant="ghost"
 *   iconOnly
 *   aria-label="Close dialog"
 *   onClick={handleClose}
 * >
 *   ✕
 * </AccessibleButton>
 *
 * // Loading state
 * <AccessibleButton isLoading loadingText="Saving...">
 *   Save
 * </AccessibleButton>
 *
 * // Toggle button
 * <AccessibleButton
 *   isPressed={isPressed}
 *   onClick={() => setIsPressed(!isPressed)}
 *   aria-label="Toggle notifications"
 * >
 *   🔔
 * </AccessibleButton>
 * ```
 */

import { forwardRef, ButtonHTMLAttributes } from 'react';

export interface AccessibleButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  /**
   * Button visual variant
   * @default 'primary'
   */
  variant?: 'primary' | 'secondary' | 'danger' | 'ghost';

  /**
   * Button size
   * @default 'md'
   */
  size?: 'sm' | 'md' | 'lg';

  /**
   * Whether the button is icon-only (requires aria-label)
   * @default false
   */
  iconOnly?: boolean;

  /**
   * Loading state - disables button and shows loading indicator
   * @default false
   */
  isLoading?: boolean;

  /**
   * Text to announce when loading (via aria-live)
   * @default 'Loading...'
   */
  loadingText?: string;

  /**
   * For toggle buttons - indicates pressed state
   * Sets aria-pressed attribute
   */
  isPressed?: boolean;

  /**
   * Additional CSS classes
   */
  className?: string;

  /**
   * Content of the button
   */
  children: React.ReactNode;
}

/**
 * AccessibleButton component with full WCAG 2.1 AA compliance
 */
export const AccessibleButton = forwardRef<HTMLButtonElement, AccessibleButtonProps>(
  (
    {
      variant = 'primary',
      size = 'md',
      iconOnly = false,
      isLoading = false,
      loadingText = 'Loading...',
      isPressed,
      className = '',
      children,
      disabled,
      'aria-label': ariaLabel,
      ...props
    },
    ref
  ) => {
    // Validation: icon-only buttons must have aria-label
    if (iconOnly && !ariaLabel && process.env.NODE_ENV === 'development') {
      console.error(
        'AccessibleButton: Icon-only buttons must have an aria-label prop for screen reader accessibility'
      );
    }

    // Base classes (always applied)
    const baseClasses = [
      'inline-flex',
      'items-center',
      'justify-center',
      'gap-2',
      'font-medium',
      'rounded-lg',
      'transition-all',
      'duration-200',
      'cursor-pointer',
      // Focus indicator (WCAG 2.1 AA)
      'focus-visible:outline-none',
      'focus-visible:ring-2',
      'focus-visible:ring-offset-2',
      'focus-visible:ring-primary-500',
      // Disabled state
      'disabled:cursor-not-allowed',
      'disabled:opacity-50',
    ];

    // Variant classes
    const variantClasses = {
      primary: [
        'bg-primary-600',
        'text-white',
        'hover:bg-primary-700',
        'active:bg-primary-800',
        'disabled:hover:bg-primary-600',
      ],
      secondary: [
        'bg-slate-200',
        'text-slate-900',
        'hover:bg-slate-300',
        'active:bg-slate-400',
        'dark:bg-slate-700',
        'dark:text-white',
        'dark:hover:bg-slate-600',
        'disabled:hover:bg-slate-200',
        'dark:disabled:hover:bg-slate-700',
      ],
      danger: [
        'bg-red-600',
        'text-white',
        'hover:bg-red-700',
        'active:bg-red-800',
        'disabled:hover:bg-red-600',
      ],
      ghost: [
        'bg-transparent',
        'text-slate-700',
        'hover:bg-slate-100',
        'active:bg-slate-200',
        'dark:text-slate-200',
        'dark:hover:bg-slate-800',
        'dark:active:bg-slate-700',
        'disabled:hover:bg-transparent',
      ],
    };

    // Size classes
    const sizeClasses = {
      sm: iconOnly ? ['p-1', 'text-sm'] : ['px-3', 'py-1.5', 'text-sm'],
      md: iconOnly ? ['p-2', 'text-base'] : ['px-4', 'py-2', 'text-base'],
      lg: iconOnly ? ['p-3', 'text-lg'] : ['px-6', 'py-3', 'text-lg'],
    };

    // Pressed state (for toggle buttons)
    const pressedClasses = isPressed
      ? variant === 'primary'
        ? ['bg-primary-800', 'ring-2', 'ring-primary-500']
        : ['bg-slate-400', 'dark:bg-slate-600', 'ring-2', 'ring-slate-500']
      : [];

    // Combine all classes
    const buttonClasses = [
      ...baseClasses,
      ...variantClasses[variant],
      ...sizeClasses[size],
      ...pressedClasses,
      className,
    ].join(' ');

    // ARIA attributes
    const ariaAttributes: React.AriaAttributes = {
      'aria-label': ariaLabel,
      'aria-disabled': disabled || isLoading,
      'aria-pressed': typeof isPressed === 'boolean' ? isPressed : undefined,
      'aria-live': isLoading ? 'polite' : undefined,
      'aria-busy': isLoading || undefined,
    };

    return (
      <>
        <button
          ref={ref}
          className={buttonClasses}
          disabled={disabled || isLoading}
          {...ariaAttributes}
          {...props}
        >
          {isLoading ? (
            <>
              {/* Loading spinner (decorative) */}
              <span aria-hidden="true" className="animate-spin">
                ⏳
              </span>
              {/* Loading text (announced by screen reader via aria-live) */}
              <span>{loadingText}</span>
            </>
          ) : (
            children
          )}
        </button>

        {/* Screen reader announcement for loading state */}
        {isLoading && (
          <div className="sr-only" role="status" aria-live="polite" aria-atomic="true">
            {loadingText}
          </div>
        )}
      </>
    );
  }
);

AccessibleButton.displayName = 'AccessibleButton';

/**
 * LoadingButton Component
 *
 * Button component with integrated loading state and spinner.
 * Automatically disables and shows loading indicator when isLoading is true.
 *
 * @example
 * ```tsx
 * const [isSubmitting, setIsSubmitting] = useState(false);
 *
 * const handleSubmit = async () => {
 *   setIsSubmitting(true);
 *   await submitForm();
 *   setIsSubmitting(false);
 * };
 *
 * <LoadingButton
 *   isLoading={isSubmitting}
 *   loadingText="Submitting..."
 *   onClick={handleSubmit}
 *   className="btn-primary"
 * >
 *   Submit Form
 * </LoadingButton>
 * ```
 */

import { ButtonHTMLAttributes, ReactNode } from 'react';
import { Spinner } from './Spinner';

export interface LoadingButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  /**
   * Whether the button is in loading state
   * @default false
   */
  isLoading?: boolean;

  /**
   * Text to display when loading (replaces children)
   */
  loadingText?: string;

  /**
   * Size of the loading spinner
   * @default 'md'
   */
  spinnerSize?: 'sm' | 'md' | 'lg';

  /**
   * Button content
   */
  children: ReactNode;
}

/**
 * LoadingButton component with integrated loading state
 */
export function LoadingButton({
  isLoading = false,
  loadingText,
  spinnerSize = 'md',
  children,
  className = '',
  disabled,
  ...buttonProps
}: LoadingButtonProps) {
  // Determine button disabled state
  const isDisabled = disabled || isLoading;

  return (
    <button
      type="button"
      {...buttonProps}
      disabled={isDisabled}
      aria-busy={isLoading}
      aria-live="polite"
      className={`
        inline-flex items-center justify-center gap-2
        transition-opacity duration-200
        ${isLoading ? 'opacity-70 cursor-not-allowed' : ''}
        ${className}
      `}
    >
      {isLoading && <Spinner size={spinnerSize} />}
      {isLoading ? loadingText || null : children}
    </button>
  );
}

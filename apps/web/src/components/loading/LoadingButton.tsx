/**
 * LoadingButton Component - Migrated to shadcn/ui
 *
 * Button component with integrated loading state and spinner.
 * Automatically disables and shows loading indicator when isLoading is true.
 * Now uses shadcn Button internally.
 *
 * NOTE: Consider using AccessibleButton which has the same functionality
 * with better accessibility features.
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
 * >
 *   Submit Form
 * </LoadingButton>
 * ```
 */

import { ReactNode } from 'react';

import { Loader2 } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

export interface LoadingButtonProps extends React.ComponentPropsWithoutRef<typeof Button> {
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
   * Button content
   */
  children: ReactNode;
}

/**
 * LoadingButton component with integrated loading state
 * Now powered by shadcn/ui Button
 */
export function LoadingButton({
  isLoading = false,
  loadingText,
  children,
  className,
  disabled,
  type = 'button',
  ...buttonProps
}: LoadingButtonProps) {
  return (
    <Button
      {...buttonProps}
      type={type}
      disabled={disabled || isLoading}
      aria-busy={isLoading}
      aria-live="polite"
      className={cn(isLoading && 'opacity-70 cursor-not-allowed', className)}
    >
      {isLoading && <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />}
      {isLoading ? loadingText || null : children}
    </Button>
  );
}

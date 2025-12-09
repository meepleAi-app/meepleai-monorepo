/**
 * Input Component - Redesigned
 * Editorial Playful aesthetic with smooth focus transitions
 * Supports variants, icons, and validation states
 */

import * as React from 'react';
import { cva, type VariantProps } from 'class-variance-authority';
import { cn } from '@/lib/utils';

const inputVariants = cva(
  // Base styles
  'flex w-full transition-all duration-200 file:border-0 file:bg-transparent file:text-sm file:font-medium placeholder:text-[var(--text-tertiary)] focus-visible:outline-none disabled:cursor-not-allowed disabled:opacity-50',
  {
    variants: {
      variant: {
        default:
          'border border-[var(--border-primary)] bg-[var(--bg-elevated)] text-[var(--text-primary)] focus:border-[var(--color-primary-500)] focus:ring-2 focus:ring-[var(--color-primary-500)]/20',

        filled:
          'border-0 bg-[var(--bg-secondary)] text-[var(--text-primary)] focus:bg-[var(--bg-tertiary)] focus:ring-2 focus:ring-[var(--color-primary-500)]/20',

        ghost:
          'border-0 border-b-2 border-[var(--border-primary)] bg-transparent rounded-none text-[var(--text-primary)] focus:border-[var(--color-primary-500)]',
      },

      inputSize: {
        sm: 'h-8 px-3 text-[var(--font-size-sm)] rounded-[var(--radius-md)]',
        md: 'h-10 px-4 text-[var(--font-size-base)] rounded-[var(--radius-lg)]',
        lg: 'h-12 px-5 text-[var(--font-size-lg)] rounded-[var(--radius-lg)]',
      },

      state: {
        default: '',
        error:
          'border-[var(--color-red)] focus:border-[var(--color-red)] focus:ring-[var(--color-red)]/20',
        success:
          'border-[var(--color-green)] focus:border-[var(--color-green)] focus:ring-[var(--color-green)]/20',
        warning:
          'border-[var(--color-yellow)] focus:border-[var(--color-yellow)] focus:ring-[var(--color-yellow)]/20',
      },
    },
    defaultVariants: {
      variant: 'default',
      inputSize: 'md',
      state: 'default',
    },
  }
);

export interface InputProps
  extends
    Omit<React.InputHTMLAttributes<HTMLInputElement>, 'size'>,
    VariantProps<typeof inputVariants> {
  leftIcon?: React.ReactNode;
  rightIcon?: React.ReactNode;
  error?: string;
  helperText?: string;
}

const Input = React.forwardRef<HTMLInputElement, InputProps>(
  (
    {
      className,
      variant,
      inputSize,
      state,
      type = 'text',
      leftIcon,
      rightIcon,
      error,
      helperText,
      ...props
    },
    ref
  ) => {
    const hasError = !!error;
    const finalState = hasError ? 'error' : state;

    return (
      <div className="input-wrapper w-full">
        <div className="relative flex items-center">
          {leftIcon && (
            <span className="absolute left-3 text-[var(--text-tertiary)] pointer-events-none">
              {leftIcon}
            </span>
          )}

          <input
            type={type}
            className={cn(
              inputVariants({ variant, inputSize, state: finalState, className }),
              leftIcon && 'pl-10',
              rightIcon && 'pr-10'
            )}
            ref={ref}
            {...props}
          />

          {rightIcon && (
            <span className="absolute right-3 text-[var(--text-tertiary)] pointer-events-none">
              {rightIcon}
            </span>
          )}
        </div>

        {(error || helperText) && (
          <p
            className={cn(
              'mt-1.5 text-[var(--font-size-sm)]',
              hasError ? 'text-[var(--color-red)]' : 'text-[var(--text-tertiary)]'
            )}
          >
            {error || helperText}
          </p>
        )}
      </div>
    );
  }
);

Input.displayName = 'Input';

export { Input, inputVariants };

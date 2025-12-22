/**
 * Button Component - Redesigned
 * Editorial Playful aesthetic with game-inspired interactions
 * Uses design system tokens for consistency
 */

import * as React from 'react';

import { Slot } from '@radix-ui/react-slot';
import { cva, type VariantProps } from 'class-variance-authority';

import { cn } from '@/lib/utils';

const buttonVariants = cva(
  // Base styles with design tokens
  'inline-flex items-center justify-center gap-2 whitespace-nowrap font-medium transition-all focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-offset-2 disabled:pointer-events-none disabled:opacity-50 [&_svg]:pointer-events-none [&_svg]:size-4 [&_svg]:shrink-0',
  {
    variants: {
      variant: {
        // Primary - Meeple Purple with playful hover
        primary:
          'bg-[var(--color-primary-500)] text-[var(--text-inverse)] shadow-md hover:bg-[var(--color-primary-600)] hover:-translate-y-0.5 hover:shadow-lg active:translate-y-0',

        // Secondary - Game Table Amber
        secondary:
          'bg-[var(--color-secondary-500)] text-[var(--text-inverse)] shadow-md hover:bg-[var(--color-secondary-600)] hover:-translate-y-0.5 hover:shadow-lg active:translate-y-0',

        // Outline - Border with subtle hover
        outline:
          'border border-[var(--border-primary)] bg-transparent text-[var(--text-primary)] hover:bg-[var(--bg-secondary)] hover:border-[var(--border-secondary)]',

        // Ghost - Transparent with hover background
        ghost:
          'bg-transparent text-[var(--text-secondary)] hover:bg-[var(--bg-secondary)] hover:text-[var(--text-primary)]',

        // Danger - Red for destructive actions
        danger:
          'bg-[var(--color-red)] text-[var(--text-inverse)] shadow-md hover:bg-red-600 hover:-translate-y-0.5 hover:shadow-lg active:translate-y-0',

        // Link - Text only with underline
        link: 'text-[var(--color-primary-500)] underline-offset-4 hover:underline',
      },
      size: {
        sm: 'h-8 px-3 text-[var(--font-size-sm)] rounded-[var(--radius-md)]',
        md: 'h-10 px-4 text-[var(--font-size-base)] rounded-[var(--radius-lg)]',
        lg: 'h-12 px-6 text-[var(--font-size-lg)] rounded-[var(--radius-lg)]',
        xl: 'h-14 px-8 text-[var(--font-size-xl)] rounded-[var(--radius-xl)]',
        icon: 'h-10 w-10 rounded-[var(--radius-md)]',
      },
      playful: {
        true: 'hover:scale-105 active:scale-95',
        false: '',
      },
    },
    defaultVariants: {
      variant: 'primary',
      size: 'md',
      playful: true,
    },
  }
);

export interface ButtonProps
  extends React.ButtonHTMLAttributes<HTMLButtonElement>, VariantProps<typeof buttonVariants> {
  asChild?: boolean;
  loading?: boolean;
  leftIcon?: React.ReactNode;
  rightIcon?: React.ReactNode;
}

const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(
  (
    {
      className,
      variant,
      size,
      playful,
      asChild = false,
      loading = false,
      leftIcon,
      rightIcon,
      children,
      disabled,
      ...props
    },
    ref
  ) => {
    const Comp = asChild ? Slot : 'button';

    return (
      <Comp
        className={cn(buttonVariants({ variant, size, playful, className }))}
        ref={ref}
        disabled={disabled || loading}
        {...props}
      >
        {loading && (
          <svg
            className="animate-spin h-4 w-4"
            xmlns="http://www.w3.org/2000/svg"
            fill="none"
            viewBox="0 0 24 24"
          >
            <circle
              className="opacity-25"
              cx="12"
              cy="12"
              r="10"
              stroke="currentColor"
              strokeWidth="4"
            />
            <path
              className="opacity-75"
              fill="currentColor"
              d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
            />
          </svg>
        )}
        {!loading && leftIcon && <span className="button-icon-left">{leftIcon}</span>}
        {children}
        {!loading && rightIcon && <span className="button-icon-right">{rightIcon}</span>}
      </Comp>
    );
  }
);

Button.displayName = 'Button';

export { Button, buttonVariants };

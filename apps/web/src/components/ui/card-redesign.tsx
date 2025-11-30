/**
 * Card Component - Redesigned
 * Editorial Playful aesthetic with hover effects
 * Multiple variants for different use cases
 */

import * as React from 'react';
import { cva, type VariantProps } from 'class-variance-authority';
import { cn } from '@/lib/utils';

const cardVariants = cva(
  // Base styles
  'rounded-[var(--radius-xl)] transition-all duration-200',
  {
    variants: {
      variant: {
        // Default - Elevated with border
        default:
          'bg-[var(--bg-elevated)] border border-[var(--border-primary)] shadow-[var(--shadow-md)]',

        // Filled - Background color, no border
        filled: 'bg-[var(--bg-secondary)] shadow-none',

        // Outlined - Border only, transparent background
        outlined: 'bg-transparent border border-[var(--border-primary)] shadow-none',

        // Interactive - Hover lift effect
        interactive:
          'bg-[var(--bg-elevated)] border border-[var(--border-primary)] shadow-[var(--shadow-md)] cursor-pointer hover:-translate-y-1 hover:shadow-[var(--shadow-xl)] hover:border-[var(--color-primary-300)] active:translate-y-0',

        // Gradient - Subtle gradient background
        gradient:
          'bg-gradient-to-br from-[var(--bg-elevated)] to-[var(--bg-secondary)] border border-[var(--border-primary)] shadow-[var(--shadow-lg)]',
      },

      padding: {
        none: 'p-0',
        sm: 'p-4',
        md: 'p-6',
        lg: 'p-8',
      },
    },
    defaultVariants: {
      variant: 'default',
      padding: 'md',
    },
  }
);

export interface CardProps
  extends React.HTMLAttributes<HTMLDivElement>,
    VariantProps<typeof cardVariants> {
  asChild?: boolean;
}

const Card = React.forwardRef<HTMLDivElement, CardProps>(
  ({ className, variant, padding, ...props }, ref) => (
    <div ref={ref} className={cn(cardVariants({ variant, padding, className }))} {...props} />
  )
);
Card.displayName = 'Card';

const CardHeader = React.forwardRef<HTMLDivElement, React.HTMLAttributes<HTMLDivElement>>(
  ({ className, ...props }, ref) => (
    <div ref={ref} className={cn('flex flex-col space-y-1.5 mb-4', className)} {...props} />
  )
);
CardHeader.displayName = 'CardHeader';

const CardTitle = React.forwardRef<HTMLHeadingElement, React.HTMLAttributes<HTMLHeadingElement>>(
  ({ className, ...props }, ref) => (
    <h3
      ref={ref}
      className={cn(
        'font-[var(--font-display)] text-[var(--font-size-xl)] font-bold text-[var(--text-primary)] leading-tight tracking-tight',
        className
      )}
      {...props}
    />
  )
);
CardTitle.displayName = 'CardTitle';

const CardDescription = React.forwardRef<
  HTMLParagraphElement,
  React.HTMLAttributes<HTMLParagraphElement>
>(({ className, ...props }, ref) => (
  <p
    ref={ref}
    className={cn('text-[var(--font-size-sm)] text-[var(--text-secondary)]', className)}
    {...props}
  />
));
CardDescription.displayName = 'CardDescription';

const CardContent = React.forwardRef<HTMLDivElement, React.HTMLAttributes<HTMLDivElement>>(
  ({ className, ...props }, ref) => (
    <div ref={ref} className={cn('text-[var(--text-primary)]', className)} {...props} />
  )
);
CardContent.displayName = 'CardContent';

const CardFooter = React.forwardRef<HTMLDivElement, React.HTMLAttributes<HTMLDivElement>>(
  ({ className, ...props }, ref) => (
    <div
      ref={ref}
      className={cn(
        'flex items-center gap-2 mt-4 pt-4 border-t border-[var(--border-primary)]',
        className
      )}
      {...props}
    />
  )
);
CardFooter.displayName = 'CardFooter';

export { Card, CardHeader, CardFooter, CardTitle, CardDescription, CardContent, cardVariants };

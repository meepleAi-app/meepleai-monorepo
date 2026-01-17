/**
 * Spinner Component
 *
 * Loading spinner indicator with size variants.
 * Built on lucide-react Loader2 icon.
 */

'use client';

import * as React from 'react';
import { Loader2 } from 'lucide-react';
import { cva, type VariantProps } from 'class-variance-authority';

import { cn } from '@/lib/utils';

const spinnerVariants = cva('animate-spin', {
  variants: {
    size: {
      sm: 'h-4 w-4',
      md: 'h-6 w-6',
      lg: 'h-8 w-8',
      xl: 'h-12 w-12',
    },
  },
  defaultVariants: {
    size: 'md',
  },
});

export interface SpinnerProps
  extends React.ComponentPropsWithoutRef<'svg'>,
    VariantProps<typeof spinnerVariants> {
  /**
   * Optional text to display alongside spinner
   */
  label?: string;
}

const Spinner = React.forwardRef<SVGSVGElement, SpinnerProps>(
  ({ className, size, label, ...props }, ref) => {
    return (
      <div className="flex items-center gap-2">
        <Loader2
          ref={ref}
          className={cn(spinnerVariants({ size, className }))}
          {...props}
        />
        {label && <span className="text-sm text-muted-foreground">{label}</span>}
      </div>
    );
  }
);
Spinner.displayName = 'Spinner';

export { Spinner, spinnerVariants };

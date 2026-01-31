import * as React from 'react';

import { cn } from '@/lib/utils';

const Input = React.forwardRef<HTMLInputElement, React.ComponentProps<'input'>>(
  ({ className, type, ...props }, ref) => {
    return (
      <input
        type={type}
        className={cn(
          'flex h-11 w-full rounded-lg border border-border',
          // Light mode: Glass morphism
          'bg-card/90 backdrop-blur-[8px]',
          // Dark mode: Solid professional
          'dark:bg-card dark:backdrop-blur-none dark:border-border/70',
          'px-3.5 py-2.5 text-base shadow-sm transition-all duration-200',
          'placeholder:text-muted-foreground',
          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/70 focus-visible:border-transparent',
          // Dark mode enhanced focus ring (amber)
          'dark:focus-visible:ring-accent/70',
          'disabled:cursor-not-allowed disabled:opacity-50',
          className
        )}
        ref={ref}
        {...props}
      />
    );
  }
);
Input.displayName = 'Input';

export { Input };

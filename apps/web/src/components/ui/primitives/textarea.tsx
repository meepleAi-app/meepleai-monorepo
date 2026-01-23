import * as React from 'react';

import { cn } from '@/lib/utils';

const Textarea = React.forwardRef<HTMLTextAreaElement, React.ComponentProps<'textarea'>>(
  ({ className, ...props }, ref) => {
    return (
      <textarea
        className={cn(
          'flex min-h-[60px] w-full rounded-lg border border-border',
          // Light mode: Glass morphism
          'bg-card/90 backdrop-blur-[8px]',
          // Dark mode: Solid professional
          'dark:bg-card dark:backdrop-blur-none dark:border-border/70',
          'px-3 py-2 text-base shadow-sm md:text-sm',
          'placeholder:text-muted-foreground',
          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/70',
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
Textarea.displayName = 'Textarea';

export { Textarea };

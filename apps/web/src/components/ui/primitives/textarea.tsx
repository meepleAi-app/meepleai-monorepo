import * as React from 'react';

import { cn } from '@/lib/utils';

const Textarea = React.forwardRef<HTMLTextAreaElement, React.ComponentProps<'textarea'>>(
  ({ className, ...props }, ref) => {
    return (
      <textarea
        className={cn(
          // #4570: a form control's border is --input, >= 3:1 (WCAG 1.4.11); border-border is the decorative one.
          'flex min-h-[60px] w-full rounded-lg border border-input',
          // Light mode: Glass morphism
          'bg-card/90 backdrop-blur-[8px]',
          // Dark mode: Solid professional
          'dark:bg-card dark:backdrop-blur-none',
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

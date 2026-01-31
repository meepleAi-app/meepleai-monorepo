import * as React from 'react';

import { Slot } from '@radix-ui/react-slot';
import { cva, type VariantProps } from 'class-variance-authority';

import { cn } from '@/lib/utils';

const buttonVariants = cva(
  'inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-lg font-quicksand font-semibold text-base transition-all duration-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:pointer-events-none disabled:opacity-50 shadow-sm hover:-translate-y-0.5 hover:shadow-md active:translate-y-0 motion-reduce:transform-none [&_svg]:pointer-events-none [&_svg]:size-4 [&_svg]:shrink-0 motion-safe:hover:animate-bounce-subtle',
  {
    variants: {
      variant: {
        default: 'bg-primary text-primary-foreground hover:bg-primary/90 dark:bg-primary/90 dark:hover:bg-primary',
        destructive: 'bg-destructive text-destructive-foreground shadow-sm hover:bg-destructive/90 dark:bg-destructive/90 dark:hover:bg-destructive',
        outline: 'border border-border/50 dark:border-border/70 bg-card/90 backdrop-blur-[8px] dark:bg-card dark:backdrop-blur-none text-foreground shadow-sm hover:bg-muted',
        secondary: 'bg-secondary text-secondary-foreground shadow-sm hover:bg-secondary/85 dark:bg-secondary/90 dark:hover:bg-secondary',
        ghost: 'text-foreground hover:bg-muted dark:hover:bg-muted/70',
        link: 'text-primary underline underline-offset-4 hover:text-primary/80 dark:text-primary/90 dark:hover:text-primary',
      },
      size: {
        default: 'h-11 px-6',
        sm: 'h-9 rounded-md px-4 text-sm',
        lg: 'h-12 rounded-xl px-8 text-lg',
        icon: 'h-11 w-11',
      },
    },
    defaultVariants: {
      variant: 'default',
      size: 'default',
    },
  }
);

export interface ButtonProps
  extends React.ButtonHTMLAttributes<HTMLButtonElement>, VariantProps<typeof buttonVariants> {
  asChild?: boolean;
}

const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(
  ({ className, variant, size, asChild = false, ...props }, ref) => {
    const Comp = asChild ? Slot : 'button';
    return (
      <Comp className={cn(buttonVariants({ variant, size, className }))} ref={ref} {...props} />
    );
  }
);
Button.displayName = 'Button';

export { Button, buttonVariants };

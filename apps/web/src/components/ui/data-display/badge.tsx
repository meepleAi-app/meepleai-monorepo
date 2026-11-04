import * as React from 'react';

import { cva, type VariantProps } from 'class-variance-authority';

import { cn } from '@/lib/utils';

/**
 * Opacità del fondo delle varianti piene (#4539): 100%, 80% al passaggio del mouse, 90% in
 * tema scuro, 100% al passaggio in tema scuro.
 *
 * Il fondo è una sola classe senza modificatori, `bg-<token>/(--badge-fill)`, e i modificatori
 * cambiano solo la variabile. Se il consumatore passa un suo `bg-*`, tailwind-merge toglie il
 * fondo della variante e non resta nessun `dark:bg-*` o `hover:bg-*` a batterlo. Prima erano
 * classi di fondo (`dark:bg-primary/90`, `hover:bg-primary/80`, …): tailwind-merge le teneva,
 * perché per lui un modificatore diverso è un altro gruppo, e in CSS vincevano per specificità.
 */
const FILL_OPACITY =
  '[--badge-fill:100%] hover:[--badge-fill:80%] dark:[--badge-fill:90%] dark:hover:[--badge-fill:100%]';

const badgeVariants = cva(
  'inline-flex items-center rounded-md border px-2.5 py-0.5 text-xs font-semibold transition-colors focus:outline-none focus:ring-2 focus:ring-ring focus:ring-offset-2 dark:focus:ring-accent',
  {
    variants: {
      variant: {
        default: `border-transparent bg-primary/(--badge-fill) text-primary-foreground shadow ${FILL_OPACITY}`,
        secondary: `border-transparent bg-secondary/(--badge-fill) text-secondary-foreground ${FILL_OPACITY}`,
        destructive: `border-transparent bg-destructive/(--badge-fill) text-destructive-foreground shadow ${FILL_OPACITY}`,
        outline: 'text-foreground border-border/50 dark:border-border/70',
      },
    },
    defaultVariants: {
      variant: 'default',
    },
  }
);

export interface BadgeProps
  extends React.HTMLAttributes<HTMLDivElement>, VariantProps<typeof badgeVariants> {}

function Badge({ className, variant, ...props }: BadgeProps) {
  return <div className={cn(badgeVariants({ variant }), className)} {...props} />;
}

export { Badge, badgeVariants };

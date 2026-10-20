import * as React from 'react';

import { Slot } from '@radix-ui/react-slot';
import { cva, type VariantProps } from 'class-variance-authority';

import { cn } from '@/lib/utils';

/*
 * Colori delle varianti (#4548). Il fondo di ogni variante è una sola classe senza modificatori,
 * `bg-<token>/(--btn-fill)`, e `hover:`, `dark:` e `dark:hover:` cambiano solo la variabile; lo
 * stesso vale per il testo di `link` con `--btn-ink`. Se il consumatore passa un suo `bg-*` (o,
 * su `link`, un suo `text-*`), tailwind-merge toglie quello della variante e non resta nessuna
 * classe col modificatore a batterlo, né in tema scuro né al passaggio del mouse.
 *
 * Prima erano classi di colore (`dark:bg-primary/90`, `hover:bg-primary/90`,
 * `dark:hover:bg-primary`, `dark:bg-card`, `dark:hover:bg-muted/70`, `dark:text-primary/90`, …):
 * tailwind-merge le teneva, perché per lui un modificatore diverso è un altro gruppo, e in CSS
 * vincevano per specificità. Chi non passa un colore riceve lo stesso CSS calcolato di prima.
 */

/** 100%, 90% al passaggio del mouse, 90% in tema scuro, 100% al passaggio in tema scuro. */
const FILL_90 =
  '[--btn-fill:100%] hover:[--btn-fill:90%] dark:[--btn-fill:90%] dark:hover:[--btn-fill:100%]';

/** Come FILL_90, ma 85% al passaggio del mouse in tema chiaro. */
const FILL_85 =
  '[--btn-fill:100%] hover:[--btn-fill:85%] dark:[--btn-fill:90%] dark:hover:[--btn-fill:100%]';

/**
 * `outline` cambia anche token: `card` al 90%, `muted` al passaggio del mouse; in tema scuro
 * `card` pieno, anche al passaggio del mouse (prima `dark:bg-card` batteva `hover:bg-muted`).
 */
const OUTLINE_FILL =
  'bg-(--btn-tone)/(--btn-fill) [--btn-tone:hsl(var(--card))] [--btn-fill:90%] hover:[--btn-tone:hsl(var(--muted))] hover:[--btn-fill:100%] dark:[--btn-fill:100%] dark:hover:[--btn-tone:hsl(var(--card))]';

/**
 * `ghost` non ha fondo a riposo (`transparent`); al passaggio `muted` pieno, al 70% in tema
 * scuro. Il tono `transparent`, e non `muted` allo 0%, tiene giusto anche il ripiego senza
 * `color-mix`, che usa il tono pieno.
 */
const GHOST_FILL =
  'bg-(--btn-tone)/(--btn-fill) [--btn-tone:transparent] [--btn-fill:100%] hover:[--btn-tone:hsl(var(--muted))] dark:hover:[--btn-fill:70%]';

/** Il testo di `link`: 100%, 80% al passaggio, 90% in tema scuro, 100% al passaggio in scuro. */
const LINK_INK =
  'text-primary/(--btn-ink) [--btn-ink:100%] hover:[--btn-ink:80%] dark:[--btn-ink:90%] dark:hover:[--btn-ink:100%]';

const buttonVariants = cva(
  'inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-lg font-quicksand font-semibold text-base transition-all duration-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:pointer-events-none disabled:opacity-50 shadow-sm hover:-translate-y-0.5 hover:shadow-md active:translate-y-0 motion-reduce:transform-none [&_svg]:pointer-events-none [&_svg]:size-4 [&_svg]:shrink-0 motion-safe:hover:animate-bounce-subtle',
  {
    variants: {
      variant: {
        default: `bg-primary/(--btn-fill) text-primary-foreground ${FILL_90}`,
        destructive: `bg-destructive/(--btn-fill) text-destructive-foreground shadow-sm ${FILL_90}`,
        outline: `border border-border/50 dark:border-border/70 ${OUTLINE_FILL} backdrop-blur-[8px] dark:backdrop-blur-none text-foreground shadow-sm`,
        secondary: `bg-secondary/(--btn-fill) text-secondary-foreground shadow-sm ${FILL_85}`,
        ghost: `text-foreground ${GHOST_FILL}`,
        link: `${LINK_INK} underline underline-offset-4`,
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

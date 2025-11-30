/**
 * Tabs Component - Redesigned
 * Multiple variants: underline, pills, cards
 * Smooth transitions and playful interactions
 */

'use client';

import * as React from 'react';
import * as TabsPrimitive from '@radix-ui/react-tabs';
import { cva, type VariantProps } from 'class-variance-authority';
import { cn } from '@/lib/utils';

const tabsListVariants = cva('inline-flex items-center gap-1', {
  variants: {
    variant: {
      // Underline - Border bottom with active indicator
      underline: 'border-b border-[var(--border-primary)] gap-0',

      // Pills - Rounded pill-shaped tabs with background
      pills: 'bg-[var(--bg-secondary)] p-1 rounded-[var(--radius-full)]',

      // Cards - Card-like tabs with shadow
      cards: 'gap-2',
    },
  },
  defaultVariants: {
    variant: 'underline',
  },
});

const tabsTriggerVariants = cva(
  'inline-flex items-center justify-center whitespace-nowrap font-medium transition-all focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-primary-500)] focus-visible:ring-offset-2 disabled:pointer-events-none disabled:opacity-50',
  {
    variants: {
      variant: {
        underline:
          'px-4 py-2 text-[var(--font-size-base)] text-[var(--text-secondary)] border-b-2 border-transparent hover:text-[var(--text-primary)] data-[state=active]:text-[var(--color-primary-500)] data-[state=active]:border-[var(--color-primary-500)]',

        pills:
          'px-4 py-2 text-[var(--font-size-sm)] text-[var(--text-secondary)] rounded-[var(--radius-full)] hover:bg-[var(--bg-tertiary)] hover:text-[var(--text-primary)] data-[state=active]:bg-[var(--color-primary-500)] data-[state=active]:text-[var(--text-inverse)] data-[state=active]:shadow-[var(--shadow-md)]',

        cards:
          'px-5 py-3 text-[var(--font-size-base)] text-[var(--text-secondary)] bg-[var(--bg-elevated)] border border-[var(--border-primary)] rounded-[var(--radius-lg)] hover:border-[var(--border-secondary)] hover:text-[var(--text-primary)] data-[state=active]:bg-[var(--color-primary-500)] data-[state=active]:text-[var(--text-inverse)] data-[state=active]:border-[var(--color-primary-500)] data-[state=active]:shadow-[var(--shadow-lg)]',
      },
    },
    defaultVariants: {
      variant: 'underline',
    },
  }
);

const Tabs = TabsPrimitive.Root;

interface TabsListProps
  extends React.ComponentPropsWithoutRef<typeof TabsPrimitive.List>,
    VariantProps<typeof tabsListVariants> {}

const TabsList = React.forwardRef<React.ElementRef<typeof TabsPrimitive.List>, TabsListProps>(
  ({ className, variant, ...props }, ref) => (
    <TabsPrimitive.List
      ref={ref}
      className={cn(tabsListVariants({ variant, className }))}
      {...props}
    />
  )
);
TabsList.displayName = TabsPrimitive.List.displayName;

interface TabsTriggerProps
  extends React.ComponentPropsWithoutRef<typeof TabsPrimitive.Trigger>,
    VariantProps<typeof tabsTriggerVariants> {}

const TabsTrigger = React.forwardRef<
  React.ElementRef<typeof TabsPrimitive.Trigger>,
  TabsTriggerProps
>(({ className, variant, ...props }, ref) => (
  <TabsPrimitive.Trigger
    ref={ref}
    className={cn(tabsTriggerVariants({ variant, className }))}
    {...props}
  />
));
TabsTrigger.displayName = TabsPrimitive.Trigger.displayName;

const TabsContent = React.forwardRef<
  React.ElementRef<typeof TabsPrimitive.Content>,
  React.ComponentPropsWithoutRef<typeof TabsPrimitive.Content>
>(({ className, ...props }, ref) => (
  <TabsPrimitive.Content
    ref={ref}
    className={cn(
      'mt-4 ring-offset-background focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-primary-500)] focus-visible:ring-offset-2',
      'data-[state=active]:animate-in data-[state=active]:fade-in-0 data-[state=active]:zoom-in-95',
      className
    )}
    {...props}
  />
));
TabsContent.displayName = TabsPrimitive.Content.displayName;

export { Tabs, TabsList, TabsTrigger, TabsContent, tabsListVariants, tabsTriggerVariants };

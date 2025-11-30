/**
 * Grid Component - Redesigned
 * Responsive grid system with playful gaps
 * Supports auto-fit, asymmetric layouts, and masonry-style grids
 */

import * as React from 'react';
import { cva, type VariantProps } from 'class-variance-authority';
import { cn } from '@/lib/utils';

const gridVariants = cva('grid', {
  variants: {
    // Column count presets
    cols: {
      1: 'grid-cols-1',
      2: 'grid-cols-1 md:grid-cols-2',
      3: 'grid-cols-1 md:grid-cols-2 lg:grid-cols-3',
      4: 'grid-cols-1 md:grid-cols-2 lg:grid-cols-4',
      auto: '', // Custom via minWidth
    },

    // Gap sizes using design tokens
    gap: {
      sm: 'gap-[var(--space-2)]',
      md: 'gap-[var(--space-4)]',
      lg: 'gap-[var(--space-6)]',
      xl: 'gap-[var(--space-8)]',
    },

    // Asymmetric layouts (editorial style)
    layout: {
      default: '',
      sidebar: 'grid-cols-1 lg:grid-cols-[1.2fr_1fr]', // 60/40 split
      'sidebar-reverse': 'grid-cols-1 lg:grid-cols-[1fr_1.2fr]', // 40/60 split
      hero: 'grid-cols-1 lg:grid-cols-[2fr_1fr]', // 66/33 split
    },
  },
  defaultVariants: {
    cols: 3,
    gap: 'md',
    layout: 'default',
  },
});

export interface GridProps
  extends React.HTMLAttributes<HTMLDivElement>,
    VariantProps<typeof gridVariants> {
  /**
   * Minimum width for auto-fit columns (e.g., "300px")
   * Overrides cols when provided
   */
  minWidth?: string;

  /**
   * Align items vertically
   */
  alignItems?: 'start' | 'center' | 'end' | 'stretch';
}

const Grid = React.forwardRef<HTMLDivElement, GridProps>(
  ({ className, cols, gap, layout, minWidth, alignItems = 'stretch', style, ...props }, ref) => {
    // Build style object
    const gridStyle: React.CSSProperties = {
      ...style,
      alignItems,
    };

    // If minWidth is provided, use auto-fit
    if (minWidth) {
      gridStyle.gridTemplateColumns = `repeat(auto-fit, minmax(${minWidth}, 1fr))`;
    }

    return (
      <div
        ref={ref}
        className={cn(gridVariants({ cols: minWidth ? 'auto' : cols, gap, layout, className }))}
        style={gridStyle}
        {...props}
      />
    );
  }
);
Grid.displayName = 'Grid';

/**
 * GridItem - Optional wrapper for grid items with custom span
 */

interface GridItemProps extends React.HTMLAttributes<HTMLDivElement> {
  /**
   * Column span (1-12)
   */
  colSpan?: 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12;

  /**
   * Row span (1-6)
   */
  rowSpan?: 1 | 2 | 3 | 4 | 5 | 6;
}

const getColSpanClass = (span: number): string => {
  switch (span) {
    case 1:
      return 'col-span-1';
    case 2:
      return 'col-span-2';
    case 3:
      return 'col-span-3';
    case 4:
      return 'col-span-4';
    case 5:
      return 'col-span-5';
    case 6:
      return 'col-span-6';
    case 7:
      return 'col-span-7';
    case 8:
      return 'col-span-8';
    case 9:
      return 'col-span-9';
    case 10:
      return 'col-span-10';
    case 11:
      return 'col-span-11';
    case 12:
      return 'col-span-12';
    default:
      return '';
  }
};

const getRowSpanClass = (span: number): string => {
  switch (span) {
    case 1:
      return 'row-span-1';
    case 2:
      return 'row-span-2';
    case 3:
      return 'row-span-3';
    case 4:
      return 'row-span-4';
    case 5:
      return 'row-span-5';
    case 6:
      return 'row-span-6';
    default:
      return '';
  }
};

const GridItem = React.forwardRef<HTMLDivElement, GridItemProps>(
  ({ className, colSpan, rowSpan, ...props }, ref) => (
    <div
      ref={ref}
      className={cn(
        colSpan !== undefined && getColSpanClass(colSpan),
        rowSpan !== undefined && getRowSpanClass(rowSpan),
        className
      )}
      {...props}
    />
  )
);
GridItem.displayName = 'GridItem';

export { Grid, GridItem, gridVariants };

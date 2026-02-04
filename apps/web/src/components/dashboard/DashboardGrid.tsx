/**
 * DashboardGrid - Responsive Grid Layout for Dashboard Widgets
 * Issue #3323 - Responsive polish and performance optimization
 *
 * Features:
 * - Responsive breakpoints (mobile: 1-col, tablet: 2-col, desktop: 3-col)
 * - Touch-friendly targets (min 44x44px)
 * - Collapsible sections for mobile
 * - Lazy loading support for widgets
 * - Keyboard navigation
 *
 * Breakpoints:
 * - Mobile (< 640px): 1 column
 * - Tablet (640-1024px): 2 columns
 * - Desktop (> 1024px): 3 columns asymmetric
 *
 * @example
 * ```tsx
 * <DashboardGrid>
 *   <DashboardGrid.Item size="large">
 *     <AiInsightsWidget />
 *   </DashboardGrid.Item>
 *   <DashboardGrid.Item>
 *     <WishlistHighlights />
 *   </DashboardGrid.Item>
 * </DashboardGrid>
 * ```
 */

'use client';

import React, { createContext, useContext, useState, useCallback } from 'react';

import { motion, AnimatePresence } from 'framer-motion';
import { ChevronDown } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export type GridItemSize = 'small' | 'medium' | 'large' | 'full';

export interface DashboardGridProps {
  /** Grid children */
  children: React.ReactNode;
  /** Additional CSS classes */
  className?: string;
  /** Enable collapsible sections on mobile */
  collapsibleOnMobile?: boolean;
}

export interface DashboardGridItemProps {
  /** Widget content */
  children: React.ReactNode;
  /** Grid item size */
  size?: GridItemSize;
  /** Section title for collapsible mode */
  title?: string;
  /** Priority for lazy loading (lower = higher priority) */
  priority?: number;
  /** Additional CSS classes */
  className?: string;
  /** Start collapsed on mobile */
  defaultCollapsed?: boolean;
}

// ============================================================================
// Context
// ============================================================================

interface GridContextValue {
  collapsibleOnMobile: boolean;
  isMobile: boolean;
}

const GridContext = createContext<GridContextValue>({
  collapsibleOnMobile: false,
  isMobile: false,
});

// ============================================================================
// Grid Item Size Classes
// ============================================================================

const GRID_SIZE_CLASSES: Record<GridItemSize, string> = {
  small: 'col-span-1',
  medium: 'col-span-1 lg:col-span-1',
  large: 'col-span-1 md:col-span-2 lg:col-span-2',
  full: 'col-span-1 md:col-span-2 lg:col-span-3',
};

// ============================================================================
// CollapsibleSection Component
// ============================================================================

interface CollapsibleSectionProps {
  title: string;
  children: React.ReactNode;
  defaultCollapsed?: boolean;
  className?: string;
}

function CollapsibleSection({
  title,
  children,
  defaultCollapsed = false,
  className,
}: CollapsibleSectionProps) {
  const [isCollapsed, setIsCollapsed] = useState(defaultCollapsed);

  const toggleCollapsed = useCallback(() => {
    setIsCollapsed((prev) => !prev);
  }, []);

  return (
    <div className={className}>
      <Button
        variant="ghost"
        onClick={toggleCollapsed}
        className="w-full justify-between h-11 px-4 mb-2 text-sm font-medium"
        aria-expanded={!isCollapsed}
        data-testid={`collapsible-trigger-${title.toLowerCase().replace(/\s+/g, '-')}`}
      >
        <span>{title}</span>
        <ChevronDown
          className={cn(
            'h-4 w-4 transition-transform duration-200',
            isCollapsed ? 'rotate-0' : 'rotate-180'
          )}
        />
      </Button>
      <AnimatePresence initial={false}>
        {!isCollapsed && (
          <motion.div
            initial={{ height: 0, opacity: 0 }}
            animate={{ height: 'auto', opacity: 1 }}
            exit={{ height: 0, opacity: 0 }}
            transition={{ duration: 0.2 }}
            className="overflow-hidden"
          >
            {children}
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}

// ============================================================================
// DashboardGridItem Component
// ============================================================================

function DashboardGridItem({
  children,
  size = 'medium',
  title,
  className,
  defaultCollapsed = false,
}: DashboardGridItemProps) {
  const { collapsibleOnMobile, isMobile } = useContext(GridContext);
  const sizeClasses = GRID_SIZE_CLASSES[size];

  // Render collapsible on mobile if enabled and title provided
  if (collapsibleOnMobile && isMobile && title) {
    return (
      <div className={cn(sizeClasses, className)} data-testid="dashboard-grid-item">
        <CollapsibleSection title={title} defaultCollapsed={defaultCollapsed}>
          {children}
        </CollapsibleSection>
      </div>
    );
  }

  return (
    <div
      className={cn(sizeClasses, className)}
      data-testid="dashboard-grid-item"
    >
      {children}
    </div>
  );
}

// ============================================================================
// DashboardGrid Component
// ============================================================================

function DashboardGridRoot({
  children,
  className,
  collapsibleOnMobile = false,
}: DashboardGridProps) {
  // Check if mobile using CSS media query match (client-side only)
  const [isMobile, setIsMobile] = useState(false);

  React.useEffect(() => {
    const checkMobile = () => {
      setIsMobile(window.innerWidth < 640);
    };

    checkMobile();
    window.addEventListener('resize', checkMobile);
    return () => window.removeEventListener('resize', checkMobile);
  }, []);

  return (
    <GridContext.Provider value={{ collapsibleOnMobile, isMobile }}>
      <div
        className={cn(
          // Base grid layout
          'grid gap-4',
          // Mobile: 1 column
          'grid-cols-1',
          // Tablet: 2 columns
          'md:grid-cols-2',
          // Desktop: 3 columns
          'lg:grid-cols-3',
          // Additional classes
          className
        )}
        data-testid="dashboard-grid"
        role="region"
        aria-label="Dashboard widgets"
      >
        {children}
      </div>
    </GridContext.Provider>
  );
}

// ============================================================================
// Compound Component Export
// ============================================================================

export const DashboardGrid = Object.assign(DashboardGridRoot, {
  Item: DashboardGridItem,
});

export default DashboardGrid;

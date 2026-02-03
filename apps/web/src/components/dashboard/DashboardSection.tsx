/**
 * DashboardSection - Reusable Section Container
 * Issue #3286 - User Dashboard Layout System
 *
 * Features:
 * - Collapsible with animation
 * - Grid/List view toggle (independent per section)
 * - Sticky header with scroll detection
 * - Drag handle for reordering
 * - Active state indicator
 * - Warm Tabletop aesthetic
 *
 * @example
 * ```tsx
 * <DashboardSection
 *   id="collection"
 *   title="La Mia Collezione"
 *   icon={Gamepad2}
 *   viewMode="grid"
 *   onViewModeChange={(mode) => setViewMode(mode)}
 * >
 *   {children}
 * </DashboardSection>
 * ```
 */

'use client';

import { useRef, useEffect, useState, type ReactNode } from 'react';

import { motion, AnimatePresence } from 'framer-motion';
import { ChevronDown, GripVertical, Grid3X3, List, type LucideIcon } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export type ViewMode = 'grid' | 'list';

export interface DashboardSectionProps {
  /** Unique section identifier */
  id: string;
  /** Section title */
  title: string;
  /** Section icon */
  icon: LucideIcon;
  /** Current view mode */
  viewMode: ViewMode;
  /** Whether section is collapsed */
  collapsed: boolean;
  /** Whether section is currently active (in focus) */
  isActive?: boolean;
  /** Whether any section is being dragged */
  isDragging?: boolean;
  /** Callback when view mode changes */
  onViewModeChange?: (mode: ViewMode) => void;
  /** Callback when collapse is toggled */
  onCollapseToggle?: () => void;
  /** Whether to show grid/list toggle */
  showViewToggle?: boolean;
  /** Section content */
  children: ReactNode;
  /** Additional className */
  className?: string;
}

// ============================================================================
// DashboardSection Component
// ============================================================================

export function DashboardSection({
  id,
  title,
  icon: Icon,
  viewMode,
  collapsed,
  isActive = false,
  isDragging = false,
  onViewModeChange,
  onCollapseToggle,
  showViewToggle = true,
  children,
  className,
}: DashboardSectionProps) {
  const headerRef = useRef<HTMLDivElement>(null);
  const [isSticky, setIsSticky] = useState(false);

  // Intersection Observer for sticky header effect
  useEffect(() => {
    const header = headerRef.current;
    if (!header) return;

    const observer = new IntersectionObserver(
      ([entry]) => {
        // Header becomes sticky when it hits the top of the viewport
        setIsSticky(!entry.isIntersecting && entry.boundingClientRect.top < 0);
      },
      { threshold: [1], rootMargin: '-64px 0px 0px 0px' }
    );

    observer.observe(header);

    return () => observer.disconnect();
  }, []);

  return (
    <section
      id={id}
      className={cn(
        'rounded-2xl border bg-card/80 backdrop-blur-sm transition-all duration-300',
        // Active state
        isActive && !isDragging && 'ring-2 ring-primary/20 border-primary/30',
        // Hover state
        'hover:shadow-md',
        // Warm shadow
        'shadow-[0_4px_20px_rgba(139,90,60,0.08)]',
        className
      )}
      aria-labelledby={`${id}-heading`}
    >
      {/* Section Header */}
      <div
        ref={headerRef}
        className={cn(
          'flex items-center justify-between gap-3 px-4 py-3 sm:px-6',
          'border-b border-border/50',
          'rounded-t-2xl transition-all duration-200',
          // Sticky state
          isSticky && 'sticky top-16 z-10 bg-card/95 backdrop-blur-md shadow-sm'
        )}
      >
        {/* Left: Drag handle + Icon + Title */}
        <div className="flex items-center gap-3">
          {/* Drag Handle (hidden on mobile) */}
          <div
            className={cn(
              'hidden cursor-grab text-muted-foreground/50 transition-colors sm:block',
              'hover:text-muted-foreground active:cursor-grabbing'
            )}
            aria-label="Trascina per riordinare"
          >
            <GripVertical className="h-5 w-5" />
          </div>

          {/* Section Icon */}
          <div
            className={cn(
              'flex h-9 w-9 items-center justify-center rounded-lg transition-colors',
              isActive
                ? 'bg-primary/15 text-primary'
                : 'bg-muted text-muted-foreground'
            )}
          >
            <Icon className="h-5 w-5" />
          </div>

          {/* Section Title */}
          <h2
            id={`${id}-heading`}
            className="font-quicksand text-lg font-semibold tracking-tight"
          >
            {title}
          </h2>
        </div>

        {/* Right: View toggle + Collapse */}
        <div className="flex items-center gap-2">
          {/* View Mode Toggle */}
          {showViewToggle && onViewModeChange && (
            <div className="flex rounded-lg border border-border bg-muted/50 p-0.5">
              <Button
                variant="ghost"
                size="sm"
                onClick={() => onViewModeChange('grid')}
                className={cn(
                  'h-7 w-7 p-0',
                  viewMode === 'grid'
                    ? 'bg-background text-primary shadow-sm'
                    : 'text-muted-foreground hover:text-foreground'
                )}
                aria-label="Vista griglia"
                aria-pressed={viewMode === 'grid'}
              >
                <Grid3X3 className="h-4 w-4" />
              </Button>
              <Button
                variant="ghost"
                size="sm"
                onClick={() => onViewModeChange('list')}
                className={cn(
                  'h-7 w-7 p-0',
                  viewMode === 'list'
                    ? 'bg-background text-primary shadow-sm'
                    : 'text-muted-foreground hover:text-foreground'
                )}
                aria-label="Vista lista"
                aria-pressed={viewMode === 'list'}
              >
                <List className="h-4 w-4" />
              </Button>
            </div>
          )}

          {/* Collapse Toggle */}
          {onCollapseToggle && (
            <Button
              variant="ghost"
              size="sm"
              onClick={onCollapseToggle}
              className="h-8 w-8 p-0 text-muted-foreground hover:text-foreground"
              aria-expanded={!collapsed}
              aria-controls={`${id}-content`}
              aria-label={collapsed ? 'Espandi sezione' : 'Comprimi sezione'}
            >
              <motion.div
                animate={{ rotate: collapsed ? 0 : 180 }}
                transition={{ duration: 0.2 }}
              >
                <ChevronDown className="h-5 w-5" />
              </motion.div>
            </Button>
          )}
        </div>
      </div>

      {/* Section Content */}
      <AnimatePresence initial={false}>
        {!collapsed && (
          <motion.div
            id={`${id}-content`}
            initial={{ height: 0, opacity: 0 }}
            animate={{ height: 'auto', opacity: 1 }}
            exit={{ height: 0, opacity: 0 }}
            transition={{ duration: 0.3, ease: [0.4, 0, 0.2, 1] }}
            className="overflow-hidden"
          >
            <div className="p-4 sm:p-6">
              {children}
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </section>
  );
}

export default DashboardSection;

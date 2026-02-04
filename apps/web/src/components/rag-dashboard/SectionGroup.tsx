'use client';

import React from 'react';

import { motion, AnimatePresence } from 'framer-motion';
import { ChevronDown } from 'lucide-react';

import { cn } from '@/lib/utils';

// =============================================================================
// Accordion Section Component (Individual Collapsible Section)
// =============================================================================

export interface AccordionSectionProps {
  /** Unique identifier for the section */
  id: string;
  /** Section title */
  title: string;
  /** Optional description below the title */
  description?: string;
  /** Optional icon element */
  icon?: React.ReactNode;
  /** Whether the section is currently open */
  isOpen: boolean;
  /** Callback when section header is clicked */
  onToggle: () => void;
  /** Section content */
  children: React.ReactNode;
  /** Additional CSS classes */
  className?: string;
}

/**
 * Individual accordion section with smooth expand/collapse animation.
 * Used within SectionGroup for collapsible content.
 *
 * Features:
 * - Smooth 300ms slide animation
 * - Chevron rotation indicator
 * - Keyboard accessible (Space/Enter to toggle)
 * - Scroll margin for header offset
 */
export function AccordionSection({
  id,
  title,
  description,
  icon,
  isOpen,
  onToggle,
  children,
  className,
}: AccordionSectionProps) {
  return (
    <div
      id={id}
      className={cn('scroll-mt-24 border-b border-border last:border-b-0', className)}
    >
      {/* Accordion Header/Trigger */}
      <button
        type="button"
        onClick={onToggle}
        onKeyDown={(e) => {
          if (e.key === 'Enter' || e.key === ' ') {
            e.preventDefault();
            onToggle();
          }
        }}
        className={cn(
          'flex w-full items-center justify-between gap-3 py-4 text-left',
          'transition-colors hover:bg-muted/30 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-2',
          'rounded-lg px-2 -mx-2'
        )}
        aria-expanded={isOpen}
        aria-controls={`${id}-content`}
      >
        <div className="flex items-center gap-3">
          {icon && <div className="text-primary">{icon}</div>}
          <div>
            <h3 className="font-quicksand text-lg font-semibold">{title}</h3>
            {description && (
              <p className="text-sm text-muted-foreground">{description}</p>
            )}
          </div>
        </div>
        <ChevronDown
          className={cn(
            'h-5 w-5 shrink-0 text-muted-foreground transition-transform duration-300',
            isOpen && 'rotate-180'
          )}
          aria-hidden="true"
        />
      </button>

      {/* Accordion Content with Animation */}
      <AnimatePresence initial={false}>
        {isOpen && (
          <motion.div
            id={`${id}-content`}
            initial={{ height: 0, opacity: 0 }}
            animate={{ height: 'auto', opacity: 1 }}
            exit={{ height: 0, opacity: 0 }}
            transition={{ duration: 0.3, ease: 'easeInOut' }}
            className="overflow-hidden"
          >
            <div className="pb-6 pt-2">{children}</div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}

// =============================================================================
// Section Group Component (Group Header)
// =============================================================================

export interface SectionGroupProps {
  /** Unique identifier for scroll spy targeting */
  id: string;
  /** Display label for the group */
  label: string;
  /** Emoji icon for the group */
  icon: string;
  /** Optional description below the label */
  description?: string;
  /** Section components to render inside the group */
  children: React.ReactNode;
  /** Additional CSS classes */
  className?: string;
}

/**
 * Wrapper component that groups related dashboard sections together
 * with a visual group header. Supports the User Journey navigation pattern.
 *
 * Features:
 * - Semantic section element with ID for scroll spy
 * - Fade-in animation on viewport entry
 * - scroll-mt-24 for header offset compensation
 *
 * @example
 * ```tsx
 * <SectionGroup
 *   id="understand"
 *   label="Understand"
 *   icon="🎓"
 *   description="Learn how TOMAC-RAG works"
 * >
 *   <OverviewSection />
 *   <ArchitectureSection />
 * </SectionGroup>
 * ```
 */
export function SectionGroup({
  id,
  label,
  icon,
  description,
  children,
  className,
}: SectionGroupProps) {
  return (
    <motion.section
      id={id}
      initial={{ opacity: 0 }}
      whileInView={{ opacity: 1 }}
      viewport={{ once: true, margin: '-100px' }}
      transition={{ duration: 0.4 }}
      className={cn('scroll-mt-24', className)}
      aria-labelledby={`${id}-heading`}
    >
      {/* Group Header */}
      <div className="mb-6 flex items-center gap-3">
        <div
          className="flex h-10 w-10 items-center justify-center rounded-lg bg-primary/10 text-2xl"
          role="img"
          aria-hidden="true"
        >
          {icon}
        </div>
        <div>
          <h2 id={`${id}-heading`} className="font-quicksand text-xl font-semibold">
            {label}
          </h2>
          {description && (
            <p className="text-sm text-muted-foreground">{description}</p>
          )}
        </div>
      </div>

      {/* Group Content */}
      <div className="space-y-8">{children}</div>
    </motion.section>
  );
}

export default SectionGroup;

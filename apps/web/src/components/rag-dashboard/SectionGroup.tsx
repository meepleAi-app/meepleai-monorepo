'use client';

import { motion } from 'framer-motion';
import { cn } from '@/lib/utils';

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

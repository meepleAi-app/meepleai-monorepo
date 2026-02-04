'use client';

import { useState, useEffect } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { ChevronDown, ChevronRight } from 'lucide-react';
import { cn } from '@/lib/utils';
import { scrollToSection } from './hooks/useScrollSpy';
import { ProgressIndicator } from './ProgressIndicator';

export interface NavSection {
  id: string;
  label: string;
}

export interface NavGroup {
  id: string;
  label: string;
  icon: string;
  description?: string;
  sections: NavSection[];
}

export interface DashboardSidebarProps {
  /** Navigation groups with their sections */
  groups: NavGroup[];
  /** Currently active section ID from scroll spy */
  activeSection: string;
  /** Additional CSS classes */
  className?: string;
  /** Whether to show progress indicator at bottom */
  showProgress?: boolean;
}

/**
 * Desktop sidebar navigation component with collapsible section groups
 * and scroll spy integration. Hidden on mobile (<1024px).
 *
 * @example
 * ```tsx
 * <DashboardSidebar
 *   groups={NAVIGATION_GROUPS}
 *   activeSection={activeSection}
 *   className="hidden lg:block"
 * />
 * ```
 */
export function DashboardSidebar({
  groups,
  activeSection,
  className,
  showProgress = true,
}: DashboardSidebarProps) {
  // All groups expanded by default
  const [expandedGroups, setExpandedGroups] = useState<Set<string>>(
    new Set(groups.map((g) => g.id))
  );

  // Auto-expand group containing active section
  useEffect(() => {
    const activeGroup = groups.find((g) =>
      g.sections.some((s) => s.id === activeSection)
    );
    if (activeGroup && !expandedGroups.has(activeGroup.id)) {
      setExpandedGroups((prev) => new Set([...prev, activeGroup.id]));
    }
  }, [activeSection, groups, expandedGroups]);

  const toggleGroup = (groupId: string) => {
    setExpandedGroups((prev) => {
      const next = new Set(prev);
      if (next.has(groupId)) {
        next.delete(groupId);
      } else {
        next.add(groupId);
      }
      return next;
    });
  };

  const activeGroup = groups.find((g) =>
    g.sections.some((s) => s.id === activeSection)
  );

  const handleSectionClick = (sectionId: string) => {
    scrollToSection(sectionId);
  };

  return (
    <aside
      className={cn(
        'sticky top-20 h-[calc(100vh-5rem)] w-56 overflow-y-auto',
        'border-r border-border bg-background/95 backdrop-blur',
        'hidden lg:flex lg:flex-col',
        className
      )}
      role="navigation"
      aria-label="Dashboard navigation"
    >
      <nav className="flex-1 space-y-2 p-4">
        <div className="mb-4 text-xs font-semibold uppercase tracking-wider text-muted-foreground">
          Navigate
        </div>

        {groups.map((group) => (
          <div key={group.id} className="space-y-1">
            {/* Group Header */}
            <button
              onClick={() => toggleGroup(group.id)}
              className={cn(
                'flex w-full items-center justify-between rounded-md px-2 py-1.5 text-sm',
                'transition-colors hover:bg-muted/50',
                activeGroup?.id === group.id && 'bg-muted/30'
              )}
              aria-expanded={expandedGroups.has(group.id)}
              aria-controls={`group-${group.id}-sections`}
            >
              <span className="flex items-center gap-2">
                <span role="img" aria-hidden="true">
                  {group.icon}
                </span>
                <span className="font-medium">{group.label}</span>
              </span>
              {expandedGroups.has(group.id) ? (
                <ChevronDown className="h-4 w-4 text-muted-foreground" />
              ) : (
                <ChevronRight className="h-4 w-4 text-muted-foreground" />
              )}
            </button>

            {/* Sections */}
            <AnimatePresence>
              {expandedGroups.has(group.id) && (
                <motion.div
                  id={`group-${group.id}-sections`}
                  initial={{ height: 0, opacity: 0 }}
                  animate={{ height: 'auto', opacity: 1 }}
                  exit={{ height: 0, opacity: 0 }}
                  transition={{ duration: 0.2 }}
                  className="overflow-hidden"
                >
                  <div className="ml-4 space-y-0.5 border-l border-border pl-2">
                    {group.sections.map((section) => (
                      <button
                        key={section.id}
                        onClick={() => handleSectionClick(section.id)}
                        className={cn(
                          'block w-full rounded px-2 py-1 text-left text-sm transition-colors',
                          activeSection === section.id
                            ? 'bg-primary/10 font-medium text-primary'
                            : 'text-muted-foreground hover:bg-muted/50 hover:text-foreground'
                        )}
                        aria-current={activeSection === section.id ? 'true' : undefined}
                      >
                        {section.label}
                      </button>
                    ))}
                  </div>
                </motion.div>
              )}
            </AnimatePresence>
          </div>
        ))}
      </nav>

      {/* Progress Indicator */}
      {showProgress && <ProgressIndicator />}
    </aside>
  );
}

export default DashboardSidebar;

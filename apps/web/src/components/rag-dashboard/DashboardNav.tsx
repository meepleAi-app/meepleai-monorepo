'use client';

import { useState, useCallback, useEffect, useRef } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { ChevronDown } from 'lucide-react';
import { cn } from '@/lib/utils';
import { scrollToSection } from './hooks/useScrollSpy';
import type { NavGroup } from './DashboardSidebar';

export interface DashboardNavProps {
  /** Navigation groups with their sections */
  groups: NavGroup[];
  /** Currently active section ID from scroll spy */
  activeSection: string;
  /** Additional CSS classes */
  className?: string;
}

/**
 * Mobile-friendly top navigation component that replaces the sidebar
 * on smaller screens. Features a dropdown section selector.
 * Only visible on mobile/tablet (<1024px).
 *
 * @example
 * ```tsx
 * <DashboardNav
 *   groups={NAVIGATION_GROUPS}
 *   activeSection={activeSection}
 * />
 * ```
 */
export function DashboardNav({ groups, activeSection, className }: DashboardNavProps) {
  const [isOpen, setIsOpen] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);

  const activeGroup = groups.find((g) =>
    g.sections.some((s) => s.id === activeSection)
  );

  const activeLabel =
    activeGroup?.sections.find((s) => s.id === activeSection)?.label || 'Navigate';

  const handleSectionClick = useCallback((sectionId: string) => {
    scrollToSection(sectionId);
    setIsOpen(false);
  }, []);

  const handleBackdropClick = useCallback(() => {
    setIsOpen(false);
  }, []);

  const handleToggle = useCallback(() => {
    setIsOpen((prev) => !prev);
  }, []);

  // Close dropdown on escape key
  useEffect(() => {
    const handleEscape = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && isOpen) {
        setIsOpen(false);
      }
    };

    document.addEventListener('keydown', handleEscape);
    return () => document.removeEventListener('keydown', handleEscape);
  }, [isOpen]);

  // Prevent body scroll when dropdown is open
  useEffect(() => {
    if (isOpen) {
      document.body.style.overflow = 'hidden';
    } else {
      document.body.style.overflow = '';
    }
    return () => {
      document.body.style.overflow = '';
    };
  }, [isOpen]);

  return (
    <div
      ref={dropdownRef}
      className={cn(
        'sticky top-16 z-40 border-b bg-background/95 backdrop-blur lg:hidden',
        className
      )}
      role="navigation"
      aria-label="Mobile dashboard navigation"
    >
      {/* Current Section Indicator */}
      <button
        onClick={handleToggle}
        className="flex w-full items-center justify-between px-4 py-3"
        aria-expanded={isOpen}
        aria-controls="mobile-nav-dropdown"
        aria-haspopup="true"
      >
        <div className="flex items-center gap-2">
          <span role="img" aria-hidden="true">
            {activeGroup?.icon || '📋'}
          </span>
          <span className="font-medium">{activeLabel}</span>
        </div>
        <ChevronDown
          className={cn('h-5 w-5 transition-transform', isOpen && 'rotate-180')}
        />
      </button>

      {/* Dropdown Menu */}
      <AnimatePresence>
        {isOpen && (
          <>
            {/* Backdrop */}
            <motion.div
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={{ opacity: 0 }}
              className="fixed inset-0 z-30 bg-black/20"
              onClick={handleBackdropClick}
              aria-hidden="true"
            />

            {/* Dropdown Content */}
            <motion.div
              id="mobile-nav-dropdown"
              initial={{ opacity: 0, y: -10 }}
              animate={{ opacity: 1, y: 0 }}
              exit={{ opacity: 0, y: -10 }}
              transition={{ duration: 0.2 }}
              className="absolute left-0 right-0 top-full z-40 max-h-[60vh] overflow-y-auto border-b bg-background shadow-lg"
              role="menu"
            >
              {groups.map((group) => (
                <div key={group.id} className="border-b last:border-0">
                  {/* Group Header */}
                  <div className="flex items-center gap-2 bg-muted/30 px-4 py-2 text-sm font-medium">
                    <span role="img" aria-hidden="true">
                      {group.icon}
                    </span>
                    <span>{group.label}</span>
                  </div>

                  {/* Sections */}
                  <div className="py-1" role="group" aria-label={group.label}>
                    {group.sections.map((section) => (
                      <button
                        key={section.id}
                        onClick={() => handleSectionClick(section.id)}
                        className={cn(
                          'block w-full px-6 py-2 text-left text-sm transition-colors',
                          activeSection === section.id
                            ? 'bg-primary/10 font-medium text-primary'
                            : 'text-muted-foreground hover:bg-muted/50'
                        )}
                        role="menuitem"
                        aria-current={activeSection === section.id ? 'true' : undefined}
                      >
                        {section.label}
                      </button>
                    ))}
                  </div>
                </div>
              ))}
            </motion.div>
          </>
        )}
      </AnimatePresence>
    </div>
  );
}

export default DashboardNav;

'use client';

/**
 * Breadcrumbs Component
 *
 * Shows current navigation context: Group > Section
 * Supports click navigation to jump to groups/sections.
 */

import React, { useMemo } from 'react';

import { ChevronRight, Home } from 'lucide-react';

import { cn } from '@/lib/utils';

import { NAVIGATION_GROUPS } from './RagDashboard';

import type { NavGroup } from './DashboardSidebar';

export interface BreadcrumbsProps {
  /** Currently active section ID */
  activeSection?: string;
  /** Navigation groups configuration */
  groups?: NavGroup[];
  /** Additional CSS classes */
  className?: string;
  /** Callback when a breadcrumb item is clicked */
  onNavigate?: (id: string) => void;
}

interface BreadcrumbInfo {
  group: NavGroup | null;
  section: { id: string; label: string } | null;
}

/**
 * Find the group and section for a given section ID
 */
function findBreadcrumbInfo(
  sectionId: string | undefined,
  groups: NavGroup[]
): BreadcrumbInfo {
  if (!sectionId) {
    return { group: null, section: null };
  }

  for (const group of groups) {
    const section = group.sections.find((s) => s.id === sectionId);
    if (section) {
      return { group, section };
    }
  }

  // Check if sectionId is actually a group ID
  const directGroup = groups.find((g) => g.id === sectionId);
  if (directGroup) {
    return { group: directGroup, section: null };
  }

  return { group: null, section: null };
}

/**
 * Breadcrumb navigation component showing current position in dashboard.
 * Format: Home > Group > Section
 *
 * @example
 * ```tsx
 * <Breadcrumbs
 *   activeSection="query-sim"
 *   onNavigate={(id) => scrollToSection(id)}
 * />
 * ```
 */
export function Breadcrumbs({
  activeSection,
  groups = NAVIGATION_GROUPS,
  className,
  onNavigate,
}: BreadcrumbsProps) {
  const { group, section } = useMemo(
    () => findBreadcrumbInfo(activeSection, groups),
    [activeSection, groups]
  );

  const handleClick = (id: string) => {
    const element = document.getElementById(id);
    if (element) {
      element.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
    onNavigate?.(id);
  };

  // Don't show breadcrumbs if no active section
  if (!group) {
    return null;
  }

  return (
    <nav
      aria-label="Breadcrumb navigation"
      className={cn(
        'flex items-center gap-1 text-sm text-muted-foreground',
        className
      )}
    >
      {/* Home */}
      <button
        onClick={() => window.scrollTo({ top: 0, behavior: 'smooth' })}
        className="flex items-center gap-1 rounded-sm px-1.5 py-0.5 transition-colors hover:bg-muted hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary"
        aria-label="Scroll to top"
      >
        <Home className="h-3.5 w-3.5" />
        <span className="sr-only md:not-sr-only">Dashboard</span>
      </button>

      <ChevronRight className="h-3.5 w-3.5 text-muted-foreground/50" aria-hidden="true" />

      {/* Group */}
      <button
        onClick={() => handleClick(group.id)}
        className={cn(
          'flex items-center gap-1.5 rounded-sm px-1.5 py-0.5 transition-colors',
          'hover:bg-muted hover:text-foreground',
          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary',
          !section && 'font-medium text-foreground'
        )}
        aria-current={!section ? 'page' : undefined}
      >
        <span className="text-base" role="img" aria-hidden="true">
          {group.icon}
        </span>
        <span>{group.label}</span>
      </button>

      {/* Section (if available) */}
      {section && (
        <>
          <ChevronRight className="h-3.5 w-3.5 text-muted-foreground/50" aria-hidden="true" />
          <button
            onClick={() => handleClick(section.id)}
            className={cn(
              'rounded-sm px-1.5 py-0.5 font-medium text-foreground transition-colors',
              'hover:bg-muted',
              'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary'
            )}
            aria-current="page"
          >
            {section.label}
          </button>
        </>
      )}
    </nav>
  );
}

export default Breadcrumbs;

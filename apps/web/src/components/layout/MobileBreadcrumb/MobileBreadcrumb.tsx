/**
 * MobileBreadcrumb — Lightweight back navigation for mobile
 * Issue #5 from mobile-first-ux-epic.md
 *
 * Shows a breadcrumb trail on mobile (below MiniNav) with back navigation.
 * Hidden on desktop (md:hidden). Only renders when depth >= 2.
 *
 * Design:
 *   - Height: 36px, bg-muted/50, text-sm, font-nunito
 *   - Arrow-left icon + "Section > Subsection"
 *   - Collapses to "arrow-left Section" on viewport < 375px
 *   - Smooth fade transition on route change
 */

'use client';

import { useMemo } from 'react';

import { ArrowLeft, ChevronRight } from 'lucide-react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { getSegmentLabel } from '@/config/breadcrumb-labels';
import { cn } from '@/lib/utils';

interface BreadcrumbSegment {
  label: string;
  href: string;
}

/**
 * Build breadcrumb segments from a pathname.
 * Filters out route group segments (parentheses) and dynamic segments (UUIDs).
 */
function buildBreadcrumbs(pathname: string): BreadcrumbSegment[] {
  const parts = pathname.split('/').filter(Boolean);

  const segments: BreadcrumbSegment[] = [];
  let href = '';

  for (const part of parts) {
    // Skip Next.js route groups like (authenticated), (public)
    if (part.startsWith('(') && part.endsWith(')')) continue;

    // Skip UUID-like dynamic segments (show parent instead)
    if (/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(part)) continue;

    href += `/${part}`;
    segments.push({
      label: getSegmentLabel(part),
      href,
    });
  }

  return segments;
}

export function MobileBreadcrumb() {
  const pathname = usePathname();

  const segments = useMemo(() => buildBreadcrumbs(pathname), [pathname]);

  // Only show when there are at least 2 meaningful segments
  if (segments.length < 2) return null;

  const parentSegment = segments[segments.length - 2];
  const currentSegment = segments[segments.length - 1];

  return (
    <nav
      className={cn(
        'md:hidden',
        'h-9 flex items-center gap-1.5 px-4',
        'bg-muted/50',
        'border-b border-border/30',
        'animate-in fade-in-0 duration-150'
      )}
      aria-label="Breadcrumb"
      data-testid="mobile-breadcrumb"
    >
      {/* Back arrow — navigates to parent */}
      <Link
        href={parentSegment.href}
        className={cn(
          'flex items-center justify-center',
          'h-7 w-7 -ml-1',
          'rounded-md',
          'text-muted-foreground hover:text-foreground hover:bg-muted',
          'transition-colors duration-150',
          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'
        )}
        aria-label={`Torna a ${parentSegment.label}`}
      >
        <ArrowLeft className="h-4 w-4" />
      </Link>

      {/* Breadcrumb trail */}
      <ol className="flex items-center gap-1 text-sm font-nunito min-w-0 overflow-hidden">
        {/* Parent — tappable link, hidden on very small screens */}
        <li className="hidden min-[375px]:flex items-center gap-1 min-w-0">
          <Link
            href={parentSegment.href}
            className="text-muted-foreground hover:text-foreground truncate transition-colors duration-150"
          >
            {parentSegment.label}
          </Link>
          <ChevronRight className="h-3 w-3 text-muted-foreground/60 shrink-0" aria-hidden="true" />
        </li>

        {/* Current — non-interactive */}
        <li className="text-foreground font-medium truncate" aria-current="page">
          {currentSegment.label}
        </li>
      </ol>
    </nav>
  );
}

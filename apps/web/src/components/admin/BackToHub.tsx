/**
 * BackToHub - Navigation component for admin sub-pages
 *
 * Provides a consistent "back to Admin Hub" link on all admin sub-pages
 * after removing the admin sidebar. Uses entity color system for visual
 * consistency with the hub cards.
 */

'use client';

import { ArrowLeftIcon, LayoutDashboardIcon } from 'lucide-react';
import Link from 'next/link';

import { cn } from '@/lib/utils';

interface BackToHubProps {
  /** Optional current section label for breadcrumb context */
  section?: string;
  /** Optional custom class name */
  className?: string;
}

export function BackToHub({ section, className }: BackToHubProps) {
  return (
    <div className={cn('flex items-center gap-3 mb-6', className)}>
      <Link
        href="/admin"
        className={cn(
          'inline-flex items-center gap-2 px-3 py-1.5 rounded-lg text-sm font-medium',
          'text-muted-foreground hover:text-foreground',
          'bg-muted/30 hover:bg-muted/60 border border-border/30',
          'transition-all duration-150'
        )}
      >
        <ArrowLeftIcon className="h-3.5 w-3.5" />
        <LayoutDashboardIcon className="h-3.5 w-3.5" />
        <span>Admin Hub</span>
      </Link>
      {section && (
        <>
          <span className="text-muted-foreground/50 text-sm">/</span>
          <span className="text-sm text-muted-foreground font-medium">{section}</span>
        </>
      )}
    </div>
  );
}

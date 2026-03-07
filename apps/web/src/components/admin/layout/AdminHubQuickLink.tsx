'use client';

import { type ReactNode } from 'react';

import { ExternalLink } from 'lucide-react';
import Link from 'next/link';

import { cn } from '@/lib/utils';

interface AdminHubQuickLinkProps {
  href: string;
  icon: ReactNode;
  label: string;
  description: string;
  /** Optional accent color class for the icon background */
  accent?: string;
}

/**
 * Shared quick-link card for admin hub pages.
 * Used in Definitions, AI Lab, RAG, etc.
 * Glassmorphic style with hover effects.
 */
export function AdminHubQuickLink({
  href,
  icon,
  label,
  description,
  accent,
}: AdminHubQuickLinkProps) {
  return (
    <Link
      href={href}
      className={cn(
        'group relative flex items-start gap-3 rounded-xl p-4',
        'bg-white/70 dark:bg-zinc-800/50 backdrop-blur-md',
        'border border-slate-200/60 dark:border-zinc-700/40',
        'hover:border-primary/30 dark:hover:border-primary/30',
        'hover:shadow-md hover:shadow-primary/5',
        'transition-all duration-200'
      )}
    >
      <div
        className={cn(
          'flex h-9 w-9 shrink-0 items-center justify-center rounded-lg',
          'transition-transform duration-200 group-hover:scale-105',
          accent ?? 'bg-primary/10 text-primary',
          '[&>svg]:h-4.5 [&>svg]:w-4.5'
        )}
      >
        {icon}
      </div>
      <div className="min-w-0 flex-1">
        <div className="flex items-center gap-1.5">
          <p className="font-medium text-sm text-foreground">{label}</p>
          <ExternalLink className="h-3 w-3 text-muted-foreground/50 opacity-0 group-hover:opacity-100 transition-opacity" />
        </div>
        <p className="mt-0.5 text-xs text-muted-foreground leading-relaxed line-clamp-2">
          {description}
        </p>
      </div>
    </Link>
  );
}

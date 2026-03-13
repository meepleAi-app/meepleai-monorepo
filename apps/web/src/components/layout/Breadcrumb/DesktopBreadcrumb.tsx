/**
 * DesktopBreadcrumb Component
 * Navigability fix — docs/frontend/navigability-analysis.md R2
 *
 * Path-based breadcrumb trail for desktop (md+).
 * Renders an inline "Dashboard > Sezione > Pagina" path from the current URL.
 * Complementary to the mobile floating-pill Breadcrumb.
 */

'use client';

import { ChevronRight, Home } from 'lucide-react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { buildBreadcrumbs } from '@/lib/breadcrumb-utils';
import { cn } from '@/lib/utils';

// ─── Component ────────────────────────────────────────────────────────────────

export interface DesktopBreadcrumbProps {
  className?: string;
}

/**
 * DesktopBreadcrumb
 *
 * Renders a horizontal breadcrumb trail for desktop screens.
 * Returns null on dashboard and root pages so any border/spacing passed
 * via className is not rendered when there is no content.
 *
 * Usage in layout:
 *   <DesktopBreadcrumb className="hidden md:flex py-3 border-b border-border/40 mb-2" />
 */
export function DesktopBreadcrumb({ className }: DesktopBreadcrumbProps) {
  const pathname = usePathname();

  if (!pathname || pathname === '/') return null;

  const crumbs = buildBreadcrumbs(pathname);

  // Don't render a single-item breadcrumb (i.e. when already on dashboard)
  if (crumbs.length <= 1) return null;

  return (
    <nav
      aria-label="Percorso di navigazione"
      className={cn('flex items-center gap-1 text-sm text-muted-foreground', className)}
    >
      {crumbs.map((crumb, index) => (
        <span key={`${crumb.href ?? crumb.label}-${index}`} className="flex items-center gap-1">
          {index > 0 && (
            <ChevronRight className="h-3.5 w-3.5 shrink-0 opacity-40" aria-hidden="true" />
          )}

          {crumb.isCurrent || !crumb.href ? (
            <span
              className={cn(
                'font-medium',
                crumb.isCurrent ? 'text-foreground' : 'text-muted-foreground'
              )}
              aria-current={crumb.isCurrent ? 'page' : undefined}
            >
              {crumb.label}
            </span>
          ) : (
            <Link
              href={crumb.href}
              className="flex items-center gap-1 hover:text-foreground transition-colors duration-150"
            >
              {index === 0 && <Home className="h-3.5 w-3.5 shrink-0" aria-hidden="true" />}
              {crumb.label}
            </Link>
          )}
        </span>
      ))}
    </nav>
  );
}

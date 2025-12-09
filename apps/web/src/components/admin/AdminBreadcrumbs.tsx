/**
 * AdminBreadcrumbs Component - Issue #881
 *
 * Breadcrumb navigation for admin pages.
 * Features:
 * - Auto-generates breadcrumbs from pathname
 * - Custom breadcrumb items override
 * - Accessible navigation
 * - Responsive (hides on mobile when too long)
 */

'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { cn } from '@/lib/utils';
import { ChevronRightIcon, HomeIcon } from 'lucide-react';

export interface BreadcrumbItem {
  label: string;
  href?: string;
}

export interface AdminBreadcrumbsProps {
  /** Custom breadcrumb items (overrides auto-generation) */
  items?: BreadcrumbItem[];
  /** Show home icon for first item */
  showHomeIcon?: boolean;
  /** Additional className */
  className?: string;
}

/**
 * Converts a path segment to a human-readable label
 */
function segmentToLabel(segment: string): string {
  // Handle special cases
  const specialLabels: Record<string, string> = {
    admin: 'Admin',
    'n8n-templates': 'N8N Templates',
    'bulk-export': 'Bulk Export',
  };

  if (Object.prototype.hasOwnProperty.call(specialLabels, segment)) {
    // eslint-disable-next-line security/detect-object-injection -- Safe: specialLabels is a const Record
    return specialLabels[segment];
  }

  // Convert kebab-case or camelCase to Title Case
  return segment
    .replace(/-/g, ' ')
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .replace(/\b\w/g, l => l.toUpperCase());
}

/**
 * Generates breadcrumb items from pathname
 */
function generateBreadcrumbs(pathname: string): BreadcrumbItem[] {
  const segments = pathname.split('/').filter(Boolean);
  const breadcrumbs: BreadcrumbItem[] = [];

  let currentPath = '';
  for (let i = 0; i < segments.length; i++) {
    // eslint-disable-next-line security/detect-object-injection -- Safe: i is a controlled loop index
    const segment = segments[i];
    currentPath += `/${segment}`;

    // Skip UUID-like segments (detail pages)
    if (/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(segment)) {
      breadcrumbs.push({
        label: 'Details',
        href: i < segments.length - 1 ? currentPath : undefined,
      });
      continue;
    }

    // Skip numeric IDs
    if (/^\d+$/.test(segment)) {
      breadcrumbs.push({
        label: `#${segment}`,
        href: i < segments.length - 1 ? currentPath : undefined,
      });
      continue;
    }

    breadcrumbs.push({
      label: segmentToLabel(segment),
      // Last item has no href (current page)
      href: i < segments.length - 1 ? currentPath : undefined,
    });
  }

  return breadcrumbs;
}

export function AdminBreadcrumbs({ items, showHomeIcon = true, className }: AdminBreadcrumbsProps) {
  const pathname = usePathname();
  const breadcrumbs = items ?? generateBreadcrumbs(pathname ?? '');

  if (breadcrumbs.length === 0) {
    return null;
  }

  return (
    <nav aria-label="Breadcrumb" className={cn('flex items-center', className)}>
      <ol className="flex items-center gap-1 text-sm text-gray-500 dark:text-gray-400">
        {breadcrumbs.map((item, index) => (
          <li key={item.label + index} className="flex items-center gap-1">
            {index > 0 && (
              <ChevronRightIcon
                className="h-4 w-4 shrink-0 text-gray-400 dark:text-gray-500"
                aria-hidden="true"
              />
            )}
            {item.href ? (
              <Link
                href={item.href}
                className="flex items-center gap-1 hover:text-gray-700 dark:hover:text-gray-200 transition-colors"
              >
                {index === 0 && showHomeIcon && (
                  <HomeIcon className="h-4 w-4 shrink-0" aria-hidden="true" />
                )}
                <span className="hidden sm:inline">{item.label}</span>
                {index === 0 && showHomeIcon && <span className="sm:hidden">{item.label}</span>}
              </Link>
            ) : (
              <span
                className="flex items-center gap-1 font-medium text-gray-900 dark:text-white"
                aria-current="page"
              >
                {index === 0 && showHomeIcon && (
                  <HomeIcon className="h-4 w-4 shrink-0" aria-hidden="true" />
                )}
                {item.label}
              </span>
            )}
          </li>
        ))}
      </ol>
    </nav>
  );
}

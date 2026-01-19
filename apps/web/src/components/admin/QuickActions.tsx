/* eslint-disable security/detect-object-injection -- Safe action config Record access */
/**
 * QuickActions Component - Issue #885
 *
 * Grid of quick action buttons for the admin dashboard.
 * Provides easy access to common admin tasks.
 *
 * Features:
 * - Responsive grid (3 cols desktop, 2 tablet, 1 mobile)
 * - Icon + label for each action
 * - Hover effects
 * - Loading state
 * - Optional badge for notifications
 */

import {
  FileUpIcon,
  UsersIcon,
  AlertTriangleIcon,
  SettingsIcon,
  MessageSquareIcon,
  DatabaseIcon,
  ZapIcon,
} from 'lucide-react';
import Link from 'next/link';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { cn } from '@/lib/utils';

import type { LucideIcon } from 'lucide-react';

export interface QuickAction {
  id: string;
  label: string;
  description?: string;
  href: string;
  icon: LucideIcon;
  badge?: number | string;
  variant?: 'default' | 'primary' | 'warning' | 'danger';
}

export interface QuickActionsProps {
  actions?: QuickAction[];
  loading?: boolean;
  className?: string;
  title?: string;
}

const variantStyles = {
  default: 'hover:bg-gray-50 hover:border-gray-300',
  primary: 'hover:bg-blue-50 hover:border-blue-300',
  warning: 'hover:bg-yellow-50 hover:border-yellow-300',
  danger: 'hover:bg-red-50 hover:border-red-300',
};

const iconVariantStyles = {
  default: 'text-gray-600 bg-gray-100',
  primary: 'text-blue-600 bg-blue-100',
  warning: 'text-yellow-600 bg-yellow-100',
  danger: 'text-red-600 bg-red-100',
};

const badgeVariantStyles = {
  default: 'bg-gray-500',
  primary: 'bg-blue-500',
  warning: 'bg-yellow-500',
  danger: 'bg-red-500',
};

/**
 * Default quick actions for the admin dashboard
 */
export const defaultQuickActions: QuickAction[] = [
  {
    id: 'upload-pdf',
    label: 'Upload PDF',
    description: 'Add new game rules',
    href: '/admin/bulk-export',
    icon: FileUpIcon,
    variant: 'primary',
  },
  {
    id: 'manage-users',
    label: 'Manage Users',
    description: 'View and edit users',
    href: '/admin/users',
    icon: UsersIcon,
    variant: 'default',
  },
  {
    id: 'view-alerts',
    label: 'View Alerts',
    description: 'System notifications',
    href: '/admin/configuration',
    icon: AlertTriangleIcon,
    variant: 'warning',
  },
  {
    id: 'manage-prompts',
    label: 'Prompts',
    description: 'AI prompt templates',
    href: '/admin/prompts',
    icon: MessageSquareIcon,
    variant: 'default',
  },
  {
    id: 'configuration',
    label: 'Configuration',
    description: 'System settings',
    href: '/admin/configuration',
    icon: SettingsIcon,
    variant: 'default',
  },
  {
    id: 'clear-cache',
    label: 'Cache',
    description: 'Manage cache',
    href: '/admin/cache',
    icon: DatabaseIcon,
    variant: 'default',
  },
];

function QuickActionsSkeleton({ className }: { className?: string }) {
  return (
    <Card className={className}>
      <CardHeader className="pb-2">
        <Skeleton className="h-6 w-32" />
      </CardHeader>
      <CardContent>
        <div
          className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3"
          data-testid="quick-actions-skeleton"
        >
          {Array.from({ length: 6 }).map((_, index) => (
            <div
              key={`skeleton-${index}`}
              className="flex items-center gap-3 p-3 border rounded-lg"
            >
              <Skeleton className="h-10 w-10 rounded-lg shrink-0" />
              <div className="flex-1 space-y-1">
                <Skeleton className="h-4 w-24" />
                <Skeleton className="h-3 w-32" />
              </div>
            </div>
          ))}
        </div>
      </CardContent>
    </Card>
  );
}

export function QuickActions({
  actions = defaultQuickActions,
  loading = false,
  className,
  title = 'Quick Actions',
}: QuickActionsProps) {
  if (loading) {
    return <QuickActionsSkeleton className={className} />;
  }

  return (
    <Card className={className}>
      <CardHeader className="pb-2">
        <CardTitle className="text-lg font-semibold flex items-center gap-2">
          <ZapIcon className="h-5 w-5 text-yellow-500" aria-hidden="true" />
          {title}
        </CardTitle>
      </CardHeader>
      <CardContent>
        <div
          className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3"
          data-testid="quick-actions-grid"
        >
          {actions.map(action => {
            const Icon = action.icon;
            const variant = action.variant || 'default';

            return (
              <Link
                key={action.id}
                href={action.href}
                className={cn(
                  'flex items-center gap-3 p-3 border rounded-lg transition-all duration-200',
                  variantStyles[variant]
                )}
                data-testid={`quick-action-${action.id}`}
              >
                <div
                  className={cn('p-2 rounded-lg shrink-0', iconVariantStyles[variant])}
                  aria-hidden="true"
                >
                  <Icon className="h-5 w-5" />
                </div>
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2">
                    <span className="text-sm font-medium text-gray-900">{action.label}</span>
                    {action.badge !== undefined && (
                      <Badge
                        className={cn(
                          'text-white text-xs px-1.5 py-0.5',
                          badgeVariantStyles[variant]
                        )}
                      >
                        {typeof action.badge === 'number' && action.badge > 99
                          ? '99+'
                          : action.badge}
                      </Badge>
                    )}
                  </div>
                  {action.description && (
                    <p className="text-xs text-gray-500 truncate">{action.description}</p>
                  )}
                </div>
              </Link>
            );
          })}
        </div>
      </CardContent>
    </Card>
  );
}

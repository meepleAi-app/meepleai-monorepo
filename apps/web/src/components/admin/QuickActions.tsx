/* eslint-disable security/detect-object-injection -- Safe action config Record access */
/**
 * QuickActions Component - Issue #885, Refactored in #2788
 *
 * Grid of quick action buttons for the admin dashboard.
 * Provides easy access to common admin tasks.
 *
 * Features:
 * - Responsive grid (3 cols desktop, 2 tablet, 1 mobile)
 * - Icon + label for each action
 * - Dual styling: variant-based (legacy) and gradient-based (new)
 * - Hover effects: orange border + icon scale for gradients
 * - Loading state with skeleton
 * - Optional badge for notifications
 * - Dynamic badge counts via props
 *
 * @see Issue #2788 for gradient-based Quick Actions Panel requirements
 */

import {
  UsersIcon,
  AlertTriangleIcon,
  SettingsIcon,
  ZapIcon,
  CheckIcon,
  TrashIcon,
  DownloadIcon,
} from 'lucide-react';
import Link from 'next/link';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { cn } from '@/lib/utils';

import type { LucideIcon } from 'lucide-react';

export type GradientKey =
  | 'green-emerald'
  | 'blue-indigo'
  | 'amber-orange'
  | 'red-rose'
  | 'purple-violet'
  | 'stone-stone';

export interface QuickAction {
  id: string;
  label: string;
  description?: string;
  href: string;
  icon: LucideIcon;
  badge?: number | string;
  variant?: 'default' | 'primary' | 'warning' | 'danger';
  gradient?: GradientKey;
}

export interface QuickActionsProps {
  actions?: QuickAction[];
  loading?: boolean;
  className?: string;
  title?: string;
  badges?: Record<string, number>;
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

const gradientStyles: Record<GradientKey, string> = {
  'green-emerald': 'bg-gradient-to-br from-green-500 to-emerald-600 text-white',
  'blue-indigo': 'bg-gradient-to-br from-blue-500 to-indigo-600 text-white',
  'amber-orange': 'bg-gradient-to-br from-amber-500 to-orange-600 text-white',
  'red-rose': 'bg-gradient-to-br from-red-500 to-rose-600 text-white',
  'purple-violet': 'bg-gradient-to-br from-purple-500 to-violet-600 text-white',
  'stone-stone': 'bg-gradient-to-br from-stone-500 to-stone-600 text-white',
};

/**
 * Default quick actions for the admin dashboard (Issue #2788)
 * Uses gradient-based styling for modern look
 */
export const defaultQuickActions: QuickAction[] = [
  {
    id: 'approve-games',
    label: 'Approva Giochi',
    href: '/admin/games/pending',
    icon: CheckIcon,
    gradient: 'green-emerald',
    // TODO: Badge count should be fetched from API endpoint
  },
  {
    id: 'manage-users',
    label: 'Gestisci Utenti',
    href: '/admin/users',
    icon: UsersIcon,
    gradient: 'blue-indigo',
  },
  {
    id: 'view-alerts',
    label: 'Vedi Alert',
    href: '/admin/alerts',
    icon: AlertTriangleIcon,
    gradient: 'amber-orange',
    // TODO: Badge count should be fetched from API endpoint
  },
  {
    id: 'clear-cache',
    label: 'Svuota Cache',
    href: '/admin/cache',
    icon: TrashIcon,
    gradient: 'red-rose',
  },
  {
    id: 'export-data',
    label: 'Esporta Dati',
    href: '/admin/export',
    icon: DownloadIcon,
    gradient: 'purple-violet',
  },
  {
    id: 'configuration',
    label: 'Configurazione',
    href: '/admin/config',
    icon: SettingsIcon,
    gradient: 'stone-stone',
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
  badges,
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
            const badgeCount = badges?.[action.id] ?? action.badge;

            // Use gradient styling if gradient is specified, otherwise use variant
            const useGradient = !!action.gradient;
            const linkHoverClass = useGradient
              ? 'hover:border-orange-500 hover:shadow-md'
              : variantStyles[variant];
            const iconClass = useGradient
              ? gradientStyles[action.gradient!]
              : iconVariantStyles[variant];

            return (
              <Link
                key={action.id}
                href={action.href}
                className={cn(
                  'group flex items-center gap-3 p-3 border rounded-lg transition-all duration-200',
                  linkHoverClass
                )}
                data-testid={`quick-action-${action.id}`}
              >
                <div
                  className={cn(
                    'p-2 rounded-lg shrink-0 transition-transform duration-200',
                    useGradient && 'group-hover:scale-110',
                    iconClass
                  )}
                  aria-hidden="true"
                >
                  <Icon className="h-5 w-5" />
                </div>
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2">
                    <span className="text-sm font-medium text-gray-900">{action.label}</span>
                    {badgeCount !== undefined && (
                      <Badge
                        className={cn(
                          'text-white text-xs px-1.5 py-0.5',
                          useGradient ? 'bg-orange-500' : badgeVariantStyles[variant]
                        )}
                      >
                        {typeof badgeCount === 'number' && badgeCount > 99 ? '99+' : badgeCount}
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

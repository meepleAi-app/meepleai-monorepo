import * as React from 'react';

import { cva } from 'class-variance-authority';
import { Crown, Shield, Edit, User } from 'lucide-react';

import { cn } from '@/lib/utils';
import { normalizeRole, displayRole, type CanonicalRole } from '@/lib/utils/roles';

const userRoleBadgeVariants = cva(
  'inline-flex items-center gap-1.5 rounded-md px-2.5 py-0.5 text-xs font-semibold transition-colors',
  {
    variants: {
      role: {
        superadmin: 'bg-amber-100 text-amber-800 dark:bg-amber-900/30 dark:text-amber-300',
        admin: 'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-300',
        editor: 'bg-purple-100 text-purple-800 dark:bg-purple-900/30 dark:text-purple-300',
        creator: 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-300',
        user: 'bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-300',
      },
    },
    defaultVariants: {
      role: 'user',
    },
  }
);

const roleIconMap: Record<CanonicalRole, React.ComponentType<{ className?: string }>> = {
  superadmin: Crown,
  admin: Shield,
  editor: Edit,
  creator: Edit,
  user: User,
};

export interface UserRoleBadgeProps extends Omit<React.HTMLAttributes<HTMLSpanElement>, 'role'> {
  /** Accepts any case - will be normalized to lowercase internally */
  role: string;
  showIcon?: boolean;
}

export const UserRoleBadge = React.memo(function UserRoleBadge({
  role: rawRole,
  showIcon = true,
  className,
  ...props
}: UserRoleBadgeProps) {
  const role = normalizeRole(rawRole);
  const Icon = roleIconMap[role];

  return (
    <span className={cn(userRoleBadgeVariants({ role }), className)} {...props}>
      {showIcon && <Icon className="h-3 w-3" />}
      {displayRole(role)}
    </span>
  );
});

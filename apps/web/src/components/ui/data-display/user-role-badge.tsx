import * as React from 'react';

import { cva, type VariantProps } from 'class-variance-authority';
import { Shield, Edit, User } from 'lucide-react';

import { cn } from '@/lib/utils';

export type UserRole = 'Admin' | 'Editor' | 'User';

const userRoleBadgeVariants = cva(
  'inline-flex items-center gap-1.5 rounded-md px-2.5 py-0.5 text-xs font-semibold transition-colors',
  {
    variants: {
      role: {
        Admin: 'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-300',
        Editor: 'bg-purple-100 text-purple-800 dark:bg-purple-900/30 dark:text-purple-300',
        User: 'bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-300',
      },
    },
    defaultVariants: {
      role: 'User',
    },
  }
);

const roleIconMap: Record<UserRole, React.ComponentType<{ className?: string }>> = {
  Admin: Shield,
  Editor: Edit,
  User: User,
};

export interface UserRoleBadgeProps
  extends Omit<React.HTMLAttributes<HTMLSpanElement>, 'role'>,
    VariantProps<typeof userRoleBadgeVariants> {
  role: UserRole;
  showIcon?: boolean;
}

export const UserRoleBadge = React.memo(function UserRoleBadge({
  role,
  showIcon = true,
  className,
  ...props
}: UserRoleBadgeProps) {
  const Icon = roleIconMap[role];

  return (
    <span className={cn(userRoleBadgeVariants({ role }), className)} {...props}>
      {showIcon && <Icon className="h-3 w-3" />}
      {role}
    </span>
  );
});

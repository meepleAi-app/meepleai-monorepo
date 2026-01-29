import * as React from 'react';

import { cva, type VariantProps } from 'class-variance-authority';

import { cn } from '@/lib/utils';

export type UserStatus = 'Active' | 'Inactive' | 'Suspended';

const userStatusIndicatorVariants = cva('inline-flex items-center gap-2 text-sm', {
  variants: {
    status: {
      Active: 'text-green-700 dark:text-green-400',
      Inactive: 'text-gray-500 dark:text-gray-400',
      Suspended: 'text-red-700 dark:text-red-400',
    },
  },
  defaultVariants: {
    status: 'Active',
  },
});

const dotVariants = cva('h-2 w-2 rounded-full', {
  variants: {
    status: {
      Active: 'bg-green-500',
      Inactive: 'bg-gray-400',
      Suspended: 'bg-red-500 animate-pulse',
    },
  },
  defaultVariants: {
    status: 'Active',
  },
});

export interface UserStatusIndicatorProps
  extends Omit<React.HTMLAttributes<HTMLSpanElement>, 'status'>,
    VariantProps<typeof userStatusIndicatorVariants> {
  status: UserStatus;
  showLabel?: boolean;
}

export const UserStatusIndicator = React.memo(function UserStatusIndicator({
  status,
  showLabel = true,
  className,
  ...props
}: UserStatusIndicatorProps) {
  return (
    <span className={cn(userStatusIndicatorVariants({ status }), className)} {...props}>
      <span className={dotVariants({ status })} aria-hidden="true" />
      {showLabel && <span>{status}</span>}
    </span>
  );
});

// Helper to derive status from user data
export function getUserStatus(user: {
  isSuspended?: boolean;
  lastSeenAt?: string | null;
}): UserStatus {
  if (user.isSuspended) {
    return 'Suspended';
  }
  // Consider user inactive if not seen in last 30 days
  if (user.lastSeenAt) {
    const lastSeen = new Date(user.lastSeenAt);
    const now = new Date(Date.now()); // Use Date.now() to respect mocks in tests
    const thirtyDaysAgo = new Date(now);
    thirtyDaysAgo.setDate(thirtyDaysAgo.getDate() - 30);
    if (lastSeen < thirtyDaysAgo) {
      return 'Inactive';
    }
  }
  return 'Active';
}

/* eslint-disable local/no-hardcoded-color-utility -- admin CRUD chrome: text-white / button color on style-prop colored bg or admin-decorative inline gradient. DS-13c admin scope (--admin-* decision deferred to DS-15). */
'use client';

import { ShieldIcon, PencilIcon, UserIcon, HelpCircleIcon, type LucideIcon } from 'lucide-react';
import Link from 'next/link';

interface RoleCardProps {
  /** Role name */
  role: 'Admin' | 'Editor' | 'User' | 'Anonymous';
  /** User count or label */
  userCount: string;
  /** Role description */
  description: string;
  /** Icon name */
  iconName: 'shield' | 'pencil' | 'user' | 'help-circle';
  /** Color theme */
  colorClass: 'amber' | 'blue' | 'green' | 'gray';
  /** Optional link href for navigation */
  href?: string;
  /** Loading state */
  isLoading?: boolean;
}

const iconMap: Record<string, LucideIcon> = {
  shield: ShieldIcon,
  pencil: PencilIcon,
  user: UserIcon,
  'help-circle': HelpCircleIcon,
};

const colorStyles = {
  amber: {
    iconBg: 'bg-amber-100',
    iconText: 'text-amber-700',
    badgeBg: 'bg-amber-100',
    badgeText: 'text-amber-900',
  },
  blue: {
    iconBg: 'bg-blue-100',
    iconText: 'text-blue-700',
    badgeBg: 'bg-blue-100',
    badgeText: 'text-blue-900',
  },
  green: {
    iconBg: 'bg-green-100',
    iconText: 'text-green-700',
    badgeBg: 'bg-green-100',
    badgeText: 'text-green-900',
  },
  gray: {
    iconBg: 'bg-muted',
    iconText: 'text-foreground',
    badgeBg: 'bg-muted',
    badgeText: 'text-foreground',
  },
};

export function RoleCard({
  role,
  userCount,
  description,
  iconName,
  colorClass,
  href,
  isLoading,
}: RoleCardProps) {
  const colors = colorStyles[colorClass];
  const Icon = iconMap[iconName];

  const content = (
    <>
      <div className="flex items-center justify-between mb-4">
        <div
          className={`w-12 h-12 ${colors.iconBg} dark:bg-zinc-700 rounded-lg flex items-center justify-center`}
        >
          <Icon className={`w-6 h-6 ${colors.iconText} dark:text-zinc-300`} />
        </div>
        <span
          className={`px-3 py-1 ${colors.badgeBg} ${colors.badgeText} dark:bg-zinc-700 dark:text-zinc-300 text-sm font-semibold rounded-full`}
        >
          {isLoading ? (
            <span className="inline-block w-12 h-4 bg-muted dark:bg-zinc-600 rounded animate-pulse" />
          ) : (
            userCount
          )}
        </span>
      </div>
      <h3 className="font-quicksand text-xl font-bold text-foreground dark:text-zinc-100 mb-2">
        {role}
      </h3>
      <p className="text-muted-foreground dark:text-muted-foreground text-sm">{description}</p>
    </>
  );

  const className =
    'bg-card/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl p-6 border border-border/50 dark:border-zinc-700/50 shadow-sm hover:shadow-md transition-shadow';

  if (href) {
    return (
      <Link href={href} className={`${className} block no-underline`}>
        {content}
      </Link>
    );
  }

  return <div className={className}>{content}</div>;
}

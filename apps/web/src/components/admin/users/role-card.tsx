'use client';

import { ShieldIcon, PencilIcon, UserIcon, HelpCircleIcon, type LucideIcon } from 'lucide-react';

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
    iconBg: 'bg-gray-100',
    iconText: 'text-gray-700',
    badgeBg: 'bg-gray-100',
    badgeText: 'text-gray-900',
  },
};

export function RoleCard({ role, userCount, description, iconName, colorClass }: RoleCardProps) {
  const colors = colorStyles[colorClass];
  const Icon = iconMap[iconName];

  return (
    <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl p-6 border border-slate-200/50 dark:border-zinc-700/50 shadow-sm hover:shadow-md transition-shadow">
      <div className="flex items-center justify-between mb-4">
        <div className={`w-12 h-12 ${colors.iconBg} dark:bg-zinc-700 rounded-lg flex items-center justify-center`}>
          <Icon className={`w-6 h-6 ${colors.iconText} dark:text-zinc-300`} />
        </div>
        <span className={`px-3 py-1 ${colors.badgeBg} ${colors.badgeText} dark:bg-zinc-700 dark:text-zinc-300 text-sm font-semibold rounded-full`}>
          {userCount}
        </span>
      </div>
      <h3 className="font-quicksand text-xl font-bold text-slate-900 dark:text-zinc-100 mb-2">
        {role}
      </h3>
      <p className="text-slate-600 dark:text-zinc-400 text-sm">
        {description}
      </p>
    </div>
  );
}

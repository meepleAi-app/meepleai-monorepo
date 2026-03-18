'use client';

import { BookOpen, Bot, Database, LayoutDashboard, Settings, Users } from 'lucide-react';

// ActionGrid relies on navigation context providers — we show its usage inline
// with descriptive placeholder cards representing real quick-action patterns.

interface QuickAction {
  label: string;
  description: string;
  icon: React.ComponentType<{ className?: string }>;
  color: string;
  href: string;
}

const ADMIN_ACTIONS: QuickAction[] = [
  {
    label: 'Overview',
    description: 'KPIs, pending requests, library summary',
    icon: LayoutDashboard,
    color: 'bg-amber-100 text-amber-700 dark:bg-amber-950/40 dark:text-amber-400',
    href: '/admin/overview',
  },
  {
    label: 'AI Agents',
    description: 'Configure models, strategies, and pipeline',
    icon: Bot,
    color: 'bg-purple-100 text-purple-700 dark:bg-purple-950/40 dark:text-purple-400',
    href: '/admin/agents',
  },
  {
    label: 'Knowledge Base',
    description: 'Vector collections, documents, queues',
    icon: Database,
    color: 'bg-blue-100 text-blue-700 dark:bg-blue-950/40 dark:text-blue-400',
    href: '/admin/knowledge-base',
  },
  {
    label: 'Content',
    description: 'Games catalog, categories, PDF processing',
    icon: BookOpen,
    color: 'bg-green-100 text-green-700 dark:bg-green-950/40 dark:text-green-400',
    href: '/admin/content',
  },
  {
    label: 'Users',
    description: 'Roles, permissions, activity, invitations',
    icon: Users,
    color: 'bg-rose-100 text-rose-700 dark:bg-rose-950/40 dark:text-rose-400',
    href: '/admin/users',
  },
  {
    label: 'System',
    description: 'Feature flags, config, health monitoring',
    icon: Settings,
    color: 'bg-slate-100 text-slate-700 dark:bg-slate-950/40 dark:text-slate-400',
    href: '/admin/system',
  },
];

export default function NavigationSystemScene() {
  return (
    <div className="space-y-6">
      <div>
        <h3 className="font-quicksand text-base font-semibold text-foreground">
          ActionGrid — Admin Quick Actions
        </h3>
        <p className="mt-1 font-nunito text-sm text-muted-foreground">
          Quick-action cards that surface the six admin sections. ActionGrid is powered by the
          navigation context and renders dynamically based on the registered actions.
        </p>
      </div>

      {/* Mock ActionGrid */}
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
        {ADMIN_ACTIONS.map(action => {
          const Icon = action.icon;
          return (
            <div
              key={action.label}
              className="flex cursor-pointer flex-col gap-3 rounded-xl border border-border/50 bg-card/50 p-4 transition-all duration-200 hover:border-amber-300/50 hover:shadow-md"
            >
              <div
                className={`inline-flex h-9 w-9 items-center justify-center rounded-lg ${action.color}`}
              >
                <Icon className="h-4 w-4" />
              </div>
              <div>
                <p className="font-quicksand text-sm font-semibold text-foreground">
                  {action.label}
                </p>
                <p className="mt-0.5 font-nunito text-xs text-muted-foreground line-clamp-2">
                  {action.description}
                </p>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}

'use client';

import { useState } from 'react';

import { Clock, User, Gamepad2, FileText, Settings, Bot, Shield, AlertCircle } from 'lucide-react';

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';

/**
 * Admin Activity Log Page
 * Route: /admin/overview/activity
 *
 * Shows timeline of recent admin events. Backend API not yet connected —
 * displays placeholder entries until audit log endpoint is implemented.
 */

interface ActivityEntry {
  id: string;
  action: string;
  target: string;
  category: string;
  timestamp: string;
  icon: React.ComponentType<{ className?: string }>;
}

const PLACEHOLDER_ENTRIES: ActivityEntry[] = [
  {
    id: '1',
    action: 'Approved shared game',
    target: 'Azul',
    category: 'games',
    timestamp: '2 minutes ago',
    icon: Gamepad2,
  },
  {
    id: '2',
    action: 'Created agent definition',
    target: 'Wingspan Helper',
    category: 'agents',
    timestamp: '15 minutes ago',
    icon: Bot,
  },
  {
    id: '3',
    action: 'Updated feature flag',
    target: 'enable-rag-v2',
    category: 'system',
    timestamp: '1 hour ago',
    icon: Settings,
  },
  {
    id: '4',
    action: 'Reindexed document',
    target: 'Catan Rules PDF',
    category: 'documents',
    timestamp: '2 hours ago',
    icon: FileText,
  },
  {
    id: '5',
    action: 'Changed user role',
    target: 'user@example.com → Admin',
    category: 'users',
    timestamp: '3 hours ago',
    icon: Shield,
  },
  {
    id: '6',
    action: 'Cleared cache',
    target: 'KB vector cache',
    category: 'system',
    timestamp: '5 hours ago',
    icon: Settings,
  },
  {
    id: '7',
    action: 'Registered new user',
    target: 'player@boardgame.io',
    category: 'users',
    timestamp: '6 hours ago',
    icon: User,
  },
  {
    id: '8',
    action: 'Imported game',
    target: 'Terraforming Mars',
    category: 'games',
    timestamp: '1 day ago',
    icon: Gamepad2,
  },
];

const CATEGORY_COLORS: Record<string, string> = {
  users: 'text-blue-600 dark:text-blue-400 bg-blue-100/80 dark:bg-blue-900/30',
  games: 'text-amber-600 dark:text-amber-400 bg-amber-100/80 dark:bg-amber-900/30',
  agents: 'text-purple-600 dark:text-purple-400 bg-purple-100/80 dark:bg-purple-900/30',
  documents: 'text-emerald-600 dark:text-emerald-400 bg-emerald-100/80 dark:bg-emerald-900/30',
  system: 'text-slate-600 dark:text-slate-400 bg-slate-100/80 dark:bg-slate-700/30',
};

export default function ActivityLogPage() {
  const [typeFilter, setTypeFilter] = useState<string>('all');
  const [dateRange, setDateRange] = useState<string>('7d');

  const filtered =
    typeFilter === 'all'
      ? PLACEHOLDER_ENTRIES
      : PLACEHOLDER_ENTRIES.filter(e => e.category === typeFilter);

  return (
    <div className="space-y-6">
      <div>
        <h1 className="font-quicksand text-2xl font-bold text-foreground">Activity Log</h1>
        <p className="text-sm text-muted-foreground mt-1">Recent admin actions and system events</p>
      </div>

      {/* Info banner */}
      <div className="flex items-center gap-2 rounded-xl border border-amber-200/60 dark:border-amber-800/40 bg-amber-50/50 dark:bg-amber-900/10 px-4 py-3">
        <AlertCircle className="h-4 w-4 text-amber-600 dark:text-amber-400 shrink-0" />
        <p className="text-xs text-amber-700 dark:text-amber-300">
          Showing placeholder data. Activity log will display real events once the audit API is
          connected.
        </p>
      </div>

      {/* Filters */}
      <div className="flex gap-3 flex-wrap">
        <Select value={typeFilter} onValueChange={setTypeFilter}>
          <SelectTrigger className="w-44">
            <SelectValue placeholder="Activity type" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All Activities</SelectItem>
            <SelectItem value="users">Users</SelectItem>
            <SelectItem value="games">Games</SelectItem>
            <SelectItem value="agents">Agents</SelectItem>
            <SelectItem value="documents">Documents</SelectItem>
            <SelectItem value="system">System</SelectItem>
          </SelectContent>
        </Select>

        <Select value={dateRange} onValueChange={setDateRange}>
          <SelectTrigger className="w-44">
            <SelectValue placeholder="Date range" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="24h">Last 24 Hours</SelectItem>
            <SelectItem value="7d">Last 7 Days</SelectItem>
            <SelectItem value="30d">Last 30 Days</SelectItem>
          </SelectContent>
        </Select>
      </div>

      {/* Timeline */}
      <div className="relative space-y-0">
        {/* Vertical line */}
        <div className="absolute left-[19px] top-2 bottom-2 w-px bg-slate-200/80 dark:bg-zinc-700/60" />

        {filtered.map(entry => {
          const Icon = entry.icon;
          const colorClass = CATEGORY_COLORS[entry.category] ?? CATEGORY_COLORS.system;

          return (
            <div key={entry.id} className="relative flex items-start gap-4 py-3 pl-0">
              {/* Icon dot */}
              <div
                className={`relative z-10 flex h-10 w-10 shrink-0 items-center justify-center rounded-xl ${colorClass}`}
              >
                <Icon className="h-4 w-4" />
              </div>

              {/* Content */}
              <div className="flex-1 min-w-0">
                <p className="text-sm font-medium text-foreground">
                  {entry.action} <span className="font-semibold">&ldquo;{entry.target}&rdquo;</span>
                </p>
                <div className="flex items-center gap-2 mt-0.5">
                  <Clock className="h-3 w-3 text-muted-foreground/60" />
                  <span className="text-xs text-muted-foreground">{entry.timestamp}</span>
                </div>
              </div>
            </div>
          );
        })}

        {filtered.length === 0 && (
          <div className="py-12 text-center">
            <p className="text-sm text-muted-foreground">
              No activities match the selected filter.
            </p>
          </div>
        )}
      </div>
    </div>
  );
}

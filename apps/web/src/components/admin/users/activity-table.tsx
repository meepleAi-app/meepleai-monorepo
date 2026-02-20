'use client';

import { Badge } from '@/components/ui/badge';

interface ActivityEntry {
  id: string;
  timestamp: string;
  user: {
    name: string;
    email: string;
    avatar?: string;
  };
  action: string;
  actionType: 'login' | 'edit' | 'approve' | 'config' | 'delete';
  target: string;
  ipAddress: string;
  status: 'success' | 'error';
}

// Mock data
const MOCK_ACTIVITIES: ActivityEntry[] = [
  {
    id: '1',
    timestamp: '2026-02-18 14:35:22',
    user: { name: 'Sarah Chen', email: 'sarah@meepleai.com', avatar: undefined },
    action: 'Approved game',
    actionType: 'approve',
    target: 'Catan (ID: 789)',
    ipAddress: '192.168.1.45',
    status: 'success',
  },
  {
    id: '2',
    timestamp: '2026-02-18 14:12:05',
    user: { name: 'Mike Johnson', email: 'mike@meepleai.com', avatar: undefined },
    action: 'Updated AI model',
    actionType: 'config',
    target: 'GPT-4 Turbo settings',
    ipAddress: '10.0.0.12',
    status: 'success',
  },
  {
    id: '3',
    timestamp: '2026-02-18 13:48:33',
    user: { name: 'Admin User', email: 'admin@meepleai.com', avatar: undefined },
    action: 'Deleted user',
    actionType: 'delete',
    target: 'spam_account@test.com',
    ipAddress: '192.168.1.1',
    status: 'success',
  },
  {
    id: '4',
    timestamp: '2026-02-18 13:22:17',
    user: { name: 'Emily Rodriguez', email: 'emily@meepleai.com', avatar: undefined },
    action: 'Edited category',
    actionType: 'edit',
    target: 'Strategy Games',
    ipAddress: '192.168.1.88',
    status: 'success',
  },
  {
    id: '5',
    timestamp: '2026-02-18 12:55:44',
    user: { name: 'John Smith', email: 'john@meepleai.com', avatar: undefined },
    action: 'Failed login',
    actionType: 'login',
    target: 'Web Portal',
    ipAddress: '203.0.113.42',
    status: 'error',
  },
  {
    id: '6',
    timestamp: '2026-02-18 12:30:11',
    user: { name: 'Sarah Chen', email: 'sarah@meepleai.com', avatar: undefined },
    action: 'Login',
    actionType: 'login',
    target: 'Admin Dashboard',
    ipAddress: '192.168.1.45',
    status: 'success',
  },
  {
    id: '7',
    timestamp: '2026-02-18 11:45:29',
    user: { name: 'Mike Johnson', email: 'mike@meepleai.com', avatar: undefined },
    action: 'Approved game',
    actionType: 'approve',
    target: 'Wingspan (ID: 456)',
    ipAddress: '10.0.0.12',
    status: 'success',
  },
  {
    id: '8',
    timestamp: '2026-02-18 11:15:03',
    user: { name: 'Emily Rodriguez', email: 'emily@meepleai.com', avatar: undefined },
    action: 'Updated category',
    actionType: 'edit',
    target: 'Family Games',
    ipAddress: '192.168.1.88',
    status: 'success',
  },
];

const actionTypeColors = {
  login: 'bg-blue-100 text-blue-900 dark:bg-blue-900/30 dark:text-blue-300',
  edit: 'bg-amber-100 text-amber-900 dark:bg-amber-900/30 dark:text-amber-300',
  approve: 'bg-green-100 text-green-900 dark:bg-green-900/30 dark:text-green-300',
  config: 'bg-purple-100 text-purple-900 dark:bg-purple-900/30 dark:text-purple-300',
  delete: 'bg-red-100 text-red-900 dark:bg-red-900/30 dark:text-red-300',
};

const statusColors = {
  success: 'bg-green-100 text-green-900 dark:bg-green-900/30 dark:text-green-300',
  error: 'bg-red-100 text-red-900 dark:bg-red-900/30 dark:text-red-300',
};

function getUserInitials(name: string): string {
  return name
    .split(' ')
    .map((n) => n[0])
    .join('')
    .toUpperCase()
    .slice(0, 2);
}

export function ActivityTable() {
  return (
    <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border border-amber-200/50 dark:border-zinc-700/50 rounded-lg overflow-hidden">
      <div className="overflow-x-auto">
        <table className="w-full">
          <thead>
            <tr className="bg-amber-100/50 dark:bg-zinc-900/50 border-b border-amber-200/50 dark:border-zinc-700/50">
              <th className="text-left py-3 px-4 text-sm font-bold text-amber-900 dark:text-amber-400 uppercase tracking-wider">
                Timestamp
              </th>
              <th className="text-left py-3 px-4 text-sm font-bold text-amber-900 dark:text-amber-400 uppercase tracking-wider">
                User
              </th>
              <th className="text-left py-3 px-4 text-sm font-bold text-amber-900 dark:text-amber-400 uppercase tracking-wider">
                Action
              </th>
              <th className="text-left py-3 px-4 text-sm font-bold text-amber-900 dark:text-amber-400 uppercase tracking-wider">
                Target
              </th>
              <th className="text-left py-3 px-4 text-sm font-bold text-amber-900 dark:text-amber-400 uppercase tracking-wider">
                IP Address
              </th>
              <th className="text-right py-3 px-4 text-sm font-bold text-amber-900 dark:text-amber-400 uppercase tracking-wider">
                Status
              </th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-200 dark:divide-zinc-700">
            {MOCK_ACTIVITIES.map((activity) => (
              <tr key={activity.id} className="hover:bg-slate-50/50 dark:hover:bg-zinc-900/50 transition-colors">
                <td className="py-3 px-4 font-mono text-sm text-slate-600 dark:text-zinc-400">
                  {activity.timestamp}
                </td>
                <td className="py-3 px-4">
                  <div className="flex items-center gap-3">
                    <div className="h-8 w-8 rounded-full bg-amber-100 dark:bg-amber-900/30 flex items-center justify-center text-xs font-semibold text-amber-900 dark:text-amber-300">
                      {getUserInitials(activity.user.name)}
                    </div>
                    <div>
                      <div className="font-medium text-slate-900 dark:text-zinc-100">
                        {activity.user.name}
                      </div>
                      <div className="text-xs text-slate-500 dark:text-zinc-400">
                        {activity.user.email}
                      </div>
                    </div>
                  </div>
                </td>
                <td className="py-3 px-4">
                  <Badge variant="outline" className={actionTypeColors[activity.actionType]}>
                    {activity.action}
                  </Badge>
                </td>
                <td className="py-3 px-4 text-slate-700 dark:text-zinc-300">
                  {activity.target}
                </td>
                <td className="py-3 px-4 font-mono text-sm text-slate-600 dark:text-zinc-400">
                  {activity.ipAddress}
                </td>
                <td className="py-3 px-4 text-right">
                  <Badge variant="outline" className={statusColors[activity.status]}>
                    {activity.status === 'success' ? 'Success' : 'Error'}
                  </Badge>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

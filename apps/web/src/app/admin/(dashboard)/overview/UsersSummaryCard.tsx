'use client';

import { Users, Send } from 'lucide-react';
import Link from 'next/link';

// ============================================================================
// Types
// ============================================================================

export interface RecentUser {
  id: string;
  displayName: string | null;
  email: string;
  createdAt: string;
}

export interface UsersSummaryCardProps {
  totalUsers: number;
  activeUsers: number;
  pendingInvitations: number;
  recentUsers: RecentUser[];
}

// ============================================================================
// Helpers
// ============================================================================

function formatItalianDate(dateStr: string): string {
  const date = new Date(dateStr);
  return date.toLocaleDateString('it-IT', { day: 'numeric', month: 'short' });
}

// ============================================================================
// Component
// ============================================================================

export function UsersSummaryCard({
  totalUsers,
  activeUsers,
  pendingInvitations,
  recentUsers,
}: UsersSummaryCardProps) {
  return (
    <div className="rounded-2xl border border-slate-200/60 dark:border-zinc-700/40 bg-white/70 dark:bg-zinc-800/50 backdrop-blur-sm p-5 flex flex-col gap-4">
      {/* Header */}
      <div className="flex items-center gap-3">
        <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-orange-100/80 dark:bg-orange-500/20">
          <Users className="h-5 w-5 text-orange-700 dark:text-orange-400" />
        </div>
        <span className="font-quicksand font-semibold text-foreground">Utenti</span>
      </div>

      {/* Stats */}
      <div className="flex flex-col gap-1">
        <p className="text-2xl font-bold text-foreground" data-testid="users-total">
          {totalUsers.toLocaleString()}
        </p>
        <p className="text-sm text-muted-foreground">totali</p>

        <div className="flex flex-wrap items-center gap-3 mt-1">
          {/* Active users */}
          <span className="flex items-center gap-1 text-sm text-muted-foreground">
            <Users className="h-3.5 w-3.5" />
            <span data-testid="users-active">{activeUsers.toLocaleString()} attivi 30gg</span>
          </span>

          {/* Pending invitations — only when > 0 */}
          {pendingInvitations > 0 && (
            <span
              className="flex items-center gap-1 text-sm text-amber-600 dark:text-amber-400"
              data-testid="users-pending-invitations"
            >
              <Send className="h-3.5 w-3.5" />
              <span>{pendingInvitations} inviti pendenti</span>
            </span>
          )}
        </div>
      </div>

      {/* Recent users section */}
      <div className="flex flex-col gap-2">
        <p className="text-xs uppercase tracking-wide text-muted-foreground font-semibold">
          Ultimi registrati
        </p>

        {recentUsers.length === 0 ? (
          <p className="text-sm italic text-muted-foreground">Nessun utente recente</p>
        ) : (
          <ul className="flex flex-col gap-1">
            {recentUsers.map(user => (
              <li key={user.id} className="flex items-center justify-between text-sm">
                <span className="text-foreground">{user.displayName ?? user.email}</span>
                <span className="text-xs text-muted-foreground">
                  {formatItalianDate(user.createdAt)}
                </span>
              </li>
            ))}
          </ul>
        )}
      </div>

      {/* Footer link */}
      <div className="mt-auto pt-2 border-t border-slate-200/60 dark:border-zinc-700/40">
        <Link
          href="/admin/users"
          className="text-sm text-orange-600 dark:text-orange-400 hover:underline font-medium"
        >
          Gestisci utenti →
        </Link>
      </div>
    </div>
  );
}

export default UsersSummaryCard;

/**
 * Admin Users List Page
 *
 * Placeholder page for the user management list.
 * Links from Admin Hub → Users tab → User List card.
 */

import type { Metadata } from 'next';

import { UsersIcon } from 'lucide-react';

import { RequireRole } from '@/components/auth/RequireRole';

export const metadata: Metadata = {
  title: 'User Management | Admin',
  description: 'Manage users, roles, and permissions',
};

export default function AdminUsersPage() {
  return (
    <RequireRole allowedRoles={['Admin']}>
      <div className="space-y-6">
        <div>
          <h1 className="text-2xl font-bold tracking-tight text-foreground font-quicksand">
            User Management
          </h1>
          <p className="text-sm text-muted-foreground mt-1">
            Gestione utenti, ruoli e permessi
          </p>
        </div>
        <div className="flex flex-col items-center justify-center rounded-2xl border border-dashed border-border/50 bg-muted/20 p-12 text-center">
          <UsersIcon className="h-12 w-12 text-muted-foreground/40 mb-4" />
          <h2 className="text-lg font-semibold text-foreground mb-2">Coming Soon</h2>
          <p className="text-sm text-muted-foreground max-w-md">
            La pagina di gestione utenti consolidata è in fase di sviluppo.
            Nel frattempo, usa la ricerca utenti dal Command Center.
          </p>
        </div>
      </div>
    </RequireRole>
  );
}

/**
 * Admin Shared Games Index Page
 *
 * Placeholder page for the shared games catalog overview.
 * Links from Admin Hub → Games tab → Catalogo Giochi card.
 */

import type { Metadata } from 'next';

import { GamepadIcon } from 'lucide-react';
import Link from 'next/link';

import { RequireRole } from '@/components/auth/RequireRole';

export const metadata: Metadata = {
  title: 'Shared Games Catalog | Admin',
  description: 'Manage the shared games catalog',
};

export default function AdminSharedGamesPage() {
  return (
    <RequireRole allowedRoles={['Admin']}>
      <div className="space-y-6">
        <div>
          <h1 className="text-2xl font-bold tracking-tight text-foreground font-quicksand">
            Shared Games Catalog
          </h1>
          <p className="text-sm text-muted-foreground mt-1">
            Catalogo giochi condivisi della community
          </p>
        </div>
        <div className="flex flex-col items-center justify-center rounded-2xl border border-dashed border-border/50 bg-muted/20 p-12 text-center">
          <GamepadIcon className="h-12 w-12 text-muted-foreground/40 mb-4" />
          <h2 className="text-lg font-semibold text-foreground mb-2">Coming Soon</h2>
          <p className="text-sm text-muted-foreground max-w-md mb-6">
            La pagina indice del catalogo è in fase di sviluppo.
            Usa i link rapidi qui sotto per accedere alle sezioni disponibili.
          </p>
          <div className="flex flex-wrap gap-3 justify-center">
            <Link
              href="/admin/shared-games/new"
              className="px-4 py-2 rounded-lg bg-primary/10 text-primary text-sm font-medium hover:bg-primary/20 transition-colors"
            >
              Aggiungi Gioco
            </Link>
            <Link
              href="/admin/shared-games/pending-approvals"
              className="px-4 py-2 rounded-lg bg-amber-500/10 text-amber-600 dark:text-amber-400 text-sm font-medium hover:bg-amber-500/20 transition-colors"
            >
              Approvazioni Pendenti
            </Link>
            <Link
              href="/admin/shared-games/add-from-bgg"
              className="px-4 py-2 rounded-lg bg-blue-500/10 text-blue-600 dark:text-blue-400 text-sm font-medium hover:bg-blue-500/20 transition-colors"
            >
              Import da BGG
            </Link>
          </div>
        </div>
      </div>
    </RequireRole>
  );
}

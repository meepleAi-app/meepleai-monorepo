import { Suspense } from 'react';

import { type Metadata } from 'next';

import { PermissionsMatrix } from '@/components/admin/users/permissions-matrix';

export const metadata: Metadata = {
  title: 'Ruoli e Permessi',
  description: 'Gestisci ruoli utente e controllo accessi sulla piattaforma',
};

function CardSkeleton({ height = 'h-[180px]' }: { height?: string }) {
  return (
    <div
      className={`${height} bg-white/40 dark:bg-zinc-800/40 backdrop-blur-sm rounded-2xl border border-slate-200/60 dark:border-zinc-700/40 animate-pulse`}
    />
  );
}

export default function RolesPermissionsPage() {
  return (
    <div className="space-y-8">
      <div>
        <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
          Ruoli e Permessi
        </h1>
        <p className="text-sm text-muted-foreground mt-1">
          Gestisci ruoli utente e controllo accessi sulla piattaforma
        </p>
      </div>

      {/* Permissions Matrix */}
      <Suspense fallback={<CardSkeleton height="h-[600px]" />}>
        <PermissionsMatrix />
      </Suspense>
    </div>
  );
}

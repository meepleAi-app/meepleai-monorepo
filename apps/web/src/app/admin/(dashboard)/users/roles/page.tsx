/* eslint-disable local/no-hardcoded-color-utility -- text-white / button color on style-prop colored bg or admin-decorative inline gradient; DS-13a admin scope, mockup .e-bg pattern. Future: extract --admin-* token family (deferred to DS-15 audit). */
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
      className={`${height} bg-card/40 dark:bg-zinc-800/40 backdrop-blur-sm rounded-2xl border border-border/60 dark:border-zinc-700/40 animate-pulse`}
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

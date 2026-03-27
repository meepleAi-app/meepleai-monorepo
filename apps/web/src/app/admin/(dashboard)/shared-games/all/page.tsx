import { type Metadata } from 'next';

import { AllGamesClient } from './client';

export const metadata: Metadata = {
  title: 'All Games',
  description: 'Browse and manage the shared game catalog',
};

export default function AllGamesPage() {
  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div>
        <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
          Tutti i Giochi
        </h1>
        <p className="text-sm text-muted-foreground mt-1">
          Sfoglia e gestisci il catalogo dei giochi condivisi
        </p>
      </div>

      <AllGamesClient />
    </div>
  );
}

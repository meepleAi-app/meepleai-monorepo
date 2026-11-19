import { type Metadata } from 'next';

import { ShareRequestsList } from '@/components/admin/share-requests/ShareRequestsList';

export const metadata: Metadata = {
  title: 'Share Requests',
  description: 'Review the share requests and game proposals users send to the catalog',
};

interface ShareRequestsPageProps {
  searchParams: Promise<{ sort?: string }>;
}

// #5076: the review of user share requests, removed with the old admin on 2026-02-23 (a100dc687f) and restored
// with the approval of private-game proposals. The admin notification links here with `?sort=oldest`.
export default async function ShareRequestsPage({ searchParams }: ShareRequestsPageProps) {
  const { sort } = await searchParams;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
          Richieste di condivisione
        </h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Rivedi le richieste e le proposte di giochi che gli utenti mandano al catalogo. La coda
          mostra quelle aperte: in attesa, in revisione o con modifiche richieste.
        </p>
      </div>

      <ShareRequestsList oldestFirst={sort === 'oldest'} />
    </div>
  );
}

/**
 * Admin per-game Knowledge Base page (KB-01, KB-02, KB-10)
 *
 * Route: /admin/shared-games/[id]/knowledge-base
 * Shows indexed KB documents and per-game settings overrides.
 */

import { type Metadata } from 'next';

import { GameKbDocuments } from '@/components/admin/knowledge-base/game-kb-documents';
import { GameKbSettings } from '@/components/admin/knowledge-base/game-kb-settings';

interface GameKbPageProps {
  params: Promise<{ id: string }>;
}

export const metadata: Metadata = {
  title: 'Knowledge Base — Game Admin',
  description: 'Gestisci i documenti indicizzati e le impostazioni KB per questo gioco',
};

export default async function GameKbPage({ params }: GameKbPageProps) {
  const { id: gameId } = await params;

  return (
    <div className="space-y-8 p-6">
      <div>
        <h2 className="font-quicksand text-lg font-semibold mb-4">Documenti Knowledge Base</h2>
        <GameKbDocuments gameId={gameId} />
      </div>
      <div>
        <h2 className="font-quicksand text-lg font-semibold mb-4">Impostazioni KB</h2>
        <GameKbSettings gameId={gameId} />
      </div>
    </div>
  );
}

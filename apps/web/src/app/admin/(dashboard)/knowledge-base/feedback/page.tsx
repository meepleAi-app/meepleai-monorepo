'use client';

import { useState } from 'react';

import { KbFeedbackPanel } from '@/components/admin/knowledge-base/kb-feedback-panel';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';

export default function KbFeedbackPage() {
  const [gameId, setGameId] = useState('');

  return (
    <div className="space-y-6">
      <div>
        <h2 className="font-quicksand text-xl font-semibold text-foreground">Feedback KB Utenti</h2>
        <p className="text-sm text-muted-foreground mt-1">
          Revisiona i feedback thumbs up/down degli utenti sulle risposte KB
        </p>
      </div>

      <div className="space-y-2 max-w-sm">
        <Label htmlFor="game-id-input">Game ID</Label>
        <Input
          id="game-id-input"
          placeholder="Inserisci l'UUID del gioco..."
          value={gameId}
          onChange={e => setGameId(e.target.value)}
        />
      </div>

      {/^[0-9a-f-]{36}$/i.test(gameId) && <KbFeedbackPanel gameId={gameId} />}
    </div>
  );
}

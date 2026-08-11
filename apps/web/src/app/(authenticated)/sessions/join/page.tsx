/**
 * Join Session Page
 *
 * Issue #5041 — Sessions Redesign Phase 1
 *
 * Enter a 6-character session code to join an existing session.
 * Calls liveSessionsClient.getByCode() → redirects to /sessions/[id].
 */

'use client';

import { useState, useRef, useCallback } from 'react';

import { ArrowLeft, Loader2, Users } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { api } from '@/lib/api';

export default function JoinSessionPage() {
  const router = useRouter();
  const [code, setCode] = useState('');
  const [isJoining, setIsJoining] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  const handleJoin = useCallback(async () => {
    const trimmed = code.trim().toUpperCase();
    if (trimmed.length < 4) {
      setError('Il codice deve avere almeno 4 caratteri');
      return;
    }

    setIsJoining(true);
    setError(null);

    try {
      // First try LiveSession code
      const session = await api.liveSessions.getByCode(trimmed);
      router.push(`/sessions/${session.id}`);
    } catch {
      // Fallback: try SessionTracking join
      try {
        await api.sessionTracking.joinByCode(trimmed);
        const session = await api.sessionTracking.getByCode(trimmed);
        router.push(`/sessions/${session.id}`);
      } catch {
        setError('Sessione non trovata. Controlla il codice e riprova.');
        setIsJoining(false);
      }
    }
  }, [code, router]);

  const handleCodeChange = (value: string) => {
    // Allow only alphanumeric characters, auto-uppercase
    const cleaned = value
      .replace(/[^a-zA-Z0-9]/g, '')
      .toUpperCase()
      .slice(0, 8);
    setCode(cleaned);
    setError(null);
  };

  return (
    <div className="container mx-auto px-4 py-6">
      <div className="mx-auto max-w-sm space-y-6">
        {/* Header */}
        <div className="text-center space-y-2">
          <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-full bg-indigo-100 dark:bg-indigo-900/40">
            <Users className="h-7 w-7 text-indigo-600" />
          </div>
          <h1 className="text-xl font-bold font-quicksand">Unisciti a una Sessione</h1>
          <p className="text-sm text-muted-foreground">
            Inserisci il codice sessione condiviso dall&apos;organizzatore
          </p>
        </div>

        {/* Code Input */}
        <div className="space-y-3">
          <Input
            ref={inputRef}
            value={code}
            onChange={e => handleCodeChange(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && handleJoin()}
            placeholder="Es. K7X2M9"
            className="text-center text-2xl font-mono tracking-[0.3em] h-14"
            maxLength={8}
            autoFocus
          />

          {error && <p className="text-sm text-red-600 dark:text-red-400 text-center">{error}</p>}

          <Button
            onClick={handleJoin}
            disabled={code.length < 4 || isJoining}
            className="w-full bg-indigo-600 hover:bg-indigo-700"
          >
            {isJoining ? (
              <>
                <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                Connessione...
              </>
            ) : (
              'Unisciti'
            )}
          </Button>
        </div>

        {/* Back */}
        <div className="text-center">
          <Button variant="ghost" onClick={() => router.push('/sessions')}>
            <ArrowLeft className="h-4 w-4 mr-1" />
            Torna alle sessioni
          </Button>
        </div>
      </div>
    </div>
  );
}

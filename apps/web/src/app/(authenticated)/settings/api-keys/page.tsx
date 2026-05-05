/**
 * API Keys Settings Page (M6 follow-up — Fase 2.4.a)
 *
 * Lista chiavi API attive, creazione e revoca.
 * Source of truth: admin-mockups/design_files/settings.jsx ApiKeysPanel (line ~700).
 *
 * Backend: Issue #909 endpoints (GET/POST/DELETE /api/v1/api-keys).
 * Plaintext key visibile solo una volta alla creazione.
 */

'use client';

import { useState } from 'react';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';

import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { useToast } from '@/hooks/useToast';
import { api } from '@/lib/api';
import type { CreateApiKeyResponse } from '@/lib/api/schemas/auth.schemas';

const QUERY_KEY = ['user', 'current', 'api-keys'] as const;

export default function ApiKeysSettingsPage() {
  const router = useRouter();
  const { toast } = useToast();
  const queryClient = useQueryClient();

  const [isCreating, setIsCreating] = useState(false);
  const [newKeyName, setNewKeyName] = useState('');
  const [newKeyScopes, setNewKeyScopes] = useState('read');
  const [plaintextKey, setPlaintextKey] = useState<CreateApiKeyResponse | null>(null);

  const { data, isLoading } = useQuery({
    queryKey: QUERY_KEY,
    queryFn: () => api.auth.listApiKeys({ includeRevoked: false, pageSize: 50 }),
    staleTime: 30 * 1000,
  });

  const createMutation = useMutation({
    mutationFn: (request: { keyName: string; scopes: string }) => api.auth.createApiKey(request),
    onSuccess: data => {
      setPlaintextKey(data);
      setNewKeyName('');
      setNewKeyScopes('read');
      setIsCreating(false);
      queryClient.invalidateQueries({ queryKey: QUERY_KEY });
      toast({ title: 'Chiave creata', description: 'Copia il valore ora, non sarà più visibile.' });
    },
    onError: err => {
      toast({
        title: 'Errore creazione',
        description: err instanceof Error ? err.message : 'Impossibile creare la chiave.',
        variant: 'destructive',
      });
    },
  });

  const revokeMutation = useMutation({
    mutationFn: (keyId: string) => api.auth.revokeApiKey(keyId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEY });
      toast({ title: 'Chiave revocata', description: 'La chiave non è più valida.' });
    },
    onError: err => {
      toast({
        title: 'Errore revoca',
        description: err instanceof Error ? err.message : 'Impossibile revocare.',
        variant: 'destructive',
      });
    },
  });

  function handleCreate() {
    const trimmed = newKeyName.trim();
    if (trimmed.length < 3) {
      toast({
        title: 'Nome troppo corto',
        description: 'Minimo 3 caratteri.',
        variant: 'destructive',
      });
      return;
    }
    createMutation.mutate({ keyName: trimmed, scopes: newKeyScopes });
  }

  function handleRevoke(keyId: string, keyName: string) {
    if (
      !window.confirm(
        `Revocare la chiave "${keyName}"? Le applicazioni che la usano smetteranno di funzionare.`
      )
    ) {
      return;
    }
    revokeMutation.mutate(keyId);
  }

  async function handleCopyPlaintext() {
    if (!plaintextKey) return;
    try {
      await navigator.clipboard.writeText(plaintextKey.plaintextKey);
      toast({ title: 'Copiato', description: 'Chiave copiata negli appunti.' });
    } catch {
      toast({
        title: 'Copia fallita',
        description: 'Copia manualmente il valore.',
        variant: 'destructive',
      });
    }
  }

  const keys = data?.items ?? [];

  return (
    <div className="min-h-dvh bg-background">
      <div className="mx-auto max-w-2xl px-4 py-8">
        <header className="mb-8">
          <h1 className="text-3xl font-bold tracking-tight text-foreground">API Keys</h1>
          <p className="mt-2 text-sm text-muted-foreground">
            Gestisci le chiavi per accesso programmatico al tuo account.
          </p>
        </header>

        {plaintextKey && (
          <div
            className="mb-6 rounded-xl border p-5"
            style={{
              borderColor: 'hsl(var(--color-warning, 36 100% 50%))',
              backgroundColor: 'hsl(var(--color-warning, 36 100% 50%) / 0.1)',
            }}
          >
            <h2 className="text-sm font-semibold text-foreground">
              ⚠️ Chiave generata — copiala ora
            </h2>
            <p className="mt-1 text-xs text-muted-foreground">
              Questo valore non sarà più visibile. Salvalo in un posto sicuro.
            </p>
            <div className="mt-3 flex items-center gap-2">
              <code className="flex-1 overflow-x-auto rounded-md bg-background p-2 text-xs font-mono">
                {plaintextKey.plaintextKey}
              </code>
              <Button size="sm" onClick={handleCopyPlaintext}>
                Copia
              </Button>
            </div>
            <Button
              variant="ghost"
              size="sm"
              className="mt-3"
              onClick={() => setPlaintextKey(null)}
            >
              Ho salvato la chiave
            </Button>
          </div>
        )}

        {/* Create form */}
        <section className="mb-8 rounded-xl border border-border bg-card p-5">
          {!isCreating ? (
            <Button onClick={() => setIsCreating(true)}>+ Nuova chiave API</Button>
          ) : (
            <div className="space-y-4">
              <div className="space-y-2">
                <Label htmlFor="keyName">Nome chiave</Label>
                <Input
                  id="keyName"
                  value={newKeyName}
                  onChange={e => setNewKeyName(e.target.value)}
                  placeholder="es. Integrazione Zapier"
                  maxLength={100}
                  disabled={createMutation.isPending}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="keyScopes">Permessi</Label>
                <select
                  id="keyScopes"
                  value={newKeyScopes}
                  onChange={e => setNewKeyScopes(e.target.value)}
                  disabled={createMutation.isPending}
                  className="w-full rounded-lg border border-border bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/40"
                >
                  <option value="read">Lettura</option>
                  <option value="read,write">Lettura e scrittura</option>
                </select>
              </div>
              <div className="flex items-center gap-2">
                <Button onClick={handleCreate} disabled={createMutation.isPending}>
                  {createMutation.isPending ? 'Creazione...' : 'Crea chiave'}
                </Button>
                <Button
                  variant="ghost"
                  onClick={() => setIsCreating(false)}
                  disabled={createMutation.isPending}
                >
                  Annulla
                </Button>
              </div>
            </div>
          )}
        </section>

        {/* Keys list */}
        <section>
          <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-muted-foreground">
            Chiavi attive
          </h2>
          {isLoading ? (
            <div className="rounded-xl border border-border bg-card p-8 text-center text-sm text-muted-foreground">
              Caricamento...
            </div>
          ) : keys.length === 0 ? (
            <div className="rounded-xl border border-border bg-card p-8 text-center text-sm text-muted-foreground">
              Nessuna chiave API. Crea la tua prima chiave qui sopra.
            </div>
          ) : (
            <ul className="space-y-2">
              {keys.map(key => (
                <li
                  key={key.id}
                  className="flex items-center justify-between gap-4 rounded-xl border border-border bg-card p-4"
                >
                  <div className="min-w-0 flex-1">
                    <div className="font-medium text-foreground">{key.keyName}</div>
                    <div className="mt-0.5 flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
                      <code className="rounded bg-muted px-1.5 py-0.5 font-mono">
                        {key.keyPrefix}…
                      </code>
                      <span>Permessi: {key.scopes}</span>
                      <span>·</span>
                      <span>Creata: {new Date(key.createdAt).toLocaleDateString('it-IT')}</span>
                      {key.lastUsedAt && (
                        <>
                          <span>·</span>
                          <span>
                            Ultimo uso: {new Date(key.lastUsedAt).toLocaleDateString('it-IT')}
                          </span>
                        </>
                      )}
                    </div>
                  </div>
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => handleRevoke(key.id, key.keyName)}
                    disabled={revokeMutation.isPending}
                    className="text-destructive hover:text-destructive"
                  >
                    Revoca
                  </Button>
                </li>
              ))}
            </ul>
          )}
        </section>

        {/* Back */}
        <div className="mt-8 border-t border-border pt-6">
          <button
            type="button"
            onClick={() => router.push('/settings')}
            className="text-sm text-muted-foreground transition-colors hover:text-foreground"
          >
            ← Impostazioni
          </button>
        </div>
      </div>
    </div>
  );
}

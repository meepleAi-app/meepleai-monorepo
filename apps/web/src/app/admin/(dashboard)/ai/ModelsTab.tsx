'use client';

import { useState, useEffect } from 'react';

import { Cpu, Star, Settings } from 'lucide-react';

import { AdminHubEmptyState } from '@/components/admin/layout/AdminHubEmptyState';
import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';
import type { AiModelDto } from '@/lib/api/schemas';

export function ModelsTab() {
  const [models, setModels] = useState<AiModelDto[]>([]);
  const [loading, setLoading] = useState(true);
  // 🔴 #4059 — l'errore deve arrivare a schermo, non solo in console.
  //
  // Qui c'era `.catch(() => {})`: lo schema del client rifiutava la risposta (il backend
  // mandava `models`/`totalCount` dove `PagedAiModelsSchema` attende `items`/`total`) e questo
  // catch cancellava l'eccezione. Risultato: un elenco vuoto identico a «non ci sono modelli»,
  // mentre la rotta rispondeva **200 con 6 modelli**. Misurato in Chromium su quattro pagine
  // admin: errore di schema in console su tutte e quattro, nessun errore a schermo su nessuna.
  //
  // Il `(data as Record<string, unknown>)?.items` con controllo `Array.isArray` era la stessa
  // storia dall'altro lato: una difesa aggiunta perche' la forma non combaciava, che rendeva il
  // disallineamento indistinguibile da un elenco legittimamente vuoto. Ora il tipo e' quello
  // che lo schema garantisce, quindi il cast non serve: se la forma cambia, a protestare e' la
  // validazione — che e' il punto in cui ha senso accorgersene.
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api.admin
      .getAiModels()
      .then(data => {
        setModels(data.models);
        setError(null);
      })
      .catch((err: unknown) => {
        setError(err instanceof Error ? err.message : 'Errore nel caricamento dei modelli');
      })
      .finally(() => setLoading(false));
  }, []);

  const handleSetPrimary = async (modelId: string) => {
    try {
      await api.admin.setPrimaryModel({ modelId });
      const data = await api.admin.getAiModels();
      setModels(data.models);
      setError(null);
    } catch (err: unknown) {
      setError(
        err instanceof Error ? err.message : 'Errore durante impostazione del modello primario'
      );
    }
  };

  return (
    <div className="space-y-5">
      <div>
        <h2 className="font-quicksand text-lg font-semibold tracking-tight text-foreground">
          AI Models
        </h2>
        <p className="text-sm text-muted-foreground mt-0.5">
          Configure LLM providers, routing rules, and cost tracking.
        </p>
      </div>

      {loading ? (
        <div className="grid gap-3 sm:grid-cols-2">
          {[1, 2, 3, 4].map(i => (
            <div key={i} className="h-28 rounded-xl bg-card/40 animate-pulse" />
          ))}
        </div>
      ) : error ? (
        /*
          #4059 — il ramo che mancava. Senza questo, un errore di caricamento e un elenco
          legittimamente vuoto sono lo STESSO schermo, ed e' il motivo per cui il
          disallineamento di schema e' sopravvissuto: la console lo diceva, la pagina no.
          Token semantici e non `bg-red-*`: `local/no-hardcoded-color-utility` e' a `error`.
        */
        <div
          role="alert"
          className="rounded-xl border border-destructive/30 bg-destructive/10 p-4"
          data-testid="models-tab-error"
        >
          <p className="text-sm font-medium text-destructive">Impossibile caricare i modelli AI.</p>
          <p className="mt-1 text-xs text-muted-foreground">{error}</p>
        </div>
      ) : models.length > 0 ? (
        <div className="grid gap-3 sm:grid-cols-2">
          {models.map(m => (
            <div
              key={m.id}
              className="relative rounded-xl border border-border/60 bg-card/70 backdrop-blur-md p-3 sm:p-4"
            >
              {m.isPrimary && (
                <span className="absolute top-3 right-3 inline-flex items-center gap-1 rounded-full bg-[hsl(var(--c-warning)/0.15)] px-2 py-0.5 text-[10px] font-medium text-[hsl(var(--c-warning-ink))]">
                  <Star className="h-3 w-3" /> Primary
                </span>
              )}
              <div className="flex items-start gap-3">
                <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-primary/10">
                  <Cpu className="h-4 w-4 text-primary" />
                </div>
                <div className="min-w-0 flex-1">
                  <p className="text-sm font-medium text-foreground truncate pr-16 sm:pr-20">
                    {m.displayName}
                  </p>
                  <p className="text-xs text-muted-foreground mt-0.5 truncate">
                    {m.provider} · {m.modelId}
                  </p>
                  <div className="mt-2 flex items-center gap-2">
                    {/* #4093: il backend manda `isActive` booleano, non un enum a tre valori.
                        Lo stato "deprecated" che questo badge sapeva mostrare non esiste nel
                        contratto reale: due stati, non tre. */}
                    <span
                      className={`inline-block h-1.5 w-1.5 rounded-full ${
                        m.isActive ? 'bg-[hsl(var(--c-success))]' : 'bg-muted-foreground/40'
                      }`}
                    />
                    <span className="text-[10px] text-muted-foreground">
                      {m.isActive ? 'attivo' : 'inattivo'}
                    </span>
                  </div>
                </div>
              </div>
              <div className="mt-3 flex items-center gap-2">
                {!m.isPrimary && (
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => handleSetPrimary(m.id)}
                    className="text-xs"
                  >
                    <Star className="mr-1 h-3 w-3" /> Set Primary
                  </Button>
                )}
                <Button variant="ghost" size="sm" className="text-xs">
                  <Settings className="mr-1 h-3 w-3" /> Configure
                </Button>
              </div>
            </div>
          ))}
        </div>
      ) : (
        <AdminHubEmptyState
          icon={<Cpu />}
          title="No AI models configured"
          description="Add and configure LLM providers to enable AI-powered features."
        />
      )}
    </div>
  );
}

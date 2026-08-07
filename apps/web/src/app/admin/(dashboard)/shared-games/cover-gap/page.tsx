/**
 * Admin — Cover gap (#3590).
 *
 * Elenca i giochi del catalogo **senza alcuna cover**, raggruppati per la causa che impedisce
 * alla pipeline cover-da-PDF di coprirli.
 *
 * Perché esiste: risolvere questi casi era già possibile (il picker manuale da URL c'è da #3545),
 * ma non c'era modo di TROVARLI — l'unico accesso all'editor era l'affordance a matita in hover
 * sulla griglia pubblica, quindi bisognava scorrerla a occhio. Questa pagina chiude quel buco.
 *
 * Fuori scope: l'editing della cover. Ogni riga porta al gioco su /shared-games, dove vive
 * l'editor esistente (`AdminCoverEditAffordance`) — non duplicarlo qui.
 */

'use client';

import { useState } from 'react';

import { useCoverGap } from '@/hooks/admin/useCoverGap';
import type { CoverGapCause } from '@/lib/api/schemas/admin/admin-cover.schemas';

const CAUSE_FILTERS: { value: CoverGapCause | ''; label: string }[] = [
  { value: '', label: 'Tutte le cause' },
  { value: 'no_source', label: 'Nessuna sorgente' },
  { value: 'heuristic_rejected', label: 'Euristica ha rifiutato' },
  { value: 'pdf_too_large', label: 'PDF troppo grande' },
  { value: 'other', label: 'Altro' },
];

const CAUSE_LABELS: Record<CoverGapCause, string> = {
  no_source: 'Nessuna sorgente',
  heuristic_rejected: 'Euristica ha rifiutato',
  pdf_too_large: 'PDF troppo grande',
  other: 'Altro',
};

/** Spiegazione operativa: cosa può fare l'admin per ciascun gruppo. */
const CAUSE_HINTS: Record<CoverGapCause, string> = {
  no_source: 'Nessun PDF collegato e nessuna immagine libera: serve una cover manuale da URL.',
  heuristic_rejected:
    'Esito corretto — nessuna pagina del rulebook è una cover accettabile. Serve una cover manuale.',
  pdf_too_large:
    "Il PDF eccede il limite del servizio di estrazione. Verificare che l'immagine di unstructured-service sia aggiornata prima di intervenire a mano.",
  other: 'PDF ancora in lavorazione o fallimento non classificato: ricontrollare più tardi.',
};

function formatMb(bytes: number | null): string {
  if (bytes === null) return '—';
  return `${Math.round(bytes / 1_048_576)} MB`;
}

export default function CoverGapPage() {
  const [cause, setCause] = useState<CoverGapCause | ''>('');
  const { data, isLoading, isError, error } = useCoverGap(cause || undefined);

  const items = data?.items ?? [];

  return (
    <div className="space-y-6 p-6">
      <header className="space-y-2">
        <h1 className="text-2xl font-semibold text-foreground">Giochi senza cover</h1>
        <p className="text-sm text-muted-foreground">
          Giochi del catalogo privi di qualunque cover (PDF, BGG, Wikidata, manuale), con la causa
          per cui la pipeline cover-da-PDF non li copre. Per risolvere, apri il gioco e usa
          l&apos;editor cover.
        </p>
      </header>

      <div className="flex flex-wrap items-center gap-3">
        <label htmlFor="cause-filter" className="text-sm text-muted-foreground">
          Causa
        </label>
        <select
          id="cause-filter"
          value={cause}
          onChange={e => setCause(e.target.value as CoverGapCause | '')}
          className="rounded-md border border-border bg-card px-3 py-1.5 text-sm text-foreground"
        >
          {CAUSE_FILTERS.map(f => (
            <option key={f.value || 'all'} value={f.value}>
              {f.label}
            </option>
          ))}
        </select>

        {data && (
          <span className="text-sm text-muted-foreground">
            {data.total} {data.total === 1 ? 'gioco' : 'giochi'} senza cover
          </span>
        )}
      </div>

      {isLoading && <p className="text-sm text-muted-foreground">Caricamento…</p>}

      {isError && (
        <p role="alert" className="text-sm text-destructive">
          Impossibile caricare l&apos;elenco: {error?.message ?? 'errore sconosciuto'}
        </p>
      )}

      {!isLoading && !isError && items.length === 0 && (
        <p className="text-sm text-muted-foreground">
          Nessun gioco senza cover per il filtro selezionato.
        </p>
      )}

      {items.length > 0 && (
        <div className="overflow-x-auto">
          <table className="w-full min-w-[720px] border-collapse text-sm">
            <thead>
              <tr className="border-b border-border text-left text-muted-foreground">
                <th scope="col" className="py-2 pr-4 font-medium">
                  Gioco
                </th>
                <th scope="col" className="py-2 pr-4 font-medium">
                  Causa
                </th>
                <th scope="col" className="py-2 pr-4 font-medium">
                  PDF
                </th>
                <th scope="col" className="py-2 pr-4 font-medium">
                  Dimensione
                </th>
                <th scope="col" className="py-2 font-medium">
                  Azione
                </th>
              </tr>
            </thead>
            <tbody>
              {items.map(game => (
                <tr key={game.gameId} className="border-b border-border">
                  <td className="py-2 pr-4 text-foreground">{game.title}</td>
                  <td className="py-2 pr-4">
                    <span className="text-foreground">{CAUSE_LABELS[game.cause]}</span>
                    <span className="block text-xs text-muted-foreground">
                      {CAUSE_HINTS[game.cause]}
                    </span>
                  </td>
                  <td className="py-2 pr-4 text-muted-foreground">{game.pdfFileName ?? '—'}</td>
                  <td className="py-2 pr-4 text-muted-foreground">{formatMb(game.pdfSizeBytes)}</td>
                  <td className="py-2">
                    <a
                      href={`/shared-games?highlight=${encodeURIComponent(game.gameId)}`}
                      className="text-primary underline underline-offset-2"
                    >
                      Apri nel catalogo
                    </a>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

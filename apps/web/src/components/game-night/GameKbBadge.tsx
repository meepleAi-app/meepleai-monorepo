import type { JSX } from 'react';

import { Sparkles, FileText, AlertTriangle } from 'lucide-react';

import { useGameKbStatus } from '@/lib/domain-hooks/useGameKbStatus';

interface GameKbBadgeProps {
  isIndexed: boolean;
  isLoading?: boolean;
}

interface GameKbWarningProps {
  gameId: string;
}

/**
 * Badge che indica se un gioco ha un PDF indicizzato (RAG agent disponibile).
 * - isIndexed=true → "AI pronto" (verde, icon sparkles)
 * - isIndexed=false → "Solo manuale" (grigio, icon file)
 * - isLoading → null (nessun render)
 */
export function GameKbBadge({
  isIndexed,
  isLoading = false,
}: GameKbBadgeProps): JSX.Element | null {
  if (isLoading) return null;

  if (isIndexed) {
    return (
      <span
        data-testid="game-kb-badge"
        data-indexed="true"
        className="inline-flex items-center gap-1 rounded-full bg-emerald-50 px-2 py-0.5 text-xs font-nunito font-medium text-emerald-700 border border-emerald-200"
      >
        <Sparkles className="h-3 w-3" aria-hidden="true" />
        AI pronto
      </span>
    );
  }

  return (
    <span
      data-testid="game-kb-badge"
      data-indexed="false"
      className="inline-flex items-center gap-1 rounded-full bg-gray-50 px-2 py-0.5 text-xs font-nunito font-medium text-gray-600 border border-gray-200"
    >
      <FileText className="h-3 w-3" aria-hidden="true" />
      Solo manuale
    </span>
  );
}

/**
 * Warning non bloccante mostrato quando l'utente seleziona un gioco senza PDF
 * indicizzato. Informa che l'agente AI non è disponibile ma permette comunque di
 * proseguire usando gli strumenti manuali (Game Night MVP hardening F1).
 *
 * Nota: `useGameKbStatus` viene chiamato al top level di questo componente per
 * rispettare le Rules of Hooks — per questo esiste un componente dedicato anziché
 * una logica inline nel wizard.
 */
export function GameKbWarning({ gameId }: GameKbWarningProps): JSX.Element | null {
  const kb = useGameKbStatus(gameId);
  if (kb.isLoading || kb.isIndexed) return null;
  return (
    <div
      role="alert"
      data-testid="kb-warning"
      className="mt-3 flex items-start gap-2 rounded-lg border border-amber-200 bg-amber-50 p-3"
    >
      <AlertTriangle className="mt-0.5 h-4 w-4 flex-none text-amber-600" aria-hidden="true" />
      <p className="text-xs font-nunito text-amber-900">
        <strong>Agente AI non disponibile</strong> — il PDF di questo gioco non è ancora
        indicizzato. Puoi comunque iniziare la sessione e usare gli strumenti manuali.
      </p>
    </div>
  );
}

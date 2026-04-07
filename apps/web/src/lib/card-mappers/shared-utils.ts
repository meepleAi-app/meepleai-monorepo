import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card';
import type { LinkedEntityInfo } from '@/types/linked-entity';

/**
 * Formatta una durata in minuti in una stringa leggibile.
 * 45 → "45min", 60 → "1h", 90 → "1h30min", 120 → "2h"
 */
export function formatPlayTime(minutes: number): string {
  if (minutes < 60) return `${minutes}min`;
  const h = Math.floor(minutes / 60);
  const m = minutes % 60;
  if (m === 0) return `${h}h`;
  return `${h}h${m}min`;
}

const MECH_ICON_MAP: Record<string, string> = {
  'worker placement': '\u2699\uFE0F',
  'deck building': '\uD83C\uDCCF',
  cooperative: '\uD83E\uDD1D',
  'area control': '\uD83D\uDDFA\uFE0F',
  auction: '\uD83D\uDCB0',
  dice: '\uD83C\uDFB2',
};

/**
 * Mappa un meccanismo di gioco a un'emoji. Case-insensitive.
 * Restituisce undefined per meccanismi sconosciuti o input mancante.
 */
export function mechToIcon(mechanism: string | undefined): string | undefined {
  if (!mechanism) return undefined;
  return MECH_ICON_MAP[mechanism.toLowerCase()];
}

const RANK_ICON_MAP: Record<string, string> = {
  master: '\uD83D\uDC51',
  expert: '\u2B50',
  veteran: '\uD83C\uDF96\uFE0F',
};

/**
 * Mappa un rank giocatore a un'emoji.
 * Restituisce undefined per rank sconosciuti o input mancante.
 */
export function rankToIcon(rank: string | undefined): string | undefined {
  if (!rank) return undefined;
  return RANK_ICON_MAP[rank.toLowerCase()];
}

/**
 * Costruisce un array LinkedEntityInfo filtrando i conteggi a zero.
 */
export function buildLinkedEntities(counts: {
  agentCount?: number;
  kbCount?: number;
  sessionCount?: number;
  gameCount?: number;
  chatCount?: number;
}): LinkedEntityInfo[] {
  const pairs: Array<[MeepleEntityType, number | undefined]> = [
    ['game', counts.gameCount],
    ['agent', counts.agentCount],
    ['kb', counts.kbCount],
    ['session', counts.sessionCount],
    ['chat', counts.chatCount],
  ];
  return pairs
    .filter(
      (pair): pair is [MeepleEntityType, number] => typeof pair[1] === 'number' && pair[1] > 0
    )
    .map(([entityType, count]) => ({ entityType, count }));
}

type StateLabel = { text: string; variant: 'success' | 'warning' | 'error' | 'info' };

/**
 * Mappa lo status di una sessione (stringa backend PascalCase) a un oggetto stateLabel.
 */
export function sessionStatusToLabel(status: string): StateLabel {
  switch (status) {
    case 'InProgress':
      return { text: 'Live', variant: 'success' };
    case 'Paused':
      return { text: 'Pausa', variant: 'warning' };
    case 'Completed':
      return { text: 'Completata', variant: 'info' };
    case 'Setup':
      return { text: 'Impostazione', variant: 'info' };
    default:
      return { text: status, variant: 'info' };
  }
}

/**
 * Mappa il processingState di un documento PDF a un oggetto stateLabel.
 */
export function processingStateToLabel(state: string): StateLabel {
  switch (state) {
    case 'Completed':
      return { text: 'Indicizzato', variant: 'success' };
    case 'Failed':
      return { text: 'Errore', variant: 'error' };
    case 'Pending':
      return { text: 'In Attesa', variant: 'info' };
    case 'Uploading':
    case 'Extracting':
    case 'Chunking':
    case 'Embedding':
    case 'Indexing':
      return { text: 'In Elaborazione', variant: 'warning' };
    default:
      return { text: state, variant: 'info' };
  }
}

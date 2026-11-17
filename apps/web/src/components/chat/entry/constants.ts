/**
 * Chat entry constants — quick-start suggestions
 *
 * #5001 (ADR-098 §5): every suggestion is a question about the rules. «Strategia per {game}» went, because
 * strategy is not in the rulebook; so did «Risolvi una disputa», because arbitration is not the chat's — it lacks
 * the game state and what comes next — and lives in the live session (ADR-098 §2, #4171).
 */

import type { QuickStartSuggestion } from './types';

const RULE_SUGGESTIONS: QuickStartSuggestion[] = [
  {
    label: 'Ho una domanda sulle regole',
    message: 'Ho una domanda sulle regole di un gioco',
    promptType: 'general',
  },
  {
    label: 'Setup della partita',
    message: 'Come funziona il setup di questa partita?',
    promptType: 'setup',
  },
  {
    label: 'Come si vince',
    message: 'Come si vince la partita?',
    promptType: 'general',
  },
];

export function getQuickStartSuggestions(gameName?: string): QuickStartSuggestion[] {
  if (!gameName) {
    return RULE_SUGGESTIONS;
  }
  return [
    { label: `Come si gioca a ${gameName}`, message: `Come si gioca a ${gameName}?` },
    { label: `Regole di ${gameName}`, message: `Spiegami le regole di ${gameName}` },
    {
      label: `Setup di ${gameName}`,
      message: `Come si prepara il tavolo per ${gameName}?`,
    },
  ];
}

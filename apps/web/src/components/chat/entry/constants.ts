/**
 * Chat entry constants — quick-start suggestions
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
    label: 'Risolvi una disputa',
    message: "C'è una disputa al tavolo — aiutami a risolverla",
    promptType: 'rule_dispute',
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
      label: `Strategia per ${gameName}`,
      message: `Qual è la migliore strategia per ${gameName}?`,
    },
  ];
}

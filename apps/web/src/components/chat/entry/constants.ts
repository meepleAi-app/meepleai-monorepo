/**
 * Chat entry constants — system agents and quick-start suggestions
 */

import type { AgentOption, QuickStartSuggestion } from './types';

export const DEFAULT_AGENTS: AgentOption[] = [
  {
    id: 'auto',
    name: 'Auto',
    type: 'auto',
    description: 'Scelta automatica in base alla domanda',
    icon: '🤖',
  },
  {
    id: 'tutor',
    name: 'Tutor',
    type: 'qa',
    description: 'Spiega regole e meccaniche',
    icon: '📚',
  },
  {
    id: 'arbitro',
    name: 'Arbitro',
    type: 'rules',
    description: 'Risolve dubbi e dispute',
    icon: '⚖️',
  },
  {
    id: 'stratega',
    name: 'Stratega',
    type: 'strategy',
    description: 'Consigli strategici e tattici',
    icon: '🎯',
  },
  {
    id: 'narratore',
    name: 'Narratore',
    type: 'narrative',
    description: 'Racconta ambientazione, lore e atmosfera',
    icon: '📖',
  },
];

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

/**
 * Demo Agent Typology Suggestions
 * Issue #4781: Suggested agent types per demo game
 *
 * Maps each demo game to the agent typologies most useful
 * for that game, with reasoning for why each typology fits.
 */

import { DEMO_GAME_IDS } from './demo-games';

// ============================================================================
// Types
// ============================================================================

export interface AgentSuggestion {
  /** Agent typology name (matches agent-welcome.ts keys) */
  typology: string;
  /** Why this typology is recommended for this game */
  reason: string;
  /** Whether this is the primary recommendation */
  isPrimary: boolean;
}

export interface GameAgentSuggestions {
  gameId: string;
  gameTitle: string;
  suggestions: AgentSuggestion[];
}

// ============================================================================
// Suggestions per Game
// ============================================================================

export const DEMO_AGENT_SUGGESTIONS: GameAgentSuggestions[] = [
  {
    gameId: DEMO_GAME_IDS.catan,
    gameTitle: 'Catan',
    suggestions: [
      {
        typology: 'Tutor',
        reason: 'Ideale per spiegare le regole di commercio, setup e meccaniche base',
        isPrimary: true,
      },
      {
        typology: 'Stratega',
        reason: 'Ottimo per analizzare posizionamento iniziale e strategie di scambio',
        isPrimary: false,
      },
      {
        typology: 'Arbitro',
        reason: 'Utile per risolvere dispute su commerci e regole del brigante',
        isPrimary: false,
      },
    ],
  },
  {
    gameId: DEMO_GAME_IDS.descent,
    gameTitle: 'Descent: Leggende delle Tenebre',
    suggestions: [
      {
        typology: 'Arbitro',
        reason: 'Essenziale per chiarire regole di combattimento e interazioni complesse',
        isPrimary: true,
      },
      {
        typology: 'Narratore',
        reason: 'Perfetto per arricchire la narrazione e l\'immersione nella campagna',
        isPrimary: false,
      },
      {
        typology: 'Tutor',
        reason: 'Aiuta a comprendere le meccaniche per nuovi giocatori',
        isPrimary: false,
      },
    ],
  },
  {
    gameId: DEMO_GAME_IDS.ticketToRide,
    gameTitle: 'Ticket to Ride',
    suggestions: [
      {
        typology: 'Tutor',
        reason: 'Perfetto per insegnare le regole semplici ai principianti',
        isPrimary: true,
      },
      {
        typology: 'Stratega',
        reason: 'Utile per pianificare percorsi ottimali e gestire le destinazioni',
        isPrimary: false,
      },
    ],
  },
  {
    gameId: DEMO_GAME_IDS.pandemic,
    gameTitle: 'Pandemic',
    suggestions: [
      {
        typology: 'Stratega',
        reason: 'Fondamentale per coordinare le azioni del team e prevenire focolai',
        isPrimary: true,
      },
      {
        typology: 'Tutor',
        reason: 'Utile per spiegare ruoli, azioni disponibili e fasi del turno',
        isPrimary: false,
      },
      {
        typology: 'Arbitro',
        reason: 'Aiuta a chiarire interazioni tra epidemie e abilità dei ruoli',
        isPrimary: false,
      },
    ],
  },
];

// ============================================================================
// Helpers
// ============================================================================

/** Get agent suggestions for a specific game */
export function getAgentSuggestionsForGame(gameId: string): GameAgentSuggestions | undefined {
  return DEMO_AGENT_SUGGESTIONS.find(s => s.gameId === gameId);
}

/** Get the primary suggested typology for a game */
export function getPrimarySuggestion(gameId: string): string | undefined {
  const suggestions = getAgentSuggestionsForGame(gameId);
  return suggestions?.suggestions.find(s => s.isPrimary)?.typology;
}

/**
 * Agent Welcome Messages Configuration
 * Issue #4780: Welcome message + auto-redirect post agent creation
 *
 * Defines personalized welcome messages, capabilities, and follow-up
 * questions per agent typology.
 */

// ============================================================================
// Types
// ============================================================================

export interface AgentWelcomeConfig {
  /** Greeting message template (use {game} for game name) */
  greeting: string;
  /** List of agent capabilities */
  capabilities: string[];
  /** Suggested follow-up questions (use {game} for game name) */
  followUpQuestions: string[];
}

// ============================================================================
// Welcome Messages by Typology
// ============================================================================

const WELCOME_CONFIGS: Record<string, AgentWelcomeConfig> = {
  Tutor: {
    greeting:
      'Ciao! Ho studiato il regolamento di {game} e posso aiutarti con regole, setup e strategie. Chiedimi qualsiasi cosa!',
    capabilities: [
      'Spiegare le regole del gioco passo dopo passo',
      'Aiutarti con il setup iniziale',
      'Chiarire situazioni ambigue durante la partita',
      'Suggerire strategie per principianti',
    ],
    followUpQuestions: [
      'Come si prepara il tavolo per {game}?',
      'Quali sono le regole base di {game}?',
      'Quali errori comuni devo evitare?',
    ],
  },
  Arbitro: {
    greeting:
      'Sono il tuo arbitro per {game}. Descrivimi la situazione e risolverò la disputa in modo imparziale.',
    capabilities: [
      'Risolvere dispute sulle regole',
      'Chiarire interazioni tra carte o meccaniche',
      'Giudicare situazioni ambigue',
      'Citare le regole ufficiali pertinenti',
    ],
    followUpQuestions: [
      'Abbiamo un dubbio su una regola di {game}, puoi aiutarci?',
      'Come funziona esattamente questa meccanica?',
      'Due regole sembrano in conflitto, quale prevale?',
    ],
  },
  Stratega: {
    greeting:
      'Ciao stratega! Sono pronto ad analizzare le tue mosse a {game}. Raccontami la situazione e troveremo la strategia migliore.',
    capabilities: [
      'Analizzare posizioni e suggerire mosse ottimali',
      'Consigliare strategie in base alla fase di gioco',
      'Valutare rischi e opportunità',
      'Suggerire counter-strategie contro avversari',
    ],
    followUpQuestions: [
      'Qual è la strategia migliore per iniziare a {game}?',
      'Come posso migliorare la mia posizione in questa fase?',
      'Quali sono le strategie avanzate per {game}?',
    ],
  },
  Narratore: {
    greeting:
      'Benvenuto nel mondo di {game}! Lascia che ti racconti la storia e l\'ambientazione di questo gioco straordinario.',
    capabilities: [
      'Raccontare la lore e l\'ambientazione del gioco',
      'Creare atmosfera durante le partite',
      'Descrivere scenari e personaggi',
      'Rendere l\'esperienza di gioco più immersiva',
    ],
    followUpQuestions: [
      'Raccontami l\'ambientazione di {game}',
      'Chi sono i personaggi principali?',
      'Qual è la storia dietro questo gioco?',
    ],
  },
};

const DEFAULT_WELCOME: AgentWelcomeConfig = {
  greeting:
    'Ciao! Sono il tuo assistente per {game}. Posso aiutarti con regole, strategie e molto altro.',
  capabilities: [
    'Rispondere a domande sulle regole',
    'Suggerire strategie di gioco',
    'Aiutarti con il setup',
    'Chiarire situazioni di gioco',
  ],
  followUpQuestions: [
    'Come si gioca a {game}?',
    'Quali sono le regole principali?',
    'Hai qualche consiglio per iniziare?',
  ],
};

// ============================================================================
// Public API
// ============================================================================

/**
 * Get welcome configuration for a given agent typology.
 * Returns default config if typology is not recognized.
 */
export function getAgentWelcome(typology: string | null | undefined): AgentWelcomeConfig {
  if (!typology) return DEFAULT_WELCOME;
  return WELCOME_CONFIGS[typology] ?? DEFAULT_WELCOME;
}

/**
 * Build a complete welcome message string with capabilities list.
 */
export function buildWelcomeMessage(
  typology: string | null | undefined,
  gameName: string
): string {
  const config = getAgentWelcome(typology);
  const greeting = config.greeting.replace(/{game}/g, gameName);
  const capabilities = config.capabilities
    .map(c => `• ${c}`)
    .join('\n');

  return `${greeting}\n\n**Cosa posso fare:**\n${capabilities}`;
}

/**
 * Get follow-up questions for a given typology, with game name substituted.
 */
export function getWelcomeFollowUpQuestions(
  typology: string | null | undefined,
  gameName: string
): string[] {
  const config = getAgentWelcome(typology);
  return config.followUpQuestions.map(q => q.replace(/{game}/g, gameName));
}

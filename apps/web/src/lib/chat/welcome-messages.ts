/**
 * Welcome Message Generator
 * Issue #4780: Chat Welcome Message + Auto-Redirect
 *
 * Generates personalized welcome messages based on agent typology and game.
 * Used when a new chat thread is opened after agent creation.
 */

export interface WelcomeMessage {
  /** Welcome text from the agent */
  content: string;
  /** Suggested follow-up questions (3) */
  followUpQuestions: string[];
}

const TYPOLOGY_WELCOME: Record<string, {
  greeting: (gameName: string) => string;
  capabilities: string[];
  questions: (gameName: string) => string[];
}> = {
  Tutor: {
    greeting: (gameName) =>
      `Ciao! Sono il tuo Tutor per **${gameName}**. Ho studiato il regolamento e posso aiutarti con regole, setup e strategie di gioco.`,
    capabilities: [
      'Spiegare le regole passo dopo passo',
      'Aiutarti con il setup iniziale',
      'Chiarire situazioni ambigue',
      'Suggerire strategie per principianti',
    ],
    questions: (gameName) => [
      `Come si prepara il tavolo per ${gameName}?`,
      `Quali sono le regole base di ${gameName}?`,
      `Quali errori evitare nelle prime partite?`,
    ],
  },
  Arbitro: {
    greeting: (gameName) =>
      `Sono il tuo Arbitro per **${gameName}**. Descrivimi la situazione e risolverò la disputa con precisione e imparzialità.`,
    capabilities: [
      'Risolvere dispute sulle regole',
      'Chiarire interazioni tra carte/meccaniche',
      'Giudicare casi limite',
      'Garantire il fair play',
    ],
    questions: (gameName) => [
      `Cosa succede in caso di pareggio in ${gameName}?`,
      `Come si risolve un conflitto tra due abilità?`,
      `Qual è la regola più fraintesa di ${gameName}?`,
    ],
  },
  Stratega: {
    greeting: (gameName) =>
      `Ciao stratega! Sono pronto ad analizzare le tue mosse a **${gameName}** e suggerirti le tattiche migliori.`,
    capabilities: [
      'Analizzare posizioni di gioco',
      'Suggerire mosse ottimali',
      'Valutare strategie a lungo termine',
      'Consigliare contromisure agli avversari',
    ],
    questions: (gameName) => [
      `Qual è la strategia migliore per iniziare ${gameName}?`,
      `Come contrastare un avversario aggressivo?`,
      `Quali sono le combo più forti di ${gameName}?`,
    ],
  },
  Narratore: {
    greeting: (gameName) =>
      `Benvenuto nel mondo di **${gameName}**! Lascia che ti racconti la storia di questa avventura e renda ogni partita un'esperienza unica.`,
    capabilities: [
      'Creare atmosfera immersiva',
      'Raccontare la storia della partita',
      'Descrivere scenari e ambientazioni',
      'Aggiungere colore narrativo alle azioni',
    ],
    questions: (gameName) => [
      `Raccontami l'ambientazione di ${gameName}`,
      `Come si inserisce la mia mossa nella storia?`,
      `Descrivi il mondo di ${gameName} come un racconto`,
    ],
  },
};

const DEFAULT_WELCOME = {
  greeting: (gameName: string) =>
    `Ciao! Sono il tuo assistente esperto di **${gameName}**. Posso aiutarti con regole, strategie e consigli di gioco.`,
  capabilities: [
    'Spiegare le regole del gioco',
    'Suggerire strategie',
    'Risolvere dubbi sulle meccaniche',
    'Rispondere a domande sul gioco',
  ],
  questions: (gameName: string) => [
    `Come funziona ${gameName}?`,
    `Quali sono le strategie migliori?`,
    `Puoi spiegarmi una regola specifica?`,
  ],
};

/**
 * Generate a welcome message for a new chat thread.
 *
 * @param gameName - The board game name
 * @param agentTypology - The agent typology (Tutor, Arbitro, Stratega, Narratore)
 * @returns Welcome message content and follow-up questions
 */
export function generateWelcomeMessage(
  gameName: string,
  agentTypology?: string
): WelcomeMessage {
  const config = (agentTypology && TYPOLOGY_WELCOME[agentTypology]) || DEFAULT_WELCOME;
  const displayName = gameName || 'il tuo gioco';

  const greeting = config.greeting(displayName);
  const capabilitiesList = config.capabilities
    .map(c => `• ${c}`)
    .join('\n');

  const content = `${greeting}\n\nEcco cosa posso fare:\n${capabilitiesList}\n\nChiedimi quello che vuoi!`;

  return {
    content,
    followUpQuestions: config.questions(displayName),
  };
}

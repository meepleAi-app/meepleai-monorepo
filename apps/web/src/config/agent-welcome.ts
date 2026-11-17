/**
 * Agent Welcome Message
 * Issue #4780: Welcome message + auto-redirect post agent creation
 *
 * #5001 (ADR-098 §5): one welcome, the same for every intent. There is one agent (ADR-094), and the intent
 * changes the SHAPE of the answer, not its domain. There used to be a welcome per typology, keyed `Tutor`,
 * `Arbitro`, `Stratega`, `Narratore` while threads store the intents in lower case, so `auto`, `tutor` and `secca`
 * all landed on a fallback that promised «strategie». Strategy and game advice are not in the rulebook: the
 * welcome, its capabilities and its questions talk about the rules.
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
// The welcome
// ============================================================================

const WELCOME: AgentWelcomeConfig = {
  greeting:
    'Ciao! Sono il tuo assistente per le regole di {game}. Rispondo con quello che dice il regolamento, e ti indico dove lo dice.',
  capabilities: [
    'Spiegarti come si gioca, passo dopo passo',
    'Aiutarti a preparare il tavolo',
    'Chiarire una regola che non ti è chiara',
  ],
  followUpQuestions: [
    'Come si gioca a {game}?',
    'Come si prepara il tavolo per {game}?',
    'Quali sono le regole principali di {game}?',
  ],
};

// ============================================================================
// Public API
// ============================================================================

/** The welcome configuration: one for every intent. */
export function getAgentWelcome(): AgentWelcomeConfig {
  return WELCOME;
}

/**
 * Build a complete welcome message string with capabilities list.
 */
export function buildWelcomeMessage(gameName: string): string {
  // #3226: replacer function inserts gameName verbatim (a title with $&/$$ would otherwise be
  // interpreted as a replacement pattern and corrupt the text).
  const greeting = WELCOME.greeting.replace(/{game}/g, () => gameName);
  const capabilities = WELCOME.capabilities.map(c => `• ${c}`).join('\n');

  return `${greeting}\n\n**Cosa posso fare:**\n${capabilities}`;
}

/**
 * Get the follow-up questions, with game name substituted.
 */
export function getWelcomeFollowUpQuestions(gameName: string): string[] {
  // #3226: replacer function inserts gameName verbatim (see buildWelcomeMessage).
  return WELCOME.followUpQuestions.map(q => q.replace(/{game}/g, () => gameName));
}

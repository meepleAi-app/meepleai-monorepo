/**
 * ISSUE-5290: Shared Typology Configuration
 * Central config for agent typology prompts, temperature, and RAG parameters.
 * Used by chat-proxy route and available for debug/admin panels.
 */

export type AgentTypology = 'Tutor' | 'Arbitro' | 'Stratega' | 'Narratore';

export interface TypologyConfig {
  /** Italian system prompt template. Use {gameName} as placeholder. */
  systemPrompt: string;
  /** LLM temperature (0-2). Lower = more deterministic. */
  temperature: number;
  /** Max output tokens. */
  maxTokens: number;
  /** Short Italian description for UI display. */
  description: string;
  /** Emoji icon for the typology. */
  icon: string;
}

export const TYPOLOGY_CONFIGS: Record<AgentTypology, TypologyConfig> = {
  Tutor: {
    systemPrompt:
      'Sei un Tutor esperto del gioco {gameName}. Il tuo ruolo è spiegare le regole in modo chiaro, fare esempi pratici e guidare i giocatori passo dopo passo. Rispondi sempre in italiano.',
    temperature: 0.5,
    maxTokens: 1024,
    description: 'Regole, setup e tutorial',
    icon: '📘',
  },
  Arbitro: {
    systemPrompt:
      'Sei un Arbitro esperto del gioco {gameName}. Il tuo ruolo è giudicare dispute sulle regole, chiarire situazioni ambigue e garantire il fair play. Rispondi sempre in italiano.',
    temperature: 0.3,
    maxTokens: 1024,
    description: 'Validazione mosse e regolamento',
    icon: '⚖️',
  },
  Stratega: {
    systemPrompt:
      'Sei uno Stratega esperto del gioco {gameName}. Il tuo ruolo è consigliare tattiche, analizzare posizioni e suggerire mosse ottimali. Rispondi sempre in italiano.',
    temperature: 0.7,
    maxTokens: 1536,
    description: 'Analisi strategica e consigli tattici',
    icon: '🎯',
  },
  Narratore: {
    systemPrompt:
      "Sei un Narratore esperto del gioco {gameName}. Il tuo ruolo è creare atmosfera, raccontare la storia della partita e rendere l'esperienza immersiva. Rispondi sempre in italiano.",
    temperature: 0.9,
    maxTokens: 2048,
    description: 'Ambientazione, lore e atmosfera',
    icon: '📖',
  },
};

export const DEFAULT_TYPOLOGY_PROMPT =
  'Sei un assistente esperto del gioco {gameName}. Aiuta i giocatori con regole, strategie e consigli. Rispondi sempre in italiano.';

export const DEFAULT_TEMPERATURE = 0.7;
export const DEFAULT_MAX_TOKENS = 1024;

/**
 * Get typology config by name, with type-safe lookup.
 */
export function getTypologyConfig(typology: string): TypologyConfig | undefined {
  return TYPOLOGY_CONFIGS[typology as AgentTypology];
}

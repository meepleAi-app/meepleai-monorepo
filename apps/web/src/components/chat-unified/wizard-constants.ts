/**
 * Centralized constants for AgentCreationWizard.
 * Import these in both the component and tests to avoid magic strings.
 * Issue #4915: 4-step wizard to create a user-owned custom agent.
 */

// ─── Agent Type IDs ────────────────────────────────────────────────────────────

export const WIZARD_AGENT_TYPE = {
  Tutor: 'Tutor',
  Arbitro: 'Arbitro',
  Stratega: 'Stratega',
  Narratore: 'Narratore',
} as const;

export type WizardAgentTypeId = (typeof WIZARD_AGENT_TYPE)[keyof typeof WIZARD_AGENT_TYPE];

/**
 * Maps wizard persona types to backend AgentType values accepted by the API validator.
 * Backend accepts: RAG, Citation, Confidence, RulesInterpreter, Conversation, Strategist, Narrator
 */
export const WIZARD_TYPE_TO_BACKEND: Record<WizardAgentTypeId, string> = {
  Tutor: 'RAG',
  Arbitro: 'RulesInterpreter',
  Stratega: 'Strategist',
  Narratore: 'Narrator',
} as const;

// ─── Step Labels ───────────────────────────────────────────────────────────────

export const WIZARD_STEP_LABEL = ['Gioco', 'Tipo', 'Nome & KB', 'Riepilogo'] as const;

export const WIZARD_STEP_TITLE = [
  'Seleziona il gioco',
  'Scegli il tipo di agent',
  'Configura nome e Knowledge Base',
  'Riepilogo e conferma',
] as const;

// ─── Navigation Button Labels ─────────────────────────────────────────────────

export const WIZARD_BTN = {
  Next: 'Avanti',
  Back: 'Indietro',
  Cancel: 'Annulla',
  Submit: 'Crea Agent',
  Submitting: 'Creazione in corso...',
  Edit: 'Modifica',
} as const;

// ─── Test IDs ─────────────────────────────────────────────────────────────────

export const WIZARD_TESTID = {
  LibraryLoading: 'library-loading',
  LibraryEmpty: 'library-empty',
  PdfsEmpty: 'pdfs-empty',
  GameCard: (title: string) => `game-card-${title}`,
  AgentTypeBtn: (typeId: string) => `agent-type-${typeId}`,
  NameInput: 'agent-name-input',
} as const;

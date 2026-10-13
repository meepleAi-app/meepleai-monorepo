/**
 * Shared types for chat entry components
 */

import type { Game } from '@/types';

/** Pre-defined system agent for the selection grid */
export interface AgentOption {
  id: string;
  name: string;
  type: string;
  description: string;
  icon: string;
}

// Issue #4138: an exported `CustomAgent` interface stood here, describing the per-game agents
// read from user_library_entries.CustomAgentConfigJson. Nothing on the answer path read that
// configuration, so the type described a choice that could not change an answer.

/** Quick start suggestion */
export interface QuickStartSuggestion {
  label: string;
  message: string;
  promptType?: PromptType;
}

export type PromptType = 'rule_dispute' | 'setup' | 'general' | 'suggestion';

/** Type adapters — convert API DTOs to domain Game */
export { type Game };

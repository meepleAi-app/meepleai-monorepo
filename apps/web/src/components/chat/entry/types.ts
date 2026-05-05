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

/** Custom agent from backend — user-owned */
export interface CustomAgent {
  id: string;
  name: string;
  type: string;
}

/** Quick start suggestion */
export interface QuickStartSuggestion {
  label: string;
  message: string;
  promptType?: PromptType;
}

export type PromptType = 'rule_dispute' | 'setup' | 'general' | 'suggestion';

/** Type adapters — convert API DTOs to domain Game */
export { type Game };

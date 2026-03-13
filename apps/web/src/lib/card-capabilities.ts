/**
 * CardCapabilityProvider - Declarative enable/disable for card actions
 *
 * Resolves which actions/features are available on a card based on
 * entity type, user role, connectivity, and entity state.
 *
 * @see docs/superpowers/specs/2026-03-13-card-content-specification-design.md
 */

import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card';

// ============================================================================
// Types
// ============================================================================

export interface CardCapabilities {
  aiChat: boolean;
  reindex: boolean;
  scoring: boolean;
  download: boolean;
  delete: boolean;
  flip: boolean;
  drawer: boolean;
  navigate: boolean;
  quickActions: boolean;
}

export type AgentStatus = 'ready' | 'not_ready' | 'no_agent' | 'error' | 'offline' | 'out_of_budget';
export type KbStatus = 'indexed' | 'processing' | 'failed' | 'none';
export type SessionState = 'planning' | 'active' | 'paused' | 'completed';

export interface CardCapabilityContext {
  entity: MeepleEntityType;
  hasAgent: boolean;
  isOnline: boolean;
  isAdmin: boolean;
  isEditor: boolean;
  agentStatus?: AgentStatus;
  kbStatus?: KbStatus;
  sessionState?: SessionState;
}

// ============================================================================
// Resolver
// ============================================================================

const DEFAULTS: CardCapabilities = {
  aiChat: false,
  reindex: false,
  scoring: false,
  download: false,
  delete: false,
  flip: true,
  drawer: true,
  navigate: true,
  quickActions: true,
};

export function resolveCapabilities(ctx: CardCapabilityContext): CardCapabilities {
  const caps = { ...DEFAULTS };

  // AI Chat: requires agent + online + agent ready
  caps.aiChat = ctx.hasAgent && ctx.isOnline && ctx.agentStatus === 'ready';

  // Reindex: KB entity + admin + not currently processing
  caps.reindex = ctx.entity === 'kb' && ctx.isAdmin && ctx.kbStatus !== 'processing';

  // Scoring: session entity + active state
  caps.scoring = ctx.entity === 'session' && ctx.sessionState === 'active';

  // Download: editor or admin
  caps.download = ctx.isEditor || ctx.isAdmin;

  // Delete: admin only
  caps.delete = ctx.isAdmin;

  return caps;
}

// ============================================================================
// Helper
// ============================================================================

/**
 * Check if all required capabilities are enabled.
 *
 * @example
 * ```tsx
 * if (requires(caps, 'aiChat', 'navigate')) {
 *   // show AI chat + navigation
 * }
 * ```
 */
export function requires(
  caps: CardCapabilities,
  ...required: (keyof CardCapabilities)[]
): boolean {
  return required.every((key) => caps[key]);
}

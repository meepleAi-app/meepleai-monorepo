/**
 * Game Templates Configuration (Issue #3164 - GST-005)
 *
 * Pre-configured templates for popular board games with:
 * - Scoring categories
 * - Round structure
 * - Scoring rules
 * - Game icons
 *
 * Used by game-specific toolkit to auto-populate scoreboard.
 */

export interface GameTemplate {
  /** Display name */
  name: string;

  /** Game icon emoji */
  icon: string;

  /** Round numbers (empty array if no fixed rounds) */
  rounds: number[];

  /** Scoring categories */
  categories: string[];

  /** Scoring rules description */
  scoringRules: string;

  /** Min/max players */
  playerCount: {
    min: number;
    max: number;
  };
}

/**
 * Game Templates by slug
 *
 * Slug format: lowercase-with-dashes (e.g., "7-wonders", "splendor")
 */
export const GAME_TEMPLATES: Record<string, GameTemplate> = {
  '7-wonders': {
    name: '7 Wonders',
    icon: '🏛️',
    rounds: [1, 2, 3],
    categories: ['Military', 'Science', 'Commerce', 'Wonders', 'Coins', 'Guilds'],
    scoringRules: 'Final score = sum of all categories. Most points wins.',
    playerCount: { min: 3, max: 7 },
  },

  splendor: {
    name: 'Splendor',
    icon: '💎',
    rounds: [],
    categories: ['Nobles', 'Cards', 'Tokens'],
    scoringRules: 'First player to reach 15 points wins.',
    playerCount: { min: 2, max: 4 },
  },

  catan: {
    name: 'Catan',
    icon: '🏝️',
    rounds: [],
    categories: [
      'Settlements',
      'Cities',
      'Longest Road',
      'Largest Army',
      'Victory Points',
      'Development Cards',
    ],
    scoringRules: 'First player to reach 10 victory points wins.',
    playerCount: { min: 3, max: 4 },
  },

  'ticket-to-ride': {
    name: 'Ticket to Ride',
    icon: '🚂',
    rounds: [],
    categories: ['Routes', 'Tickets', 'Longest Route', 'Stations'],
    scoringRules: 'Points from routes + completed tickets. Most points wins.',
    playerCount: { min: 2, max: 5 },
  },

  wingspan: {
    name: 'Wingspan',
    icon: '🦅',
    rounds: [1, 2, 3, 4],
    categories: ['Birds', 'Bonus Cards', 'End-of-Round Goals', 'Eggs', 'Food', 'Tucked Cards'],
    scoringRules: 'Points from birds, bonus cards, goals, eggs, and tucked cards.',
    playerCount: { min: 1, max: 5 },
  },

  azul: {
    name: 'Azul',
    icon: '🎨',
    rounds: [1, 2, 3, 4, 5],
    categories: ['Wall Tiles', 'Rows', 'Columns', 'Colors', 'Penalties'],
    scoringRules: 'Points from completed patterns, rows, columns, and color sets. Most points wins.',
    playerCount: { min: 2, max: 4 },
  },
};

/**
 * Get template by game slug
 *
 * @param slug - Game slug (lowercase-with-dashes)
 * @returns Template or undefined if not found
 */
export function getGameTemplate(slug: string): GameTemplate | undefined {
  return GAME_TEMPLATES[slug.toLowerCase()];
}

/**
 * Get template by game name (fuzzy match)
 *
 * @param gameName - Game name from database
 * @returns Template or undefined if not found
 */
export function getGameTemplateByName(gameName: string): GameTemplate | undefined {
  const normalized = gameName.toLowerCase().replace(/[^a-z0-9]/g, '-');

  // Direct match
  if (GAME_TEMPLATES[normalized]) {
    return GAME_TEMPLATES[normalized];
  }

  // Fuzzy match (contains)
  for (const [slug, template] of Object.entries(GAME_TEMPLATES)) {
    if (normalized.includes(slug) || slug.includes(normalized)) {
      return template;
    }
  }

  return undefined;
}

/**
 * Check if game has a template
 */
export function hasGameTemplate(gameNameOrSlug: string): boolean {
  return getGameTemplateByName(gameNameOrSlug) !== undefined;
}

/**
 * Get all available template slugs
 */
export function getAvailableTemplates(): string[] {
  return Object.keys(GAME_TEMPLATES);
}

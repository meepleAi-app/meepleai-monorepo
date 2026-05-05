/**
 * Static FAQ dataset for the public /faq route.
 *
 * Wave A.1 (Issue #583) — single source of truth for FAQ structure
 * (id / category / popularity). Display strings live in i18n bundles
 * under `pages.faq.items.{id}.{question|short|long}` to support IT + EN.
 *
 * Mockup parity: admin-mockups/design_files/sp3-faq-enhanced.jsx lines 20-95.
 */

export type CategoryId = 'all' | 'account' | 'games' | 'ai' | 'privacy' | 'billing';

export interface FAQCategory {
  readonly id: CategoryId;
  readonly labelKey: string;
  readonly icon: string;
}

export interface FAQ {
  readonly id: string;
  readonly cat: Exclude<CategoryId, 'all'>;
  readonly questionKey: string;
  readonly shortKey: string;
  readonly longKey: string;
  readonly popular: boolean;
  readonly popularRank?: number;
}

export const FAQ_CATEGORIES: ReadonlyArray<FAQCategory> = [
  { id: 'all', labelKey: 'pages.faq.categories.all.label', icon: '📚' },
  { id: 'account', labelKey: 'pages.faq.categories.account.label', icon: '👤' },
  { id: 'games', labelKey: 'pages.faq.categories.games.label', icon: '🎲' },
  { id: 'ai', labelKey: 'pages.faq.categories.ai.label', icon: '🤖' },
  { id: 'privacy', labelKey: 'pages.faq.categories.privacy.label', icon: '🔒' },
  { id: 'billing', labelKey: 'pages.faq.categories.billing.label', icon: '💳' },
] as const;

function makeFaq(
  id: string,
  cat: Exclude<CategoryId, 'all'>,
  popular: boolean,
  popularRank?: number
): FAQ {
  return {
    id,
    cat,
    questionKey: `pages.faq.items.${id}.question`,
    shortKey: `pages.faq.items.${id}.short`,
    longKey: `pages.faq.items.${id}.long`,
    popular,
    popularRank,
  };
}

export const FAQS: ReadonlyArray<FAQ> = [
  // Account
  makeFaq('q1', 'account', true, 1),
  makeFaq('q2', 'account', false),
  makeFaq('q3', 'account', false),
  // Games
  makeFaq('q4', 'games', true, 2),
  makeFaq('q5', 'games', false),
  makeFaq('q6', 'games', false),
  // AI
  makeFaq('q7', 'ai', true, 3),
  makeFaq('q8', 'ai', false),
  makeFaq('q9', 'ai', false),
  // Privacy
  makeFaq('q10', 'privacy', true, 4),
  makeFaq('q11', 'privacy', false),
  makeFaq('q12', 'privacy', false),
  // Billing
  makeFaq('q13', 'billing', true, 5),
  makeFaq('q14', 'billing', false),
] as const;

/**
 * Top-4 popular FAQs sorted by `popularRank` ascending.
 * (5 popular total → take first 4 to fill 2x2 grid as per mockup decision.)
 */
export const POPULAR_FAQS: ReadonlyArray<FAQ> = FAQS.filter(f => f.popular)
  .slice()
  .sort((a, b) => (a.popularRank ?? Infinity) - (b.popularRank ?? Infinity))
  .slice(0, 4);

/**
 * Map category id → entity color token (used by accordion-item to colorize
 * the inline category badge). 'all' is unused at the badge level (FAQs are
 * always tagged with a concrete cat) but mapped defensively to 'game'.
 */
export const CATEGORY_ENTITY: Record<CategoryId, 'game' | 'player' | 'agent' | 'kb' | 'event'> = {
  all: 'game',
  account: 'player',
  games: 'game',
  ai: 'agent',
  privacy: 'kb',
  billing: 'event',
} as const;

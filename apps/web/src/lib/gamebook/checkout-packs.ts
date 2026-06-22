/**
 * Pack catalog for the libro-game quota/credits checkout modal (Issue #953).
 *
 * Mockup: `admin-mockups/design_files/sp6-libro-game-quota-credits.jsx` lines 50-54.
 * Visual-only: pricing is hardcoded; no backend endpoint exists. When real Stripe
 * + billing BC land, replace this constant with a `GET /api/v1/budget/packs` fetch.
 *
 * Locale: it-IT, currency EUR. Locale hardcoded by design (single-market product).
 */

export type CheckoutPackId = 'starter' | 'mid' | 'pro';
export type CheckoutPackBadge = 'popular' | 'save' | null;

export interface CheckoutPack {
  readonly id: CheckoutPackId;
  readonly name: string;
  readonly priceEur: number;
  readonly credits: number;
  readonly perParagraphEur: number;
  readonly badge: CheckoutPackBadge;
}

export const CHECKOUT_PACKS: readonly CheckoutPack[] = [
  {
    id: 'starter',
    name: 'Starter',
    priceEur: 5,
    credits: 100,
    perParagraphEur: 0.05,
    badge: 'popular',
  },
  { id: 'mid', name: 'Mid', priceEur: 20, credits: 500, perParagraphEur: 0.04, badge: null },
  { id: 'pro', name: 'Pro', priceEur: 35, credits: 1000, perParagraphEur: 0.035, badge: 'save' },
] as const;

/**
 * Resolve a {@link CheckoutPack} by id. Since `CheckoutPackId` is the exact
 * union of `CHECKOUT_PACKS[*].id`, this lookup is total by construction — the
 * throw is a defensive guard against future config drift (e.g. removing a pack
 * id from `CHECKOUT_PACKS` without removing the corresponding `CheckoutPackId`
 * variant).
 */
export function getCheckoutPack(id: CheckoutPackId): CheckoutPack {
  const pack = CHECKOUT_PACKS.find(p => p.id === id);
  if (!pack) {
    throw new Error(`Checkout pack "${id}" not found in CHECKOUT_PACKS — config drift`);
  }
  return pack;
}

/**
 * Format EUR value using Italian locale. Integer values render without decimals
 * ("5 €"); fractional values render with two decimals and comma separator
 * ("5,50 €").
 */
export function formatEur(value: number): string {
  return new Intl.NumberFormat('it-IT', {
    style: 'currency',
    currency: 'EUR',
    minimumFractionDigits: value % 1 === 0 ? 0 : 2,
  }).format(value);
}

/**
 * Deterministic color utility for users when the BE doesn't expose `User.AccentHue`.
 *
 * FNV-1a 32-bit hash for a fast, deterministic, well-distributed mapping
 * `userId` → `hue` ∈ [0, 360). Purely visual — no security or crypto guarantees.
 *
 * Motivation: the mockups `sp4-game-detail.jsx` / `sp4-player-detail.jsx` use a
 * per-user hue for avatar backgrounds, but the BE `User` entity does not expose
 * an `AccentHue` field (PILOT_GAP_REPORT.md § 3.5 decision #5). This module is a
 * stable client-side replacement so the same user gets the same color across
 * sessions and devices.
 *
 * @see issue #1470
 * @module
 */

const FNV_OFFSET_BASIS = 2166136261;
const FNV_PRIME = 16777619;

/**
 * Returns a deterministic hue (0-359) for a given user identifier.
 *
 * Same input → same output across sessions and platforms. An empty `userId`
 * returns `0` so consumers can render an "unknown user" uniformly without
 * branching.
 *
 * @example
 *   userHue('p-marco')  // → stable hue ∈ [0, 360)
 *   userHue('')         // → 0
 */
export function userHue(userId: string): number {
  if (!userId) return 0;
  let hash = FNV_OFFSET_BASIS;
  for (let i = 0; i < userId.length; i++) {
    hash ^= userId.charCodeAt(i);
    hash = Math.imul(hash, FNV_PRIME);
  }
  // `hash` is a signed 32-bit integer after `Math.imul`; mod 360 of its absolute
  // value yields a value in [0, 360).
  return Math.abs(hash) % 360;
}

/**
 * Returns a full HSL color string for the given user identifier.
 *
 * Saturation 60% and lightness 55% are tuned for AA contrast on the project's
 * light background and remain readable on the dark variant.
 *
 * @param userId - see {@link userHue}
 * @param alpha - optional alpha channel ∈ [0, 1]; when set, returns `hsla()`
 *
 * @example
 *   userHsl('p-marco')        // → 'hsl(<hue>, 60%, 55%)'
 *   userHsl('p-marco', 0.4)   // → 'hsla(<hue>, 60%, 55%, 0.4)'
 */
export function userHsl(userId: string, alpha?: number): string {
  const h = userHue(userId);
  return alpha !== undefined ? `hsla(${h}, 60%, 55%, ${alpha})` : `hsl(${h}, 60%, 55%)`;
}

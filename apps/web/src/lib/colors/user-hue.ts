/**
 * Deterministic color utility for users when BE doesn't expose `User.AccentHue`.
 *
 * Uses FNV-1a 32-bit hash for fast, deterministic, well-distributed mapping
 * `userId` → `hue` ∈ [0, 360). Purely visual — no security implications,
 * no crypto guarantees.
 *
 * Motivation: the mockup `admin-mockups/design_files/sp4-game-detail.jsx`
 * (function `Leaderboard`) uses `p.color : number` (hue 0-360) for avatar
 * background colors. The BE `User` entity does not currently expose an
 * `AccentHue` field (verified 2026-05-24 PM via `PILOT_GAP_REPORT.md § 3.5`
 * decision #5). This module provides a client-side stable replacement so
 * the same user gets the same color across sessions and devices.
 *
 * @see docs `admin-mockups/design_handoff/PILOT_GAP_REPORT.md` decision #5
 * @see issue #1470
 * @module
 */

const FNV_OFFSET_BASIS = 2166136261;
const FNV_PRIME = 16777619;

/**
 * Returns a deterministic hue (0-359) for a given user identifier.
 * Same input → same output across sessions and platforms.
 *
 * Empty `userId` returns `0` so consumers can handle "unknown user"
 * uniformly without branching.
 *
 * @example
 *   userHue('p-marco')                                  // → stable hue ∈ [0, 360)
 *   userHue('00000000-0000-0000-0000-000000000001')     // → stable hue ∈ [0, 360)
 *   userHue('')                                         // → 0
 */
export function userHue(userId: string): number {
  if (!userId) return 0;
  let hash = FNV_OFFSET_BASIS;
  for (let i = 0; i < userId.length; i++) {
    hash ^= userId.charCodeAt(i);
    hash = Math.imul(hash, FNV_PRIME);
  }
  // `hash` is a signed 32-bit integer after `Math.imul`; mod 360 of its
  // absolute value yields a value in [0, 360).
  return Math.abs(hash) % 360;
}

/**
 * Returns a full HSL color string for the given user identifier.
 *
 * Saturation 60% and Lightness 55% are tuned for AA contrast on the project's
 * light background (`--bg-base` = `#f7f3ee`) and remain readable on the dark
 * variant. Override via CSS custom properties if a specific contrast target
 * is required.
 *
 * @param userId — see {@link userHue}
 * @param alpha — optional alpha channel ∈ [0, 1]; when set, returns `hsla()`
 *
 * @example
 *   userHsl('p-marco')           // → 'hsl(123, 60%, 55%)' (example)
 *   userHsl('p-marco', 0.3)      // → 'hsla(123, 60%, 55%, 0.3)'
 */
export function userHsl(userId: string, alpha?: number): string {
  const h = userHue(userId);
  return alpha !== undefined ? `hsla(${h}, 60%, 55%, ${alpha})` : `hsl(${h}, 60%, 55%)`;
}

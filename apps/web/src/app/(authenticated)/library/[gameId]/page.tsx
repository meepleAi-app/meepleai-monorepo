/**
 * Library Game Detail — Consolidated Route
 * Issue #5039 — Consolidate User Routes
 *
 * New canonical path: /library/[gameId]
 * Old path (redirected via next.config.js): /library/games/[gameId]
 *
 * Re-uses the same page component — the param name `gameId` matches
 * so `useParams()` works identically at both routes during migration.
 */

// Re-export the existing page component from the legacy location.
// Both routes share the same gameId param name, so the component
// works without modification.
export { default } from '../games/[gameId]/page';

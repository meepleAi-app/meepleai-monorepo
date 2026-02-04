/**
 * API Schemas Index (FE-IMP-005)
 *
 * Centralized export for all Zod validation schemas
 */

// Auth schemas
export * from './auth.schemas';

// Games & Sessions schemas
export * from './games.schemas';

// Chat & Knowledge Base schemas
export * from './chat.schemas';

// PDF & Document Processing schemas
export * from './pdf.schemas';

// Document Collections schemas (Issue #2051)
export * from './documents.schemas';

// System Configuration schemas
export * from './config.schemas';

// Agent schemas (Issue #868)
export * from './agents.schemas';

// Streaming SSE schemas (Issue #1007)
export * from './streaming.schemas';

// Admin schemas (Issue #1679)
export * from './admin.schemas';

// Report schemas (Issue #920)
export * from './reports.schemas';

// Testing schemas (Issue #2139)
export * from './testing.schemas';

// ShareLinks schemas (Issue #2052)
export * from './share-links.schemas';

// Shared Game Catalog schemas (Issue #2372)
// BggGameDetailsSchema and BggGameDetails are re-exported from games.schemas in shared-games.schemas
// TypeScript handles the re-export correctly, so we can use export *
export * from './shared-games.schemas';

// User Library schemas
export * from './library.schemas';

// Share Requests schemas (Issue #2743)
export * from './share-requests.schemas';

// Admin Share Requests schemas (Issue #2745)
export * from './admin-share-requests.schemas';

// Game Contributors schemas (Issue #2746)
export * from './game-contributors.schemas';

// Agent Configuration schemas (Issue #2518)
export * from './agent-config.schemas';

// Agent Typologies schemas (Issue #AGT-012)
export * from './agent-typologies.schemas';

// AI Models Management schemas (Issue #2521)
export * from './ai-models.schemas';

// Badge & Gamification schemas (Issue #2747)
export * from './badges.schemas';

// Rate Limit Configuration schemas (Issue #2750)
export * from './rate-limits.schemas';

// Session Quota schemas (Issue #3075)
export * from './session-quota.schemas';

// Email Verification schemas (Issue #3076)
export * from './email-verification.schemas';

// Chat Sessions schemas (Issue #3484)
export * from './chat-sessions.schemas';

// Tier-Strategy Configuration schemas (Issue #3440)
export * from './tier-strategy.schemas';

// AI Usage Tracking schemas (Issue #3338)
export * from './ai-usage.schemas';

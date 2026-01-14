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
export * from './shared-games.schemas';

// User Library schemas
export * from './library.schemas';

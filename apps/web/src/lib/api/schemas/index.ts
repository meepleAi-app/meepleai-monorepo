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

// System Configuration schemas
export * from './config.schemas';

// Agent schemas (Issue #868)
export * from './agents.schemas';

// Streaming SSE schemas (Issue #1007)
export * from './streaming.schemas';

// Admin schemas (Issue #1679)
export * from './admin.schemas';

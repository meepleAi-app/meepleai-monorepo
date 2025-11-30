/**
 * Security Utilities Barrel Export
 *
 * Centralized security utilities for the MeepleAI frontend.
 * Implements defense-in-depth against XSS, injection, and other web vulnerabilities.
 *
 * @module security
 */

export {
  sanitizeHtml,
  sanitizeUserContent,
  htmlToPlainText,
  createSafeMarkup,
  isSafeUrl,
  sanitizeUrl,
  needsSanitization,
} from './sanitize';

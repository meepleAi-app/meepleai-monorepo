/**
 * HTML Sanitization Utilities
 *
 * Security Issue: Multiple CodeQL warnings related to XSS vulnerabilities
 * Solution: Centralized sanitization using DOMPurify to prevent XSS attacks
 *
 * Usage:
 * ```tsx
 * import { sanitizeHtml, createSafeMarkup } from '@/lib/security/sanitize';
 *
 * // For dangerouslySetInnerHTML
 * <div dangerouslySetInnerHTML={createSafeMarkup(userContent)} />
 *
 * // For direct sanitization
 * const cleanHtml = sanitizeHtml(userContent);
 * ```
 *
 * References:
 * - CWE-79: Cross-site Scripting (XSS)
 * - OWASP A03:2021 - Injection
 * - DOMPurify: https://github.com/cure53/DOMPurify
 *
 * @module security/sanitize
 */

import DOMPurify from 'dompurify';

/**
 * Default DOMPurify configuration for MeepleAI
 *
 * Allows safe HTML tags and attributes commonly used in board game rules:
 * - Text formatting: b, i, u, em, strong, span, p, br
 * - Lists: ul, ol, li
 * - Tables: table, thead, tbody, tr, th, td
 * - Headers: h1-h6
 * - Links: a (with safe attributes only)
 * - Images: img (with safe attributes only)
 *
 * Security:
 * - Removes all JavaScript (on* attributes, javascript: URLs)
 * - Removes data: URLs (except safe image formats)
 * - Removes potentially dangerous tags (script, iframe, object, embed)
 */
const DEFAULT_CONFIG: DOMPurify.Config = {
  // Allowed tags
  ALLOWED_TAGS: [
    // Text formatting
    'b', 'i', 'u', 'em', 'strong', 'span', 'p', 'br', 'hr',
    // Lists
    'ul', 'ol', 'li',
    // Tables
    'table', 'thead', 'tbody', 'tr', 'th', 'td',
    // Headers
    'h1', 'h2', 'h3', 'h4', 'h5', 'h6',
    // Links and images
    'a', 'img',
    // Code
    'code', 'pre',
    // Blockquotes
    'blockquote',
  ],

  // Allowed attributes
  ALLOWED_ATTR: [
    'href', 'title', 'alt', 'src', 'class', 'id',
    // For accessibility
    'aria-label', 'aria-describedby', 'role',
    // For tables
    'colspan', 'rowspan',
  ],

  // Additional security options
  ALLOW_DATA_ATTR: false, // Prevent data-* attributes (can be used for attacks)
  ALLOW_UNKNOWN_PROTOCOLS: false, // Only allow http, https, mailto
  SAFE_FOR_TEMPLATES: true, // Prevent mXSS attacks in template engines
  RETURN_TRUSTED_TYPE: false, // For browser compatibility

  // URL sanitization - blocks data:, javascript:, vbscript:, file:
  ALLOWED_URI_REGEXP: /^(?:(?:(?:f|ht)tps?|mailto|tel|callto|cid|xmpp):|[^a-z]|[a-z+.-]+(?:[^a-z+.\-:]|$))/i,

  // Additional protections
  ADD_TAGS: [], // Don't add any extra tags
  ADD_ATTR: [], // Don't add any extra attributes
  FORCE_BODY: false, // Don't force wrapping in <body>
  SANITIZE_DOM: true, // Enable DOM sanitization (anti-mXSS)
  KEEP_CONTENT: true, // Keep text content when removing tags
};

/**
 * Strict DOMPurify configuration for user-generated content
 *
 * More restrictive than default config:
 * - No links (removes <a> tags)
 * - No images (removes <img> tags)
 * - Only basic text formatting
 *
 * Use for untrusted user input (comments, forum posts, etc.)
 */
const STRICT_CONFIG: DOMPurify.Config = {
  ALLOWED_TAGS: [
    'b', 'i', 'u', 'em', 'strong', 'p', 'br',
    'ul', 'ol', 'li',
    'code', 'pre',
  ],
  ALLOWED_ATTR: ['class'], // Only allow class for styling
  ALLOW_DATA_ATTR: false,
  ALLOW_UNKNOWN_PROTOCOLS: false,
  SAFE_FOR_TEMPLATES: true,
};

/**
 * Plain text configuration - strips all HTML
 *
 * Use when you need to convert HTML to plain text safely
 */
const PLAIN_TEXT_CONFIG: DOMPurify.Config = {
  ALLOWED_TAGS: [], // Remove all tags
  ALLOWED_ATTR: [],
  KEEP_CONTENT: true, // Keep text content
};

/**
 * Sanitizes HTML content using DOMPurify
 *
 * This is the primary sanitization function. Use it before rendering
 * any HTML from untrusted sources.
 *
 * @param html - Raw HTML string to sanitize
 * @param config - Optional DOMPurify configuration (defaults to DEFAULT_CONFIG)
 * @returns Sanitized HTML string safe for rendering
 *
 * @example
 * ```tsx
 * const userInput = '<script>alert("XSS")</script><p>Safe content</p>';
 * const safe = sanitizeHtml(userInput);
 * // Result: '<p>Safe content</p>'
 * ```
 */
export function sanitizeHtml(
  html: string,
  config: DOMPurify.Config = DEFAULT_CONFIG
): string {
  if (typeof window === 'undefined') {
    // Server-side: return empty string (DOMPurify requires DOM)
    // In production, consider using a server-side sanitization library
    console.warn('DOMPurify requires a browser environment. Returning empty string.');
    return '';
  }

  // Add hooks to remove dangerous URLs (data:, javascript:, etc.)
  DOMPurify.addHook('uponSanitizeAttribute', (node, data) => {
    // Remove data: URLs from src and href attributes
    if (data.attrName === 'src' || data.attrName === 'href') {
      const value = data.attrValue?.toLowerCase() || '';

      // Block dangerous protocols
      if (
        value.startsWith('data:') ||
        value.startsWith('javascript:') ||
        value.startsWith('vbscript:') ||
        value.startsWith('file:')
      ) {
        // Remove the attribute entirely
        data.keepAttr = false;
      }
    }

    // Sanitize title attribute to prevent mXSS
    if (data.attrName === 'title') {
      const value = data.attrValue || '';

      // Remove HTML tags and dangerous content from title attribute
      // Title attributes should only contain plain text
      const cleaned = value
        .replace(/<[^>]*>/g, '') // Remove all HTML tags
        .replace(/javascript:/gi, '') // Remove javascript: protocol
        .replace(/on\w+\s*=/gi, ''); // Remove event handlers

      data.attrValue = cleaned;
    }
  });

  const result = DOMPurify.sanitize(html, config);

  // Remove hooks after sanitization to avoid memory leaks
  DOMPurify.removeAllHooks();

  return result;
}

/**
 * Sanitizes HTML with strict rules (user-generated content)
 *
 * Use for untrusted user input like comments, forum posts, etc.
 * More restrictive than default sanitization.
 *
 * @param html - Raw HTML string to sanitize
 * @returns Strictly sanitized HTML string
 *
 * @example
 * ```tsx
 * const comment = '<a href="evil.com">Click me</a><b>Bold text</b>';
 * const safe = sanitizeUserContent(comment);
 * // Result: '<b>Bold text</b>' (link removed)
 * ```
 */
export function sanitizeUserContent(html: string): string {
  return sanitizeHtml(html, STRICT_CONFIG);
}

/**
 * Converts HTML to plain text by stripping all tags
 *
 * Use when you need plain text output from HTML input
 * (e.g., for meta descriptions, search indexing)
 *
 * @param html - Raw HTML string
 * @returns Plain text with all HTML tags removed
 *
 * @example
 * ```tsx
 * const html = '<p>Hello <b>world</b>!</p>';
 * const text = htmlToPlainText(html);
 * // Result: 'Hello world!'
 * ```
 */
export function htmlToPlainText(html: string): string {
  return sanitizeHtml(html, PLAIN_TEXT_CONFIG);
}

/**
 * Creates a safe object for React's dangerouslySetInnerHTML
 *
 * This is the recommended way to use dangerouslySetInnerHTML with user content.
 * It automatically sanitizes the HTML before creating the __html object.
 *
 * @param html - Raw HTML string to sanitize
 * @param config - Optional DOMPurify configuration
 * @returns Object with __html property containing sanitized HTML
 *
 * @example
 * ```tsx
 * function RuleContent({ content }: { content: string }) {
 *   return <div dangerouslySetInnerHTML={createSafeMarkup(content)} />;
 * }
 * ```
 */
export function createSafeMarkup(
  html: string,
  config: DOMPurify.Config = DEFAULT_CONFIG
): { __html: string } {
  return {
    __html: sanitizeHtml(html, config),
  };
}

/**
 * Validates if a URL is safe for use in links
 *
 * Checks for:
 * - javascript: protocol (XSS vector)
 * - data: protocol (XSS vector)
 * - Allows: http, https, mailto, tel
 *
 * @param url - URL string to validate
 * @returns true if URL is safe, false otherwise
 *
 * @example
 * ```tsx
 * isSafeUrl('https://example.com') // true
 * isSafeUrl('javascript:alert(1)') // false
 * isSafeUrl('data:text/html,<script>alert(1)</script>') // false
 * ```
 */
export function isSafeUrl(url: string): boolean {
  if (!url) return false;

  const trimmedUrl = url.trim().toLowerCase();

  // Block dangerous protocols
  const dangerousProtocols = ['javascript:', 'data:', 'vbscript:', 'file:'];
  if (dangerousProtocols.some(protocol => trimmedUrl.startsWith(protocol))) {
    return false;
  }

  // Allow safe protocols or relative URLs
  const safeProtocols = ['http://', 'https://', 'mailto:', 'tel:', '/'];
  return safeProtocols.some(protocol => trimmedUrl.startsWith(protocol));
}

/**
 * Sanitizes a URL for use in href attributes
 *
 * Returns a safe URL or '#' if the URL is unsafe
 *
 * @param url - URL string to sanitize
 * @returns Sanitized URL or '#' if unsafe
 *
 * @example
 * ```tsx
 * <a href={sanitizeUrl(userUrl)}>Link</a>
 * ```
 */
export function sanitizeUrl(url: string): string {
  return isSafeUrl(url) ? url : '#';
}

/**
 * Type guard for checking if sanitization is needed
 *
 * Use this to determine if content needs sanitization before rendering
 *
 * @param content - Content to check
 * @returns true if content might contain HTML that needs sanitization
 */
export function needsSanitization(content: string): boolean {
  // Check for common HTML patterns
  return /<[^>]+>/.test(content);
}

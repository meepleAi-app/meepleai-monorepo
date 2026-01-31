/**
 * Security Tests for Rich Text Editor (Issue #715)
 * Tests XSS protection via DOMPurify sanitization
 */

import { renderHook } from "@testing-library/react";
import { useMemo } from "react";
import DOMPurify from "dompurify";

describe("Editor XSS Protection (SEC-715)", () => {
  /**
   * Helper to simulate the sanitization logic from editor.tsx
   */
  const useSanitizedContent = (richContent: string) => {
    return useMemo(() => {
      if (!richContent) return "";

      return DOMPurify.sanitize(richContent, {
        ALLOWED_TAGS: [
          'p', 'br', 'strong', 'em', 'u', 's', 'h1', 'h2', 'h3', 'h4', 'h5', 'h6',
          'ul', 'ol', 'li', 'blockquote', 'code', 'pre', 'a', 'table',
          'thead', 'tbody', 'tr', 'th', 'td', 'span', 'div'
        ],
        ALLOWED_ATTR: ['href', 'class', 'style', 'target', 'rel'],
        ALLOW_DATA_ATTR: false,
        ALLOW_UNKNOWN_PROTOCOLS: false,
        SAFE_FOR_TEMPLATES: true
      });
    }, [richContent]);
  };

  describe("XSS Attack Prevention", () => {
    it("should sanitize basic script injection", () => {
      const malicious = '<script>alert("XSS")</script><p>Safe content</p>';
      const { result } = renderHook(() => useSanitizedContent(malicious));
      const sanitized = result.current;

      expect(sanitized).not.toContain('<script>');
      expect(sanitized).not.toContain('alert');
      expect(sanitized).toContain('<p>Safe content</p>');
    });

    it("should sanitize event handlers (onerror)", () => {
      const malicious = '<img src=x onerror=alert("XSS")>';
      const { result } = renderHook(() => useSanitizedContent(malicious));
      const sanitized = result.current;

      expect(sanitized).not.toContain('onerror');
      expect(sanitized).not.toContain('alert');
    });

    it("should sanitize event handlers (onclick)", () => {
      const malicious = '<button onclick="alert(\'XSS\')">Click me</button>';
      const { result } = renderHook(() => useSanitizedContent(malicious));
      const sanitized = result.current;

      expect(sanitized).not.toContain('onclick');
      expect(sanitized).not.toContain('alert');
    });

    it("should sanitize SVG-based XSS", () => {
      const malicious = '<svg onload=alert("XSS")><circle r="50"/></svg>';
      const { result } = renderHook(() => useSanitizedContent(malicious));
      const sanitized = result.current;

      expect(sanitized).not.toContain('svg');
      expect(sanitized).not.toContain('onload');
      expect(sanitized).not.toContain('alert');
    });

    it("should sanitize javascript: protocol URLs", () => {
      const malicious = '<a href="javascript:alert(\'XSS\')">Click</a>';
      const { result } = renderHook(() => useSanitizedContent(malicious));
      const sanitized = result.current;

      expect(sanitized).not.toContain('javascript:');
      expect(sanitized).not.toContain('alert');
    });

    it("should sanitize data: URI XSS", () => {
      const malicious = '<a href="data:text/html,<script>alert(\'XSS\')</script>">Click</a>';
      const { result } = renderHook(() => useSanitizedContent(malicious));
      const sanitized = result.current;

      // DOMPurify removes the entire dangerous href
      expect(sanitized).not.toContain('data:text/html');
      expect(sanitized).not.toContain('<script>');
    });

    it("should sanitize iframe injection", () => {
      const malicious = '<iframe src="javascript:alert(\'XSS\')"></iframe>';
      const { result } = renderHook(() => useSanitizedContent(malicious));
      const sanitized = result.current;

      expect(sanitized).not.toContain('iframe');
      expect(sanitized).not.toContain('javascript:');
    });

    it("should sanitize object/embed tags", () => {
      const malicious = '<object data="javascript:alert(\'XSS\')"></object>';
      const { result } = renderHook(() => useSanitizedContent(malicious));
      const sanitized = result.current;

      expect(sanitized).not.toContain('object');
      expect(sanitized).not.toContain('javascript:');
    });
  });

  describe("Legitimate Content Preservation", () => {
    it("should preserve safe HTML formatting", () => {
      const safe = '<p>This is <strong>bold</strong> and <em>italic</em> text.</p>';
      const { result } = renderHook(() => useSanitizedContent(safe));
      const sanitized = result.current;

      expect(sanitized).toContain('<p>');
      expect(sanitized).toContain('<strong>bold</strong>');
      expect(sanitized).toContain('<em>italic</em>');
    });

    it("should preserve headings", () => {
      const safe = '<h1>Title</h1><h2>Subtitle</h2><h3>Section</h3>';
      const { result } = renderHook(() => useSanitizedContent(safe));
      const sanitized = result.current;

      expect(sanitized).toContain('<h1>Title</h1>');
      expect(sanitized).toContain('<h2>Subtitle</h2>');
      expect(sanitized).toContain('<h3>Section</h3>');
    });

    it("should preserve lists", () => {
      const safe = '<ul><li>Item 1</li><li>Item 2</li></ul>';
      const { result } = renderHook(() => useSanitizedContent(safe));
      const sanitized = result.current;

      expect(sanitized).toContain('<ul>');
      expect(sanitized).toContain('<li>Item 1</li>');
      expect(sanitized).toContain('<li>Item 2</li>');
    });

    it("should preserve safe links with href", () => {
      const safe = '<a href="https://example.com">Link</a>';
      const { result } = renderHook(() => useSanitizedContent(safe));
      const sanitized = result.current;

      expect(sanitized).toContain('href="https://example.com"');
      expect(sanitized).toContain('>Link</a>');
    });

    it("should preserve code blocks", () => {
      const safe = '<pre><code>const x = 42;</code></pre>';
      const { result } = renderHook(() => useSanitizedContent(safe));
      const sanitized = result.current;

      expect(sanitized).toContain('<pre>');
      expect(sanitized).toContain('<code>');
      expect(sanitized).toContain('const x = 42;');
    });

    it("should preserve tables", () => {
      const safe = '<table><thead><tr><th>Header</th></tr></thead><tbody><tr><td>Data</td></tr></tbody></table>';
      const { result } = renderHook(() => useSanitizedContent(safe));
      const sanitized = result.current;

      expect(sanitized).toContain('<table>');
      expect(sanitized).toContain('<thead>');
      expect(sanitized).toContain('<th>Header</th>');
      expect(sanitized).toContain('<td>Data</td>');
    });
  });

  describe("Edge Cases", () => {
    it("should handle empty string", () => {
      const { result } = renderHook(() => useSanitizedContent(""));
      const sanitized = result.current;

      expect(sanitized).toBe("");
    });

    it("should handle plain text without HTML", () => {
      const plainText = "This is plain text without any HTML tags.";
      const { result } = renderHook(() => useSanitizedContent(plainText));
      const sanitized = result.current;

      expect(sanitized).toBe(plainText);
    });

    it("should handle nested malicious content", () => {
      const malicious = '<div><script>alert("XSS")</script><p>Safe</p></div>';
      const { result } = renderHook(() => useSanitizedContent(malicious));
      const sanitized = result.current;

      expect(sanitized).not.toContain('<script>');
      expect(sanitized).toContain('<div>');
      expect(sanitized).toContain('<p>Safe</p>');
    });

    it("should handle mixed safe and unsafe content", () => {
      const mixed = '<p>Safe paragraph</p><script>alert("XSS")</script><strong>Bold text</strong>';
      const { result } = renderHook(() => useSanitizedContent(mixed));
      const sanitized = result.current;

      expect(sanitized).toContain('<p>Safe paragraph</p>');
      expect(sanitized).toContain('<strong>Bold text</strong>');
      expect(sanitized).not.toContain('<script>');
      expect(sanitized).not.toContain('alert');
    });
  });

  describe("Performance and Memoization", () => {
    it("should memoize results for same input", () => {
      const content = '<p>Test content</p>';
      const { result, rerender } = renderHook(
        ({ input }) => useSanitizedContent(input),
        { initialProps: { input: content } }
      );

      const firstResult = result.current;
      rerender({ input: content });
      const secondResult = result.current;

      // Same reference means memoization is working
      expect(firstResult).toBe(secondResult);
    });

    it("should re-sanitize on content change", () => {
      const initialContent = '<p>Initial</p>';
      const newContent = '<p>Updated</p>';
      const { result, rerender } = renderHook(
        ({ input }) => useSanitizedContent(input),
        { initialProps: { input: initialContent } }
      );

      const firstResult = result.current;
      expect(firstResult).toContain('Initial');

      rerender({ input: newContent });
      const secondResult = result.current;

      expect(secondResult).toContain('Updated');
      expect(firstResult).not.toBe(secondResult);
    });
  });
});

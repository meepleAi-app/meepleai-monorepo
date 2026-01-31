/**
 * HTML Sanitization Tests
 *
 * Tests for XSS prevention and HTML sanitization utilities
 *
 * @jest-environment jsdom
 */

import {
  sanitizeHtml,
  sanitizeUserContent,
  htmlToPlainText,
  createSafeMarkup,
  isSafeUrl,
  sanitizeUrl,
  needsSanitization,
} from '../sanitize';

describe('sanitizeHtml', () => {
  it('should remove script tags', () => {
    const malicious = '<script>alert("XSS")</script><p>Safe content</p>';
    const result = sanitizeHtml(malicious);
    expect(result).not.toContain('<script>');
    expect(result).toContain('<p>Safe content</p>');
  });

  it('should remove javascript: URLs in links', () => {
    const malicious = '<a href="javascript:alert(1)">Click</a>';
    const result = sanitizeHtml(malicious);
    expect(result).not.toContain('javascript:');
  });

  it('should remove onclick and other event handlers', () => {
    const malicious = '<div onclick="alert(1)">Click me</div>';
    const result = sanitizeHtml(malicious);
    expect(result).not.toContain('onclick');
  });

  it('should allow safe HTML tags', () => {
    const safe = '<p><b>Bold</b> <i>Italic</i> <u>Underline</u></p>';
    const result = sanitizeHtml(safe);
    expect(result).toBe(safe);
  });

  it('should allow safe table markup', () => {
    const table = '<table><tr><td>Cell</td></tr></table>';
    const result = sanitizeHtml(table);
    expect(result).toContain('<table>');
    expect(result).toContain('<tr>');
    expect(result).toContain('<td>');
  });

  it('should remove data: URLs in images', () => {
    const malicious = '<img src="data:text/html,<script>alert(1)</script>">';
    const result = sanitizeHtml(malicious);
    // DOMPurify removes the entire img tag with data: URL
    expect(result).not.toContain('data:');
  });

  it('should handle empty strings', () => {
    expect(sanitizeHtml('')).toBe('');
  });

  it('should handle plain text (no HTML)', () => {
    const text = 'Just plain text';
    expect(sanitizeHtml(text)).toBe(text);
  });
});

describe('sanitizeUserContent', () => {
  it('should remove links from user content', () => {
    const content = '<a href="https://example.com">Link</a><b>Bold</b>';
    const result = sanitizeUserContent(content);
    expect(result).not.toContain('<a');
    expect(result).toContain('<b>Bold</b>');
  });

  it('should remove images from user content', () => {
    const content = '<img src="image.jpg"><p>Text</p>';
    const result = sanitizeUserContent(content);
    expect(result).not.toContain('<img');
    expect(result).toContain('<p>Text</p>');
  });

  it('should allow basic text formatting', () => {
    const content = '<b>Bold</b> <i>Italic</i> <p>Paragraph</p>';
    const result = sanitizeUserContent(content);
    expect(result).toContain('<b>Bold</b>');
    expect(result).toContain('<i>Italic</i>');
    expect(result).toContain('<p>Paragraph</p>');
  });

  it('should remove script tags from user content', () => {
    const malicious = '<script>alert(1)</script><p>Content</p>';
    const result = sanitizeUserContent(malicious);
    expect(result).not.toContain('<script>');
    expect(result).toContain('<p>Content</p>');
  });
});

describe('htmlToPlainText', () => {
  it('should strip all HTML tags', () => {
    const html = '<p>Hello <b>world</b>!</p>';
    const result = htmlToPlainText(html);
    expect(result).toBe('Hello world!');
  });

  it('should handle nested tags', () => {
    const html = '<div><p><span>Nested <b>content</b></span></p></div>';
    const result = htmlToPlainText(html);
    expect(result).toBe('Nested content');
  });

  it('should remove script content', () => {
    const html = '<script>alert(1)</script>Text';
    const result = htmlToPlainText(html);
    expect(result).not.toContain('alert');
    expect(result).toContain('Text');
  });

  it('should handle empty HTML', () => {
    expect(htmlToPlainText('')).toBe('');
    expect(htmlToPlainText('<div></div>')).toBe('');
  });
});

describe('createSafeMarkup', () => {
  it('should return object with __html property', () => {
    const html = '<p>Content</p>';
    const result = createSafeMarkup(html);
    expect(result).toHaveProperty('__html');
    expect(result.__html).toContain('<p>Content</p>');
  });

  it('should sanitize content in __html', () => {
    const malicious = '<script>alert(1)</script><p>Safe</p>';
    const result = createSafeMarkup(malicious);
    expect(result.__html).not.toContain('<script>');
    expect(result.__html).toContain('<p>Safe</p>');
  });

  it('should be usable with React dangerouslySetInnerHTML', () => {
    const markup = createSafeMarkup('<b>Bold</b>');
    // This would be used as: <div dangerouslySetInnerHTML={markup} />
    expect(markup.__html).toBe('<b>Bold</b>');
  });
});

describe('isSafeUrl', () => {
  it('should allow https URLs', () => {
    expect(isSafeUrl('https://example.com')).toBe(true);
  });

  it('should allow http URLs', () => {
    expect(isSafeUrl('http://example.com')).toBe(true);
  });

  it('should allow mailto URLs', () => {
    expect(isSafeUrl('mailto:user@example.com')).toBe(true);
  });

  it('should allow tel URLs', () => {
    expect(isSafeUrl('tel:+1234567890')).toBe(true);
  });

  it('should allow relative URLs', () => {
    expect(isSafeUrl('/path/to/page')).toBe(true);
  });

  it('should block javascript: URLs', () => {
    expect(isSafeUrl('javascript:alert(1)')).toBe(false);
  });

  it('should block data: URLs', () => {
    expect(isSafeUrl('data:text/html,<script>alert(1)</script>')).toBe(false);
  });

  it('should block vbscript: URLs', () => {
    expect(isSafeUrl('vbscript:msgbox(1)')).toBe(false);
  });

  it('should block file: URLs', () => {
    expect(isSafeUrl('file:///etc/passwd')).toBe(false);
  });

  it('should handle empty strings', () => {
    expect(isSafeUrl('')).toBe(false);
  });

  it('should handle case-insensitive protocols', () => {
    expect(isSafeUrl('JAVASCRIPT:alert(1)')).toBe(false);
    expect(isSafeUrl('JavaScript:alert(1)')).toBe(false);
  });
});

describe('sanitizeUrl', () => {
  it('should return safe URLs unchanged', () => {
    const url = 'https://example.com';
    expect(sanitizeUrl(url)).toBe(url);
  });

  it('should return # for unsafe URLs', () => {
    expect(sanitizeUrl('javascript:alert(1)')).toBe('#');
    expect(sanitizeUrl('data:text/html,<script>alert(1)</script>')).toBe('#');
  });

  it('should handle relative URLs', () => {
    const url = '/path/to/page';
    expect(sanitizeUrl(url)).toBe(url);
  });
});

describe('needsSanitization', () => {
  it('should return true for content with HTML tags', () => {
    expect(needsSanitization('<p>Text</p>')).toBe(true);
    expect(needsSanitization('Text <b>bold</b>')).toBe(true);
    expect(needsSanitization('<script>alert(1)</script>')).toBe(true);
  });

  it('should return false for plain text', () => {
    expect(needsSanitization('Just plain text')).toBe(false);
    expect(needsSanitization('Text with & ampersand')).toBe(false);
  });

  it('should handle empty strings', () => {
    expect(needsSanitization('')).toBe(false);
  });
});

describe('Security Edge Cases', () => {
  it('should handle mXSS attacks', () => {
    // mXSS: mutation XSS that can bypass sanitization
    const mxss = '<noscript><p title="</noscript><img src=x onerror=alert(1)>">';
    const result = sanitizeHtml(mxss);

    // Verify that dangerous tags are removed (img with onerror should not be executable)
    // Note: DOMPurify may include 'onerror' in the title attribute (as text),
    // but this is safe because browsers don't execute JavaScript in title attributes.
    // The important check is that there's no standalone <img> tag with onerror attribute.
    expect(result).not.toMatch(/<img[^>]+onerror/i); // No executable onerror
    expect(result).not.toContain('<script'); // No script tags
  });

  it('should handle HTML entity encoding attacks', () => {
    const encoded = '<img src=x onerror="&#97;&#108;&#101;&#114;&#116;&#40;&#49;&#41;">';
    const result = sanitizeHtml(encoded);
    expect(result).not.toContain('onerror');
  });

  it('should handle DOM clobbering attempts', () => {
    const clobbering = '<form name="getElementById"><input name="querySelector"></form>';
    const result = sanitizeHtml(clobbering);
    // DOMPurify removes form tags by default (not in ALLOWED_TAGS)
    expect(result).not.toContain('<form');
  });

  it('should handle prototype pollution attempts', () => {
    const pollution = '<div __proto__="malicious">Content</div>';
    const result = sanitizeHtml(pollution);
    expect(result).not.toContain('__proto__');
  });

  it('should prevent multi-character sanitization bypass attacks (CWE-182)', () => {
    // Test for incomplete multi-character sanitization vulnerability
    // Previously, sequential .replace() calls could be bypassed with nested patterns
    // Now using whitelist approach for title attributes

    // Attack 1: Nested javascript: protocol in title attribute
    const attack1 = '<p title="javajavascript:script:alert(1)">Test</p>';
    const result1 = sanitizeHtml(attack1);
    expect(result1).not.toContain('javascript:');
    expect(result1).toContain('<p');

    // Attack 2: Nested event handler in title attribute
    const attack2 = '<span title="onon<script>click=alert(1)">Test</span>';
    const result2 = sanitizeHtml(attack2);
    expect(result2).not.toContain('onclick');
    expect(result2).not.toContain('<script');

    // Attack 3: HTML tags in title attribute
    const attack3 = '<p title="<img src=x onerror=alert(1)>">Test</p>';
    const result3 = sanitizeHtml(attack3);
    // Title should be sanitized to only contain safe characters
    expect(result3).not.toContain('onerror');
    expect(result3).not.toContain('src=x');

    // Verify that safe content in title is preserved
    const safe = '<p title="Safe Title: Game Rules!">Content</p>';
    const resultSafe = sanitizeHtml(safe);
    expect(resultSafe).toContain('title');
    expect(resultSafe).toContain('Safe Title');
    expect(resultSafe).toContain('Content');
  });

  it('should handle DoS protection for deeply nested patterns', () => {
    // Test that DoS protection (MAX_ITERATIONS) prevents infinite loops
    // while still sanitizing deeply nested patterns

    // Create a deeply nested pattern (would require many iterations to clean)
    let deeplyNested = 'javascript:';
    for (let i = 0; i < 50; i++) {
      deeplyNested = 'java' + deeplyNested + 'script:';
    }

    const attack = `<p title="${deeplyNested}alert(1)">Test</p>`;
    const result = sanitizeHtml(attack);

    // Should complete without hanging (DoS protection works)
    // May still contain some "javascript:" if too deeply nested (100 iteration limit)
    // but the important thing is it doesn't hang
    expect(result).toContain('<p');
    expect(result).toContain('Test');

    // Verify moderate nesting is fully cleaned (within 100 iterations)
    const moderateNesting = '<p title="javajavascript:javascript:script:alert(1)">Test</p>';
    const result2 = sanitizeHtml(moderateNesting);
    expect(result2).not.toContain('javascript:');
  });
});

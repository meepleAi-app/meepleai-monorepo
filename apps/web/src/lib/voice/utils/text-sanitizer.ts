/**
 * Text Sanitizer for TTS Output
 *
 * Strips markdown formatting, citations, and URLs from text
 * to produce clean input for speech synthesis.
 */

/**
 * Sanitize text for text-to-speech output.
 *
 * Removes:
 * - Markdown bold/italic (`**text**`, `*text*`, `__text__`, `_text_`)
 * - Markdown headers (`## Header`)
 * - Markdown links (`[text](url)`)
 * - Markdown images (`![alt](url)`)
 * - Citation markers (`[p.15]`, `[Page 3]`, `[Source: doc.pdf]`)
 * - Standalone URLs (http/https)
 * - Markdown code blocks and inline code
 * - Multiple consecutive whitespace characters
 *
 * @param text - Raw text potentially containing markdown
 * @returns Clean text suitable for TTS
 */
export function sanitizeForTts(text: string): string {
  let result = text;

  // Strip fenced code blocks (```...```)
  result = result.replace(/```[\s\S]*?```/g, '');

  // Strip inline code (`code`)
  result = result.replace(/`([^`]+)`/g, '$1');

  // Strip markdown images before links (![alt](url))
  result = result.replace(/!\[([^\]]*)\]\([^)]+\)/g, '$1');

  // Strip markdown links ([text](url)) - keep link text
  result = result.replace(/\[([^\]]+)\]\([^)]+\)/g, '$1');

  // Strip markdown headers (## Header)
  result = result.replace(/^#{1,6}\s+/gm, '');

  // Strip bold/italic markers (order matters: bold before italic)
  // **bold** or __bold__
  result = result.replace(/\*\*([^*]+)\*\*/g, '$1');
  result = result.replace(/__([^_]+)__/g, '$1');
  // *italic* or _italic_
  result = result.replace(/\*([^*]+)\*/g, '$1');
  result = result.replace(/\b_([^_]+)_\b/g, '$1');

  // Strip citation markers: [p.15], [Page 3], [Source: doc.pdf], [1], [ref]
  result = result.replace(/\[(?:p\.\s*\d+|Page\s+\d+|Source:\s*[^\]]+|\d+|ref)\]/gi, '');

  // Strip standalone URLs
  result = result.replace(/https?:\/\/[^\s)]+/g, '');

  // Strip horizontal rules (---, ***, ___)
  result = result.replace(/^[-*_]{3,}\s*$/gm, '');

  // Strip markdown list markers (-, *, +, 1.)
  result = result.replace(/^\s*[-*+]\s+/gm, '');
  result = result.replace(/^\s*\d+\.\s+/gm, '');

  // Collapse multiple whitespace (spaces, tabs) into single space
  result = result.replace(/[^\S\n]+/g, ' ');

  // Collapse multiple newlines into double newline
  result = result.replace(/\n{3,}/g, '\n\n');

  // Trim each line and the whole string
  result = result
    .split('\n')
    .map(line => line.trim())
    .join('\n')
    .trim();

  return result;
}

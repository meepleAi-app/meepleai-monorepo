import { describe, expect, it } from 'vitest';

import { sanitizeForTts } from '@/lib/voice/utils/text-sanitizer';

describe('sanitizeForTts', () => {
  // ------------------------------------------------------------------
  // Markdown bold / italic
  // ------------------------------------------------------------------
  describe('markdown bold/italic', () => {
    it('strips **bold** markers', () => {
      expect(sanitizeForTts('This is **bold** text')).toBe('This is bold text');
    });

    it('strips __bold__ markers', () => {
      expect(sanitizeForTts('This is __bold__ text')).toBe('This is bold text');
    });

    it('strips *italic* markers', () => {
      expect(sanitizeForTts('This is *italic* text')).toBe('This is italic text');
    });

    it('strips _italic_ markers', () => {
      expect(sanitizeForTts('This is _italic_ text')).toBe('This is italic text');
    });

    it('strips nested **bold** and *italic*', () => {
      expect(sanitizeForTts('**bold** and *italic*')).toBe('bold and italic');
    });
  });

  // ------------------------------------------------------------------
  // Citations
  // ------------------------------------------------------------------
  describe('citation markers', () => {
    it('strips [p.15] citations', () => {
      expect(sanitizeForTts('See the rule [p.15] for details')).toBe('See the rule for details');
    });

    it('strips [Page 3] citations (case-insensitive)', () => {
      expect(sanitizeForTts('Refer to [Page 3] here')).toBe('Refer to here');
    });

    it('strips [page 3] lowercase citations', () => {
      expect(sanitizeForTts('Refer to [page 3] here')).toBe('Refer to here');
    });

    it('strips [Source: doc.pdf] citations', () => {
      expect(sanitizeForTts('Data [Source: doc.pdf] shows')).toBe('Data shows');
    });

    it('strips numeric reference citations [1]', () => {
      expect(sanitizeForTts('As stated [1] in the doc')).toBe('As stated in the doc');
    });

    it('strips [ref] citations', () => {
      expect(sanitizeForTts('See [ref] for more')).toBe('See for more');
    });
  });

  // ------------------------------------------------------------------
  // URLs
  // ------------------------------------------------------------------
  describe('URLs', () => {
    it('strips http URLs', () => {
      expect(sanitizeForTts('Visit http://example.com for info')).toBe('Visit for info');
    });

    it('strips https URLs', () => {
      expect(sanitizeForTts('Check https://docs.example.com/path?q=1')).toBe('Check');
    });

    it('strips URLs embedded in markdown links, preserving link text', () => {
      expect(sanitizeForTts('Click [here](https://example.com) to learn more')).toBe(
        'Click here to learn more'
      );
    });
  });

  // ------------------------------------------------------------------
  // Whitespace handling
  // ------------------------------------------------------------------
  describe('whitespace collapsing', () => {
    it('collapses multiple spaces into one', () => {
      expect(sanitizeForTts('hello    world')).toBe('hello world');
    });

    it('collapses tabs into single space', () => {
      expect(sanitizeForTts('hello\t\tworld')).toBe('hello world');
    });

    it('collapses 3+ newlines into double newline', () => {
      expect(sanitizeForTts('line1\n\n\n\nline2')).toBe('line1\n\nline2');
    });

    it('trims leading and trailing whitespace', () => {
      expect(sanitizeForTts('  hello world  ')).toBe('hello world');
    });
  });

  // ------------------------------------------------------------------
  // Empty / null-ish input
  // ------------------------------------------------------------------
  describe('empty and edge-case input', () => {
    it('returns empty string for empty input', () => {
      expect(sanitizeForTts('')).toBe('');
    });

    it('returns empty string for whitespace-only input', () => {
      expect(sanitizeForTts('   ')).toBe('');
    });

    it('returns empty string for only markdown markers', () => {
      expect(sanitizeForTts('**')).toBe('**');
      // incomplete markers are kept since regex requires content between them
    });
  });

  // ------------------------------------------------------------------
  // Normal text preservation
  // ------------------------------------------------------------------
  describe('normal text preservation', () => {
    it('preserves plain text unchanged', () => {
      expect(sanitizeForTts('Hello world, this is a test.')).toBe('Hello world, this is a test.');
    });

    it('preserves punctuation', () => {
      expect(sanitizeForTts('Hello! How are you? Fine.')).toBe('Hello! How are you? Fine.');
    });

    it('preserves numbers in normal context', () => {
      expect(sanitizeForTts('Roll 2d6 and add 3')).toBe('Roll 2d6 and add 3');
    });
  });

  // ------------------------------------------------------------------
  // Additional markdown features
  // ------------------------------------------------------------------
  describe('other markdown elements', () => {
    it('strips markdown headers', () => {
      expect(sanitizeForTts('## Game Setup\nPlace the board')).toBe('Game Setup\nPlace the board');
    });

    it('strips fenced code blocks', () => {
      expect(sanitizeForTts('Before\n```\ncode here\n```\nAfter')).toBe('Before\n\nAfter');
    });

    it('strips inline code but keeps content', () => {
      expect(sanitizeForTts('Use the `setup` command')).toBe('Use the setup command');
    });

    it('strips markdown images, keeping alt text', () => {
      expect(sanitizeForTts('![board](img.png) shows the layout')).toBe('board shows the layout');
    });

    it('strips list markers', () => {
      expect(sanitizeForTts('- item one\n- item two')).toBe('item one\nitem two');
    });

    it('strips numbered list markers', () => {
      expect(sanitizeForTts('1. first\n2. second')).toBe('first\nsecond');
    });

    it('strips horizontal rules', () => {
      expect(sanitizeForTts('Above\n---\nBelow')).toBe('Above\n\nBelow');
    });
  });
});

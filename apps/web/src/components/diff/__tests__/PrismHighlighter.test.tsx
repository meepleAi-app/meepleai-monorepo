/**
 * Unit tests for PrismHighlighter component
 * Tests syntax highlighting with Prism.js
 */

import { render } from '@testing-library/react';
import { vi } from 'vitest';

// Mock Prism BEFORE importing component
vi.mock('prismjs', () => {
  return {
    default: {
      highlight: vi.fn((code: string) => `<span class="highlighted">${code}</span>`),
      languages: {
        json: {},
      },
    },
  };
});

// Mock the JSON language component
vi.mock('prismjs/components/prism-json', () => ({}));

import { PrismHighlighter } from '../PrismHighlighter';
import Prism from 'prismjs';

// Helper to get mocked Prism
const getMockedPrism = () => vi.mocked(Prism);

describe('PrismHighlighter', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Rendering', () => {
    it('should render code with syntax highlighting', () => {
      const code = '{ "key": "value" }';
      const { container } = render(
        <PrismHighlighter code={code} language="json" lineType="unchanged" />
      );

      const codeElement = container.querySelector('code');

      expect(codeElement).toBeInTheDocument();
      expect(codeElement).toHaveClass('language-json');
    });

    it('should apply lineType class', () => {
      const { container } = render(
        <PrismHighlighter code="test" language="json" lineType="added" />
      );

      const codeElement = container.querySelector('code');

      expect(codeElement).toHaveClass('diff-line--added');
    });

    it('should apply custom className', () => {
      const { container } = render(
        <PrismHighlighter
          code="test"
          language="json"
          lineType="unchanged"
          className="custom-class"
        />
      );

      const codeElement = container.querySelector('code');

      expect(codeElement).toHaveClass('custom-class');
    });
  });

  describe('Line Types', () => {
    it('should render added line type', () => {
      const { container } = render(
        <PrismHighlighter code="added" language="json" lineType="added" />
      );

      expect(container.querySelector('.diff-line--added')).toBeInTheDocument();
    });

    it('should render deleted line type', () => {
      const { container } = render(
        <PrismHighlighter code="deleted" language="json" lineType="deleted" />
      );

      expect(container.querySelector('.diff-line--deleted')).toBeInTheDocument();
    });

    it('should render modified line type', () => {
      const { container } = render(
        <PrismHighlighter code="modified" language="json" lineType="modified" />
      );

      expect(container.querySelector('.diff-line--modified')).toBeInTheDocument();
    });

    it('should render unchanged line type', () => {
      const { container } = render(
        <PrismHighlighter code="unchanged" language="json" lineType="unchanged" />
      );

      expect(container.querySelector('.diff-line--unchanged')).toBeInTheDocument();
    });
  });

  describe('Syntax Highlighting', () => {
    it('should call Prism.highlight with correct parameters', () => {
      const mockPrism = getMockedPrism();
      const code = '{ "test": true }';

      render(<PrismHighlighter code={code} language="json" lineType="unchanged" />);

      expect(mockPrism.highlight).toHaveBeenCalledWith(code, mockPrism.languages.json, 'json');
    });

    it('should use dangerouslySetInnerHTML to render highlighted HTML', () => {
      const code = '{ "key": "value" }';
      const { container } = render(
        <PrismHighlighter code={code} language="json" lineType="unchanged" />
      );

      const codeElement = container.querySelector('code');

      expect(codeElement?.innerHTML).toContain('highlighted');
    });

    it('should fallback to plain text on error', () => {
      const mockPrism = getMockedPrism();
      const consoleError = vi.spyOn(console, 'error').mockImplementation();
      mockPrism.highlight.mockImplementation(() => {
        throw new Error('Highlighting error');
      });

      const code = 'plain text';
      const { container } = render(
        <PrismHighlighter code={code} language="json" lineType="unchanged" />
      );

      const codeElement = container.querySelector('code');

      expect(codeElement?.textContent).toBe(code);
      expect(consoleError).toHaveBeenCalledWith('Prism highlighting error:', expect.any(Error));

      consoleError.mockRestore();
    });

    it('should fallback to json language if specified language not found', () => {
      const mockPrism = getMockedPrism();
      mockPrism.languages = { json: {} }; // Only json available

      const code = 'test';
      render(<PrismHighlighter code={code} language="json" lineType="unchanged" />);

      expect(mockPrism.highlight).toHaveBeenCalledWith(code, mockPrism.languages.json, 'json');
    });
  });

  describe('Memoization', () => {
    it('should use useMemo for highlighting', () => {
      const mockPrism = getMockedPrism();
      const code = '{ "test": true }';

      const { rerender } = render(
        <PrismHighlighter code={code} language="json" lineType="unchanged" />
      );

      expect(mockPrism.highlight).toHaveBeenCalledTimes(1);

      // Rerender with same props - should not call highlight again (memoized)
      rerender(<PrismHighlighter code={code} language="json" lineType="unchanged" />);

      expect(mockPrism.highlight).toHaveBeenCalledTimes(1);
    });

    it('should re-highlight when code changes', () => {
      const mockPrism = getMockedPrism();

      const { rerender } = render(
        <PrismHighlighter code="first" language="json" lineType="unchanged" />
      );

      expect(mockPrism.highlight).toHaveBeenCalledTimes(1);

      rerender(<PrismHighlighter code="second" language="json" lineType="unchanged" />);

      expect(mockPrism.highlight).toHaveBeenCalledTimes(2);
    });

    it('should re-highlight when language changes', () => {
      const mockPrism = getMockedPrism();

      const { rerender } = render(
        <PrismHighlighter code="test" language="json" lineType="unchanged" />
      );

      expect(mockPrism.highlight).toHaveBeenCalledTimes(1);

      rerender(<PrismHighlighter code="test" language="json" lineType="added" />);

      // Language didn't change, so should still be 1
      expect(mockPrism.highlight).toHaveBeenCalledTimes(1);
    });
  });

  describe('Edge Cases', () => {
    it('should handle empty code', () => {
      const { container } = render(
        <PrismHighlighter code="" language="json" lineType="unchanged" />
      );

      const codeElement = container.querySelector('code');

      expect(codeElement).toBeInTheDocument();
    });

    it('should handle multiline code', () => {
      const code = '{\n  "key": "value",\n  "number": 123\n}';

      const { container } = render(
        <PrismHighlighter code={code} language="json" lineType="unchanged" />
      );

      expect(container.querySelector('code')).toBeInTheDocument();
    });

    it('should handle special characters', () => {
      const code = '{ "special": "<>&\\"" }';

      const { container } = render(
        <PrismHighlighter code={code} language="json" lineType="unchanged" />
      );

      expect(container.querySelector('code')).toBeInTheDocument();
    });

    it('should handle very long code', () => {
      const longCode = '{ "key": "' + 'x'.repeat(1000) + '" }';

      const { container } = render(
        <PrismHighlighter code={longCode} language="json" lineType="unchanged" />
      );

      expect(container.querySelector('code')).toBeInTheDocument();
    });
  });

  describe('CSS Classes Combination', () => {
    it('should combine all CSS classes correctly', () => {
      const { container } = render(
        <PrismHighlighter code="test" language="json" lineType="added" className="custom" />
      );

      const codeElement = container.querySelector('code');

      expect(codeElement).toHaveClass('language-json');
      expect(codeElement).toHaveClass('diff-line--added');
      expect(codeElement).toHaveClass('custom');
    });

    it('should apply language class with json prefix', () => {
      const { container } = render(
        <PrismHighlighter code="test" language="json" lineType="unchanged" />
      );

      expect(container.querySelector('.language-json')).toBeInTheDocument();
    });
  });
});

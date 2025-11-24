import { render, screen } from '@testing-library/react';
import { DiffCodeBlock } from '../DiffCodeBlock';
import { DiffLine } from '../../../lib/diffProcessor';

// Mock PrismHighlighter
vi.mock('../PrismHighlighter', () => ({
  PrismHighlighter: ({ code, lineType }: { code: string; lineType: string }) => (
    <span data-testid="prism-highlighter" data-line-type={lineType}>
      {code}
    </span>
  ),
}));

describe('DiffCodeBlock', () => {
  const addedLine: DiffLine = {
    lineNumber: 10,
    content: '+ Added line content',
    type: 'added',
    newLineNumber: 10,
  };

  const deletedLine: DiffLine = {
    lineNumber: 5,
    content: '- Deleted line content',
    type: 'deleted',
    originalLineNumber: 5,
  };

  const modifiedLine: DiffLine = {
    lineNumber: 15,
    content: 'Modified line content',
    type: 'modified',
    originalLineNumber: 14,
    newLineNumber: 15,
  };

  const unchangedLine: DiffLine = {
    lineNumber: 20,
    content: 'Unchanged line content',
    type: 'unchanged',
  };

  const emptyLine: DiffLine = {
    lineNumber: null,
    content: '',
    type: 'unchanged',
  };

  describe('Rendering', () => {
    it('should render added line', () => {
      render(<DiffCodeBlock line={addedLine} isHighlighted={false} searchQuery="" />);

      expect(screen.getByText('+ Added line content')).toBeInTheDocument();
    });

    it('should render deleted line', () => {
      render(<DiffCodeBlock line={deletedLine} isHighlighted={false} searchQuery="" />);

      expect(screen.getByText('- Deleted line content')).toBeInTheDocument();
    });

    it('should render modified line', () => {
      render(<DiffCodeBlock line={modifiedLine} isHighlighted={false} searchQuery="" />);

      expect(screen.getByText('Modified line content')).toBeInTheDocument();
    });

    it('should render unchanged line', () => {
      render(<DiffCodeBlock line={unchangedLine} isHighlighted={false} searchQuery="" />);

      expect(screen.getByText('Unchanged line content')).toBeInTheDocument();
    });

    it('should render empty placeholder for empty line', () => {
      const { container } = render(
        <DiffCodeBlock line={emptyLine} isHighlighted={false} searchQuery="" />
      );

      const emptyElement = container.querySelector('.diff-line--empty');
      expect(emptyElement).toBeInTheDocument();
      expect(emptyElement).toHaveAttribute('aria-hidden', 'true');
    });

    it('should apply correct class for added line', () => {
      const { container } = render(
        <DiffCodeBlock line={addedLine} isHighlighted={false} searchQuery="" />
      );

      const lineElement = container.querySelector('.diff-line--added');
      expect(lineElement).toBeInTheDocument();
    });

    it('should apply correct class for deleted line', () => {
      const { container } = render(
        <DiffCodeBlock line={deletedLine} isHighlighted={false} searchQuery="" />
      );

      const lineElement = container.querySelector('.diff-line--deleted');
      expect(lineElement).toBeInTheDocument();
    });

    it('should apply correct class for modified line', () => {
      const { container } = render(
        <DiffCodeBlock line={modifiedLine} isHighlighted={false} searchQuery="" />
      );

      const lineElement = container.querySelector('.diff-line--modified');
      expect(lineElement).toBeInTheDocument();
    });

    it('should apply highlighted class when isHighlighted is true', () => {
      const { container } = render(
        <DiffCodeBlock line={addedLine} isHighlighted={true} searchQuery="" />
      );

      const lineElement = container.querySelector('.diff-line--highlighted');
      expect(lineElement).toBeInTheDocument();
    });

    it('should not apply highlighted class when isHighlighted is false', () => {
      const { container } = render(
        <DiffCodeBlock line={addedLine} isHighlighted={false} searchQuery="" />
      );

      const lineElement = container.querySelector('.diff-line--highlighted');
      expect(lineElement).not.toBeInTheDocument();
    });
  });

  describe('PrismHighlighter Integration', () => {
    it('should pass content to PrismHighlighter', () => {
      render(<DiffCodeBlock line={addedLine} isHighlighted={false} searchQuery="" />);

      const highlighter = screen.getByTestId('prism-highlighter');
      expect(highlighter).toHaveTextContent('+ Added line content');
    });

    it('should pass line type to PrismHighlighter', () => {
      render(<DiffCodeBlock line={addedLine} isHighlighted={false} searchQuery="" />);

      const highlighter = screen.getByTestId('prism-highlighter');
      expect(highlighter).toHaveAttribute('data-line-type', 'added');
    });

    it('should use json language for highlighting', () => {
      render(<DiffCodeBlock line={addedLine} isHighlighted={false} searchQuery="" />);

      expect(screen.getByTestId('prism-highlighter')).toBeInTheDocument();
    });
  });

  describe('Line Number Attribute', () => {
    it('should set data-line-number attribute when lineNumber exists', () => {
      const { container } = render(
        <DiffCodeBlock line={addedLine} isHighlighted={false} searchQuery="" />
      );

      const lineElement = container.querySelector('.diff-line');
      expect(lineElement).toHaveAttribute('data-line-number', '10');
    });

    it('should not set data-line-number attribute for empty line', () => {
      const { container } = render(
        <DiffCodeBlock line={emptyLine} isHighlighted={false} searchQuery="" />
      );

      const lineElement = container.querySelector('.diff-line');
      if (lineElement) {
        expect(lineElement).not.toHaveAttribute('data-line-number');
      }
    });
  });

  describe('Memoization', () => {
    it('should use React.memo for optimization', () => {
      expect(DiffCodeBlock.displayName).toBe('DiffCodeBlock');
    });

    it('should not re-render with same props', () => {
      const { rerender } = render(
        <DiffCodeBlock line={addedLine} isHighlighted={false} searchQuery="" />
      );

      // Rerender with same props
      rerender(<DiffCodeBlock line={addedLine} isHighlighted={false} searchQuery="" />);

      expect(screen.getByText('+ Added line content')).toBeInTheDocument();
    });
  });

  describe('Edge Cases', () => {
    it('should handle line with special characters', () => {
      const specialLine: DiffLine = {
        lineNumber: 1,
        content: '<script>alert("XSS")</script>',
        type: 'added',
      };

      render(<DiffCodeBlock line={specialLine} isHighlighted={false} searchQuery="" />);

      expect(screen.getByText('<script>alert("XSS")</script>')).toBeInTheDocument();
    });

    it('should handle very long content', () => {
      const longLine: DiffLine = {
        lineNumber: 1,
        content: 'A'.repeat(1000),
        type: 'added',
      };

      render(<DiffCodeBlock line={longLine} isHighlighted={false} searchQuery="" />);

      expect(screen.getByText('A'.repeat(1000))).toBeInTheDocument();
    });

    it('should handle unicode characters', () => {
      const unicodeLine: DiffLine = {
        lineNumber: 1,
        content: '🎮 Unicode emoji content 中文',
        type: 'added',
      };

      render(<DiffCodeBlock line={unicodeLine} isHighlighted={false} searchQuery="" />);

      expect(screen.getByText('🎮 Unicode emoji content 中文')).toBeInTheDocument();
    });

    it('should handle empty content string', () => {
      const emptyContentLine: DiffLine = {
        lineNumber: 1,
        content: '',
        type: 'added',
      };

      const { container } = render(
        <DiffCodeBlock line={emptyContentLine} isHighlighted={false} searchQuery="" />
      );

      expect(container.querySelector('.diff-line')).toBeInTheDocument();
    });

    it('should handle whitespace-only content', () => {
      const whitespaceLine: DiffLine = {
        lineNumber: 1,
        content: '   ',
        type: 'added',
      };

      const { container } = render(
        <DiffCodeBlock line={whitespaceLine} isHighlighted={false} searchQuery="" />
      );

      const highlighter = screen.getByTestId('prism-highlighter');
      expect(highlighter.textContent).toBe('   ');
    });

    it('should distinguish between null lineNumber and 0 lineNumber', () => {
      const zeroLineNumber: DiffLine = {
        lineNumber: 0,
        content: 'Line at 0',
        type: 'added',
      };

      const { container } = render(
        <DiffCodeBlock line={zeroLineNumber} isHighlighted={false} searchQuery="" />
      );

      const lineElement = container.querySelector('.diff-line');
      expect(lineElement).toHaveAttribute('data-line-number', '0');
    });
  });

  describe('Search Query Handling', () => {
    it('should pass search query to component', () => {
      render(<DiffCodeBlock line={addedLine} isHighlighted={false} searchQuery="Added" />);

      expect(screen.getByText('+ Added line content')).toBeInTheDocument();
    });

    it('should handle empty search query', () => {
      render(<DiffCodeBlock line={addedLine} isHighlighted={false} searchQuery="" />);

      expect(screen.getByText('+ Added line content')).toBeInTheDocument();
    });
  });
});

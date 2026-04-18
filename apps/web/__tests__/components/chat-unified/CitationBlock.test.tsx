import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { CitationBlock } from '@/components/chat-unified/CitationBlock';

const mockSnippets = [
  { text: 'Setup instructions for the game.', source: 'PDF:abc-123', page: 3, line: 0, score: 0.9 },
  { text: 'Scoring rules at end of game.', source: 'PDF:abc-123', page: 5, line: 0, score: 0.8 },
];

describe('CitationBlock', () => {
  it('renders citation chips for each snippet', () => {
    render(<CitationBlock snippets={mockSnippets} excludeIndices={new Set()} />);
    expect(screen.getByText('Pagina 3')).toBeInTheDocument();
    expect(screen.getByText('Pagina 5')).toBeInTheDocument();
  });

  it('excludes snippets already shown inline', () => {
    render(<CitationBlock snippets={mockSnippets} excludeIndices={new Set([0])} />);
    expect(screen.queryByText('Pagina 3')).not.toBeInTheDocument();
    expect(screen.getByText('Pagina 5')).toBeInTheDocument();
  });

  it('expands accordion on chip click', () => {
    render(<CitationBlock snippets={mockSnippets} excludeIndices={new Set()} />);
    fireEvent.click(screen.getByText('Pagina 3'));
    expect(screen.getByText(/Setup instructions/)).toBeInTheDocument();
  });

  it('renders nothing when all snippets excluded', () => {
    const { container } = render(<CitationBlock snippets={mockSnippets} excludeIndices={new Set([0, 1])} />);
    expect(container.firstChild).toBeNull();
  });
});

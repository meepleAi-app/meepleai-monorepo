import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { InlineCitationText } from '@/components/chat-unified/InlineCitationText';
import type { InlineCitationMatch } from '@/lib/api/clients/chatClient';

const mockSnippets = [
  { text: 'Full chunk text from the rulebook about game setup.', source: 'PDF:abc-123', page: 3, line: 0, score: 0.9 },
];

describe('InlineCitationText', () => {
  it('renders plain text when no citations', () => {
    render(<InlineCitationText text="Hello world" citations={[]} snippets={[]} />);
    expect(screen.getByText('Hello world')).toBeInTheDocument();
  });

  it('renders highlighted span for citation', () => {
    const citations: InlineCitationMatch[] = [
      { startOffset: 0, endOffset: 5, snippetIndex: 0, pageNumber: 3, pdfDocumentId: 'abc-123', confidence: 1.0 },
    ];
    render(<InlineCitationText text="Hello world" citations={citations} snippets={mockSnippets} />);
    const highlight = screen.getByTestId('citation-highlight-0');
    expect(highlight).toBeInTheDocument();
    expect(highlight).toHaveTextContent('Hello');
  });

  it('expands accordion on click', () => {
    const citations: InlineCitationMatch[] = [
      { startOffset: 0, endOffset: 5, snippetIndex: 0, pageNumber: 3, pdfDocumentId: 'abc-123', confidence: 1.0 },
    ];
    render(<InlineCitationText text="Hello world" citations={citations} snippets={mockSnippets} />);
    fireEvent.click(screen.getByTestId('citation-highlight-0'));
    expect(screen.getByTestId('citation-accordion-0')).toBeInTheDocument();
    expect(screen.getByText(/Full chunk text from the rulebook/)).toBeInTheDocument();
  });

  it('renders PDF link icon', () => {
    const citations: InlineCitationMatch[] = [
      { startOffset: 0, endOffset: 5, snippetIndex: 0, pageNumber: 3, pdfDocumentId: 'abc-123', confidence: 1.0 },
    ];
    render(<InlineCitationText text="Hello world" citations={citations} snippets={mockSnippets} />);
    const pdfLink = screen.getByTestId('pdf-link-0');
    expect(pdfLink).toHaveAttribute('href', '/api/v1/pdfs/abc-123/download#page=3');
  });
});

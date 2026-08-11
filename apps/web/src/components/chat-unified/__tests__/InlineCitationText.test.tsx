import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect } from 'vitest';

import { InlineCitationText } from '../InlineCitationText';

import type { InlineCitationMatch } from '@/lib/api/clients/chatClient';

const citations = [
  {
    startOffset: 0,
    endOffset: 5,
    snippetIndex: 0,
    pageNumber: 3,
    pdfDocumentId: 'doc-1',
  },
] as unknown as InlineCitationMatch[];

const snippets = [{ text: 'snippet body', source: 's', page: 3, line: 1, score: 0.9 }];

describe('InlineCitationText — keyboard-operable disclosure (#3263 discovery)', () => {
  it('exposes the citation toggle as a focusable button with aria-expanded', () => {
    render(<InlineCitationText text="Hello world" citations={citations} snippets={snippets} />);

    const toggle = screen.getByTestId('citation-highlight-0');
    expect(toggle).toHaveAttribute('role', 'button');
    expect(toggle).toHaveAttribute('tabindex', '0');
    expect(toggle).toHaveAttribute('aria-expanded', 'false');
  });

  it('expands the snippet on Enter and reflects aria-expanded', async () => {
    const user = userEvent.setup();
    render(<InlineCitationText text="Hello world" citations={citations} snippets={snippets} />);

    const toggle = screen.getByTestId('citation-highlight-0');
    toggle.focus();
    await user.keyboard('{Enter}');

    expect(screen.getByTestId('citation-accordion-0')).toBeInTheDocument();
    expect(toggle).toHaveAttribute('aria-expanded', 'true');
  });
});

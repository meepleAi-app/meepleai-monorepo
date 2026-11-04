/**
 * CitationChip — pure component tests
 * Spec: docs/superpowers/specs/2026-05-09-game-chat-tab-v1-g5-design.md §3.2
 */
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { CitationChip } from '../CitationChip';

describe('CitationChip', () => {
  it('renders page number and section title', () => {
    render(
      <CitationChip pageNumber={12} sectionTitle="Poteri quando attivato" onClick={vi.fn()} />
    );
    expect(
      screen.getByRole('button', { name: /p\. ?12.*Poteri quando attivato/i })
    ).toBeInTheDocument();
  });

  it('calls onClick when clicked', () => {
    const onClick = vi.fn();
    render(<CitationChip pageNumber={4} sectionTitle="Iniziativa" onClick={onClick} />);
    fireEvent.click(screen.getByRole('button'));
    expect(onClick).toHaveBeenCalledOnce();
  });

  it('exposes snippet via title tooltip', () => {
    render(
      <CitationChip
        pageNumber={12}
        sectionTitle="Poteri"
        snippet="Ogni potere si attiva..."
        onClick={vi.fn()}
      />
    );
    expect(screen.getByRole('button')).toHaveAttribute('title', 'Ogni potere si attiva...');
  });

  it('omits title when no snippet', () => {
    render(<CitationChip pageNumber={12} sectionTitle="Poteri" onClick={vi.fn()} />);
    expect(screen.getByRole('button')).not.toHaveAttribute('title');
  });
});

describe('CitationChip — citazione senza pagina (#4819)', () => {
  it.each([null, 0])('names the section alone when the page is %s', page => {
    render(<CitationChip pageNumber={page} sectionTitle="Poteri" onClick={vi.fn()} />);

    const chip = screen.getByRole('button');
    expect(chip).toHaveTextContent('Poteri');
    expect(chip.textContent).not.toContain('p.');
    expect(chip.textContent).not.toContain('—');
  });

  it('still writes the page before the section when there is one', () => {
    render(<CitationChip pageNumber={12} sectionTitle="Poteri" onClick={vi.fn()} />);

    expect(screen.getByRole('button')).toHaveTextContent('p. 12—Poteri');
  });
});

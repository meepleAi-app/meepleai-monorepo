import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { axe } from 'jest-axe';
import { CitationPill } from '../CitationPill';

const refMock = { docId: '550e8400-e29b-41d4-a716-446655440000', page: 14 };

describe('CitationPill', () => {
  it('renders the number + ref text', () => {
    render(
      <CitationPill
        n={1}
        refText="p.14"
        docId={refMock.docId}
        page={refMock.page}
        ariaLabel="Citazione 1, pagina 14"
      />
    );
    expect(screen.getByText('1')).toBeInTheDocument();
    expect(screen.getByText('p.14')).toBeInTheDocument();
  });

  it('has a button role with aria-label', () => {
    render(
      <CitationPill
        n={2}
        refText="p.14 §4.1"
        docId={refMock.docId}
        page={14}
        ariaLabel="Citazione 2, pagina 14 sezione 4.1"
      />
    );
    const btn = screen.getByRole('button', { name: /citazione 2/i });
    expect(btn).toBeInTheDocument();
  });

  it('calls onClick with {docId, page} on click (D-E deep-link)', async () => {
    const onClick = vi.fn();
    render(
      <CitationPill
        n={3}
        refText="p.21"
        docId={refMock.docId}
        page={21}
        ariaLabel="c3"
        onClick={onClick}
      />
    );
    await userEvent.click(screen.getByRole('button'));
    expect(onClick).toHaveBeenCalledWith({ docId: refMock.docId, page: 21 });
  });

  it('is keyboard activatable (Enter + Space)', async () => {
    const onClick = vi.fn();
    render(
      <CitationPill
        n={1}
        refText="p.14"
        docId={refMock.docId}
        page={14}
        ariaLabel="c1"
        onClick={onClick}
      />
    );
    const btn = screen.getByRole('button');
    btn.focus();
    await userEvent.keyboard('{Enter}');
    expect(onClick).toHaveBeenCalledTimes(1);
    await userEvent.keyboard(' ');
    expect(onClick).toHaveBeenCalledTimes(2);
  });

  it('has no a11y violations (jest-axe)', async () => {
    const { container } = render(
      <CitationPill
        n={1}
        refText="p.14"
        docId={refMock.docId}
        page={14}
        ariaLabel="Citazione 1, pagina 14"
      />
    );
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('passes chunkId in onClick payload when prop provided (#1702)', async () => {
    const user = userEvent.setup();
    const onClick = vi.fn();
    render(
      <CitationPill
        n={2}
        refText="Rules"
        docId="doc-1"
        page={5}
        chunkId="doc-1_3"
        ariaLabel="Apri citazione 2"
        onClick={onClick}
      />
    );
    await user.click(screen.getByRole('button'));
    expect(onClick).toHaveBeenCalledWith({ docId: 'doc-1', page: 5, chunkId: 'doc-1_3' });
  });

  it('omits chunkId from onClick payload when prop absent (back-compat)', async () => {
    const user = userEvent.setup();
    const onClick = vi.fn();
    render(
      <CitationPill
        n={1}
        refText="Setup"
        docId="doc-2"
        page={3}
        ariaLabel="Apri citazione 1"
        onClick={onClick}
      />
    );
    await user.click(screen.getByRole('button'));
    expect(onClick).toHaveBeenCalledWith({ docId: 'doc-2', page: 3 });
  });
});
